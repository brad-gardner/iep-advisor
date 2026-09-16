using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Deliberate whole-draft sharing (see <see cref="IDraftSharingService"/>, plan 6, decisions 1, 2, 5, 7).
/// A share freezes the instance's CURRENT value-document into an immutable revision; the previous Active
/// revision (if any) is marked Superseded and a semantic diff (<see cref="ChangeSummaryBuilder"/>) is
/// captured against it. Recipients are every accepted, active family user plus the student's own account
/// — resolved the same way <c>MeetingService.LoadFamilyUserIdsAsync</c> does for meeting invitations.
/// </summary>
public class DraftSharingService : IDraftSharingService
{
    private const string PermissionMessage = "You do not have permission to access this document.";
    private const string NotFoundMessage = "Document not found.";
    private const string RevisionNotFoundMessage = "Shared draft revision not found.";
    private const string PolicyDisabledMessage = "Family draft sharing is disabled for this district.";
    private const string NotShareableStatusMessage = "This document cannot be shared in its current state.";
    private const string NoRecipientsMessage = "This document has no family recipients to share with. Link a parent or student account first.";
    private const string NotActiveMessage = "This revision is not currently active.";
    private const string RaceMessage = "Another share for this document happened at the same time. Please try again.";
    private const int MaxMessageLength = 1000;
    private const int MaxShareAttempts = 3;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly ITemplateAuthoringService _authoring;
    private readonly INotificationService _notifications;
    private readonly IDraftResponseService _responses;
    private readonly IAuditLogger _audit;
    private readonly ILogger<DraftSharingService> _logger;

    public DraftSharingService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IAccessService accessService,
        ITemplateAuthoringService authoring,
        INotificationService notifications,
        IDraftResponseService responses,
        IAuditLogger audit,
        ILogger<DraftSharingService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _authoring = authoring;
        _notifications = notifications;
        _responses = responses;
        _audit = audit;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Staff: preview + share + withdraw

    public async Task<ServiceResult<RecipientPreviewModel>> PreviewRecipientsAsync(int userId, int instanceId, CancellationToken ct = default)
    {
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<RecipientPreviewModel>.FailureResult(NotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<RecipientPreviewModel>.FailureResult(PermissionMessage);

        var policyEnabled = await GetPolicyEnabledAsync(header.SchoolStudentId, ct);
        var recipients = await LoadRecipientsAsync(header.SchoolStudentId, ct);

        var lastSharedAt = await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.DocumentInstanceId == instanceId)
            .OrderByDescending(r => r.RevisionNumber)
            .Select(r => (DateTime?)r.SharedAt)
            .FirstOrDefaultAsync(ct);
        var activeRevisionNumber = await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.DocumentInstanceId == instanceId && r.Status == SharedDraftStatus.Active)
            .Select(r => (int?)r.RevisionNumber)
            .FirstOrDefaultAsync(ct);

        return ServiceResult<RecipientPreviewModel>.SuccessResult(new RecipientPreviewModel
        {
            Recipients = recipients.Select(ToPublicRecipient).ToList(),
            PolicyEnabled = policyEnabled,
            LastSharedAt = lastSharedAt,
            WillSupersedeRevision = activeRevisionNumber
        });
    }

    public async Task<ServiceResult<SharedDraftRevisionModel>> ShareAsync(int userId, int instanceId, string? message, CancellationToken ct = default)
    {
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(NotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(PermissionMessage);
        if (header.Status is not (DocumentInstanceStatus.Draft or DocumentInstanceStatus.Finalizing))
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(NotShareableStatusMessage);

        if (!await GetPolicyEnabledAsync(header.SchoolStudentId, ct))
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(PolicyDisabledMessage);

        var trimmedMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (trimmedMessage != null && trimmedMessage.Length > MaxMessageLength)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult($"Message must be {MaxMessageLength} characters or fewer.");

        var recipients = await LoadRecipientsAsync(header.SchoolStudentId, ct);
        if (recipients.Count == 0)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(NoRecipientsMessage);

        var sections = await TemplateSectionLoader.LoadAsync(_context, header.DocumentTemplateVersionId, ct);

        for (var attempt = 1; attempt <= MaxShareAttempts; attempt++)
        {
            try
            {
                var currentValuesJson = await _context.DocumentInstances.AsNoTracking()
                    .Where(i => i.Id == instanceId)
                    .Select(i => i.ValuesJson)
                    .FirstOrDefaultAsync(ct) ?? "{}";

                var previous = await _context.SharedDraftRevisions
                    .FirstOrDefaultAsync(r => r.DocumentInstanceId == instanceId && r.Status == SharedDraftStatus.Active, ct);
                var maxNumber = await _context.SharedDraftRevisions
                    .Where(r => r.DocumentInstanceId == instanceId)
                    .Select(r => (int?)r.RevisionNumber)
                    .MaxAsync(ct) ?? 0;

                string? changeSummaryJson = null;
                if (previous != null)
                {
                    var diff = ChangeSummaryBuilder.Build(sections, ValueDocumentJson.Parse(previous.ValuesJson), ValueDocumentJson.Parse(currentValuesJson));
                    changeSummaryJson = JsonSerializer.Serialize(diff);
                    previous.Status = SharedDraftStatus.Superseded;
                    previous.UpdatedById = userId;
                }

                var now = DateTime.UtcNow;
                var revision = new SharedDraftRevision
                {
                    DocumentInstanceId = instanceId,
                    RevisionNumber = maxNumber + 1,
                    ValuesJson = currentValuesJson,
                    DocumentTemplateVersionId = header.DocumentTemplateVersionId,
                    SharedByUserId = userId,
                    SharedAt = now,
                    Message = trimmedMessage,
                    Status = SharedDraftStatus.Active,
                    ChangeSummaryJson = changeSummaryJson,
                    CreatedById = userId,
                    UpdatedById = userId
                };
                await _context.SharedDraftRevisions.AddAsync(revision, ct);
                await _context.SaveChangesAsync(ct);

                _audit.Record(AuditAction.Share, userId, "DocumentInstance", instanceId);
                _logger.LogInformation("User {UserId} shared document instance {InstanceId} as revision {RevisionNumber}.", userId, instanceId, revision.RevisionNumber);
                await NotifyRecipientsAsync(instanceId, recipients, revision.Id, revision.RevisionNumber, withdrawn: false, trimmedMessage, ct);

                return ServiceResult<SharedDraftRevisionModel>.SuccessResult(await MapForStaffAsync(revision.Id, ct) ?? throw new InvalidOperationException());
            }
            catch (DbUpdateException ex) when (attempt < MaxShareAttempts)
            {
                _logger.LogWarning(ex, "Concurrent share race on instance {InstanceId}; retrying (attempt {Attempt}).", instanceId, attempt);
                _context.ChangeTracker.Clear();
            }
        }

        return ServiceResult<SharedDraftRevisionModel>.FailureResult(RaceMessage);
    }

    public async Task<ServiceResult<List<SharedDraftRevisionModel>>> ListForInstanceAsync(int userId, int instanceId, CancellationToken ct = default)
    {
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<List<SharedDraftRevisionModel>>.FailureResult(NotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, header.SchoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<SharedDraftRevisionModel>>.FailureResult(PermissionMessage);

        var rows = await ProjectRevisionRows(_context.SharedDraftRevisions.AsNoTracking()
                .Where(r => r.DocumentInstanceId == instanceId)
                .OrderByDescending(r => r.RevisionNumber))
            .ToListAsync(ct);

        return ServiceResult<List<SharedDraftRevisionModel>>.SuccessResult(await MapForStaffAsync(rows, ct));
    }

    public async Task<ServiceResult<SharedDraftRevisionModel>> WithdrawAsync(int userId, int instanceId, int revisionId, CancellationToken ct = default)
    {
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(NotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(PermissionMessage);

        var revision = await _context.SharedDraftRevisions
            .FirstOrDefaultAsync(r => r.Id == revisionId && r.DocumentInstanceId == instanceId, ct);
        if (revision == null)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(RevisionNotFoundMessage);
        if (revision.Status != SharedDraftStatus.Active)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(NotActiveMessage);

        revision.Status = SharedDraftStatus.Withdrawn;
        revision.WithdrawnAt = DateTime.UtcNow;
        revision.WithdrawnByUserId = userId;
        revision.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "DocumentInstance", instanceId);
        var recipients = await LoadRecipientsAsync(header.SchoolStudentId, ct);
        await NotifyRecipientsAsync(instanceId, recipients, revision.Id, revision.RevisionNumber, withdrawn: true, null, ct);

        return ServiceResult<SharedDraftRevisionModel>.SuccessResult(await MapForStaffAsync(revision.Id, ct) ?? throw new InvalidOperationException());
    }

    public async Task<ServiceResult<ConvergeModel>> GetConvergeAsync(int userId, int instanceId, CancellationToken ct = default)
    {
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<ConvergeModel>.FailureResult(NotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, header.SchoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<ConvergeModel>.FailureResult(PermissionMessage);

        var policyEnabled = await GetPolicyEnabledAsync(header.SchoolStudentId, ct);
        var latestRow = await ProjectRevisionRows(_context.SharedDraftRevisions.AsNoTracking()
                .Where(r => r.DocumentInstanceId == instanceId)
                .OrderByDescending(r => r.RevisionNumber))
            .FirstOrDefaultAsync(ct);

        var model = new ConvergeModel
        {
            InstanceId = instanceId,
            PolicyEnabled = policyEnabled,
            CanShare = policyEnabled
                && header.Status is DocumentInstanceStatus.Draft or DocumentInstanceStatus.Finalizing
                && (await LoadRecipientsAsync(header.SchoolStudentId, ct)).Count > 0
        };

        if (latestRow != null)
        {
            model.LatestRevision = await MapForStaffAsync(latestRow, ct);
            model.Acknowledgements = model.LatestRevision.Acknowledgements;

            var currentValuesJson = await _context.DocumentInstances.AsNoTracking()
                .Where(i => i.Id == instanceId).Select(i => i.ValuesJson).FirstOrDefaultAsync(ct) ?? "{}";
            var sections = await TemplateSectionLoader.LoadAsync(_context, header.DocumentTemplateVersionId, ct);
            var latestValuesJson = await LoadValuesJsonAsync(latestRow.Id, ct);
            var diff = ChangeSummaryBuilder.Build(sections, ValueDocumentJson.Parse(latestValuesJson), ValueDocumentJson.Parse(currentValuesJson));
            model.ChangesSinceShare = diff.IsEmpty ? null : diff;

            // Reuses IDraftResponseService's own read (same pattern as HomeService reusing
            // IDistrictService.GetComplianceBoardAsync) so converge can never numerically drift from the
            // staff responses list — the extra authz re-check is accepted overhead for that guarantee.
            var responsesResult = await _responses.GetForInstanceAsync(userId, instanceId, null, ct);
            var responses = responsesResult.Data ?? new List<DraftResponseModel>();
            model.OpenResponses = responses.Where(r => r.Status == DraftResponseStatus.Open).ToList();
            model.ResolvedResponses = responses.Where(r => r.Status == DraftResponseStatus.Resolved).ToList();
        }

        return ServiceResult<ConvergeModel>.SuccessResult(model);
    }

    // ---------------------------------------------------------------- Parent

    public async Task<ServiceResult<List<SharedDraftRevisionModel>>> ListForParentAsync(int parentUserId, int childId, CancellationToken ct = default)
    {
        if (await _accessService.GetRoleAsync(childId, parentUserId, ct) == null)
            return ServiceResult<List<SharedDraftRevisionModel>>.FailureResult("Child profile not found.");

        var linkedStudentIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId)
            .ToListAsync(ct);
        if (linkedStudentIds.Count == 0)
            return ServiceResult<List<SharedDraftRevisionModel>>.SuccessResult(new List<SharedDraftRevisionModel>());

        var rows = await ProjectRevisionRows(_context.SharedDraftRevisions.AsNoTracking()
                .Where(r => linkedStudentIds.Contains(r.DocumentInstance.SchoolStudentId))
                .OrderByDescending(r => r.RevisionNumber))
            .ToListAsync(ct);

        return ServiceResult<List<SharedDraftRevisionModel>>.SuccessResult(await MapForParentAsync(rows, parentUserId, ct));
    }

    public async Task<ServiceResult<SharedDraftRevisionDetailModel>> GetForParentAsync(int parentUserId, int revisionId, CancellationToken ct = default)
    {
        var row = await ProjectRevisionRows(RevisionsById(revisionId)).FirstOrDefaultAsync(ct);
        if (row == null)
            return ServiceResult<SharedDraftRevisionDetailModel>.FailureResult(RevisionNotFoundMessage);

        if (await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, parentUserId, row.SchoolStudentId, AccessRole.Viewer, ct) == null)
            return ServiceResult<SharedDraftRevisionDetailModel>.FailureResult(PermissionMessage);

        var tree = await _authoring.GetVersionAsync(row.DocumentTemplateVersionId, ct);
        if (!tree.Success)
            return ServiceResult<SharedDraftRevisionDetailModel>.FailureResult(tree.Message ?? "The pinned template version could not be loaded.");

        var baseModel = await MapForParentAsync(row, parentUserId, ct);
        var detail = ToDetail(baseModel, await LoadValuesJsonAsync(row.Id, ct), tree.Data!);

        _audit.Record(AuditAction.View, parentUserId, "SharedDraftRevision", revisionId);
        return ServiceResult<SharedDraftRevisionDetailModel>.SuccessResult(detail);
    }

    public async Task<ServiceResult<SharedDraftRevisionModel>> AcknowledgeAsync(int parentUserId, int revisionId, CancellationToken ct = default)
    {
        var row = await ProjectRevisionRows(RevisionsById(revisionId)).FirstOrDefaultAsync(ct);
        if (row == null)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(RevisionNotFoundMessage);

        if (await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, parentUserId, row.SchoolStudentId, AccessRole.Collaborator, ct) == null)
            return ServiceResult<SharedDraftRevisionModel>.FailureResult(PermissionMessage);

        var now = DateTime.UtcNow;
        var existing = await _context.DraftAcknowledgements
            .FirstOrDefaultAsync(a => a.SharedDraftRevisionId == revisionId && a.UserId == parentUserId, ct);
        if (existing == null)
        {
            await _context.DraftAcknowledgements.AddAsync(new DraftAcknowledgement
            {
                SharedDraftRevisionId = revisionId,
                UserId = parentUserId,
                AcknowledgedAt = now,
                CreatedById = parentUserId,
                UpdatedById = parentUserId
            }, ct);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // A concurrent acknowledge (double-tap / client retry) won the unique (revision, user) index.
                // Acknowledging is idempotent, so the existing stamp is the answer — not a 500.
                _context.ChangeTracker.Clear();
                var winner = await _context.DraftAcknowledgements.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.SharedDraftRevisionId == revisionId && a.UserId == parentUserId, ct);
                if (winner == null)
                    throw;
            }
        }
        else
        {
            existing.AcknowledgedAt = now;
            existing.UpdatedById = parentUserId;
            await _context.SaveChangesAsync(ct);
        }

        return ServiceResult<SharedDraftRevisionModel>.SuccessResult(await MapForParentAsync(row, parentUserId, ct));
    }

    // ---------------------------------------------------------------- Recipients + notifications

    /// <summary>A recipient plus (for a family user) the child profile id their own account views this student through.</summary>
    private sealed record RecipientInfo(int UserId, string DisplayName, string Email, string Relationship, int? ChildId);

    private static ShareRecipientModel ToPublicRecipient(RecipientInfo r) => new()
    {
        UserId = r.UserId,
        DisplayName = r.DisplayName,
        Email = r.Email,
        Relationship = r.Relationship
    };

    /// <summary>Accepted, active family users (across every linked ChildProfile) plus the student's own account — mirrors <c>MeetingService.LoadFamilyUserIdsAsync</c> + the student-account lookup.</summary>
    private async Task<List<RecipientInfo>> LoadRecipientsAsync(int schoolStudentId, CancellationToken ct)
    {
        var recipients = new List<RecipientInfo>();

        var childProfileIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.SchoolStudentId == schoolStudentId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
            .Select(l => l.ChildProfileId!.Value)
            .Distinct()
            .ToListAsync(ct);

        if (childProfileIds.Count > 0)
        {
            var familyAccess = await _context.ChildAccesses.AsNoTracking()
                .Where(ca => childProfileIds.Contains(ca.ChildProfileId) && ca.IsActive && ca.AcceptedAt != null && ca.UserId != null)
                .Select(ca => new { ca.ChildProfileId, UserId = ca.UserId!.Value })
                .Distinct()
                .ToListAsync(ct);

            var userIds = familyAccess.Select(a => a.UserId).Distinct().ToList();
            var users = await _context.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToDictionaryAsync(u => u.Id, ct);

            foreach (var a in familyAccess)
            {
                if (!users.TryGetValue(a.UserId, out var u)) continue;
                recipients.Add(new RecipientInfo(u.Id, $"{u.FirstName} {u.LastName}".Trim(), u.Email, "Parent", a.ChildProfileId));
            }
        }

        var studentUserId = await _context.StudentProfiles.AsNoTracking()
            .Where(sp => sp.SchoolStudentId == schoolStudentId)
            .Select(sp => (int?)sp.UserId)
            .FirstOrDefaultAsync(ct);
        if (studentUserId.HasValue && recipients.All(r => r.UserId != studentUserId.Value))
        {
            var su = await _context.Users.AsNoTracking()
                .Where(u => u.Id == studentUserId.Value)
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .FirstOrDefaultAsync(ct);
            if (su != null)
                recipients.Add(new RecipientInfo(su.Id, $"{su.FirstName} {su.LastName}".Trim(), su.Email, "Student", null));
        }

        return recipients;
    }

    private async Task NotifyRecipientsAsync(int instanceId, IReadOnlyList<RecipientInfo> recipients, int revisionId, int revisionNumber, bool withdrawn, string? message, CancellationToken ct)
    {
        var title = withdrawn ? "Draft withdrawn" : "A draft was shared with you";
        var dedupKey = withdrawn ? $"shared-draft-{instanceId}-{revisionNumber}-withdrawn" : $"shared-draft-{instanceId}-{revisionNumber}";

        try
        {
            foreach (var group in recipients.Where(r => r.Relationship == "Parent").GroupBy(r => r.ChildId!.Value))
            {
                var body = withdrawn
                    ? $"Revision {revisionNumber} was withdrawn by the school team."
                    : $"A new draft (revision {revisionNumber}) is ready for you to review." + (message != null ? $" Note from the team: {message}" : string.Empty);
                // Routes key on the revision id (not the per-instance revision number).
                var linkPath = $"/children/{group.Key}/shared-drafts/{revisionId}";
                await _notifications.NotifyAsync(group.Select(g => g.UserId), NotificationKind.DraftShared, title, body, linkPath, dedupKey, emailImmediately: true, ct);
            }

            var studentIds = recipients.Where(r => r.Relationship == "Student").Select(r => r.UserId).ToList();
            if (studentIds.Count > 0)
            {
                var body = withdrawn
                    ? $"Revision {revisionNumber} was withdrawn by the school team."
                    : $"A new draft (revision {revisionNumber}) is ready for you to review.";
                await _notifications.NotifyAsync(studentIds, NotificationKind.DraftShared, title, body, $"/shared-drafts/{revisionId}", dedupKey, emailImmediately: true, ct);
            }
        }
        catch (Exception ex)
        {
            // Notification failure must never roll back or fail the share/withdraw that already committed.
            _logger.LogError(ex, "Failed to queue DraftShared notifications for instance {InstanceId} revision {RevisionNumber}.", instanceId, revisionNumber);
        }
    }

    // ---------------------------------------------------------------- Loading + mapping

    private sealed record InstanceHeader(int SchoolStudentId, int DocumentTypeId, int DocumentTemplateVersionId, DocumentInstanceStatus Status);

    private async Task<InstanceHeader?> LoadInstanceHeaderAsync(int instanceId, CancellationToken ct) =>
        await _context.DocumentInstances.AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => new InstanceHeader(i.SchoolStudentId, i.DocumentTypeId, i.DocumentTemplateVersionId, i.Status))
            .FirstOrDefaultAsync(ct);

    private async Task<bool> GetPolicyEnabledAsync(int schoolStudentId, CancellationToken ct) =>
        await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => s.District.FamilyDraftSharingEnabled)
            .FirstOrDefaultAsync(ct);

    private sealed record RevisionRow(
        int Id, int DocumentInstanceId, int SchoolStudentId, string StudentName, string DocumentTypeKey, string DocumentTypeDisplayName,
        int RevisionNumber, SharedDraftStatus Status, DateTime SharedAt, string SharedByName, string? Message, DateTime? WithdrawnAt,
        string? ChangeSummaryJson, int DocumentTemplateVersionId);

    /// <summary>
    /// Projects a raw, already-filtered <see cref="SharedDraftRevision"/> query into <see cref="RevisionRow"/>.
    /// The filter MUST be applied to <paramref name="source"/> before calling this — EF Core cannot
    /// translate a further <c>.Where()</c> against a property of the projected record afterward (it has
    /// no way to map <c>RevisionRow.Id</c> back through the join chain to the underlying column).
    /// </summary>
    private static IQueryable<RevisionRow> ProjectRevisionRows(IQueryable<SharedDraftRevision> source) =>
        source.Select(r => new RevisionRow(
            r.Id,
            r.DocumentInstanceId,
            r.DocumentInstance.SchoolStudentId,
            r.DocumentInstance.SchoolStudent.FirstName + " " + r.DocumentInstance.SchoolStudent.LastName,
            r.DocumentInstance.DocumentType.Key,
            r.DocumentInstance.DocumentType.DisplayName,
            r.RevisionNumber,
            r.Status,
            r.SharedAt,
            r.SharedByUser.FirstName + " " + r.SharedByUser.LastName,
            r.Message,
            r.WithdrawnAt,
            r.ChangeSummaryJson,
            r.DocumentTemplateVersionId));

    /// <summary>The frozen value document — fetched on its own, only by the single-revision paths that render or diff it.</summary>
    private Task<string> LoadValuesJsonAsync(int revisionId, CancellationToken ct) =>
        _context.SharedDraftRevisions.AsNoTracking().Where(r => r.Id == revisionId).Select(r => r.ValuesJson).FirstAsync(ct);

    private IQueryable<SharedDraftRevision> RevisionsById(int revisionId) =>
        _context.SharedDraftRevisions.AsNoTracking().Where(r => r.Id == revisionId);

    private async Task<SharedDraftRevisionModel> MapForStaffAsync(int revisionId, CancellationToken ct)
    {
        var row = await ProjectRevisionRows(RevisionsById(revisionId)).FirstAsync(ct);
        return await MapForStaffAsync(row, ct);
    }

    private async Task<SharedDraftRevisionModel> MapForStaffAsync(RevisionRow row, CancellationToken ct) =>
        (await MapForStaffAsync(new[] { row }, ct))[0];

    // The list endpoints are unpaged and revisions are never purged, so the per-revision extras are
    // batch-loaded for the whole id set: three round trips regardless of how many times a draft was shared.
    private async Task<List<SharedDraftRevisionModel>> MapForStaffAsync(IReadOnlyList<RevisionRow> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var acks = await _context.DraftAcknowledgements.AsNoTracking()
            .Where(a => ids.Contains(a.SharedDraftRevisionId))
            .Select(a => new { a.SharedDraftRevisionId, ParentName = a.User.FirstName + " " + a.User.LastName, a.AcknowledgedAt })
            .ToListAsync(ct);
        var acksByRevision = acks.ToLookup(a => a.SharedDraftRevisionId);
        var openCounts = await OpenResponseCountsAsync(ids, ct);

        return rows.Select(row =>
        {
            var model = MapRow(row);
            model.Acknowledgements = acksByRevision[row.Id]
                .Select(a => new AcknowledgementModel { ParentName = a.ParentName.Trim(), AcknowledgedAt = a.AcknowledgedAt })
                .ToList();
            model.OpenResponseCount = openCounts.GetValueOrDefault(row.Id);
            return model;
        }).ToList();
    }

    private async Task<SharedDraftRevisionModel> MapForParentAsync(RevisionRow row, int parentUserId, CancellationToken ct) =>
        (await MapForParentAsync(new[] { row }, parentUserId, ct))[0];

    private async Task<List<SharedDraftRevisionModel>> MapForParentAsync(IReadOnlyList<RevisionRow> rows, int parentUserId, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var acknowledgedAt = await _context.DraftAcknowledgements.AsNoTracking()
            .Where(a => ids.Contains(a.SharedDraftRevisionId) && a.UserId == parentUserId)
            .Select(a => new { a.SharedDraftRevisionId, a.AcknowledgedAt })
            .ToDictionaryAsync(a => a.SharedDraftRevisionId, a => a.AcknowledgedAt, ct);
        var openCounts = await OpenResponseCountsAsync(ids, ct);

        return rows.Select(row =>
        {
            var model = MapRow(row);
            model.AcknowledgedAt = acknowledgedAt.TryGetValue(row.Id, out var at) ? at : null;
            model.OpenResponseCount = openCounts.GetValueOrDefault(row.Id);
            return model;
        }).ToList();
    }

    private async Task<Dictionary<int, int>> OpenResponseCountsAsync(List<int> revisionIds, CancellationToken ct) =>
        await _context.DraftResponses.AsNoTracking()
            .Where(r => revisionIds.Contains(r.SharedDraftRevisionId) && r.Status == DraftResponseStatus.Open)
            .GroupBy(r => r.SharedDraftRevisionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

    private static SharedDraftRevisionModel MapRow(RevisionRow r) => new()
    {
        Id = r.Id,
        DocumentInstanceId = r.DocumentInstanceId,
        StudentId = r.SchoolStudentId,
        StudentName = r.StudentName.Trim(),
        DocumentTypeKey = r.DocumentTypeKey,
        DocumentTypeDisplayName = r.DocumentTypeDisplayName,
        RevisionNumber = r.RevisionNumber,
        Status = r.Status,
        SharedAt = r.SharedAt,
        SharedByName = r.SharedByName.Trim(),
        Message = r.Message,
        WithdrawnAt = r.WithdrawnAt,
        ChangeSummary = string.IsNullOrWhiteSpace(r.ChangeSummaryJson) ? null : JsonSerializer.Deserialize<ChangeSummaryModel>(r.ChangeSummaryJson),
        TemplateVersionId = r.DocumentTemplateVersionId
    };

    private static SharedDraftRevisionDetailModel ToDetail(SharedDraftRevisionModel m, string valuesJson, TemplateVersionDetailModel tree) => new()
    {
        Id = m.Id,
        DocumentInstanceId = m.DocumentInstanceId,
        StudentId = m.StudentId,
        StudentName = m.StudentName,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        RevisionNumber = m.RevisionNumber,
        Status = m.Status,
        SharedAt = m.SharedAt,
        SharedByName = m.SharedByName,
        Message = m.Message,
        WithdrawnAt = m.WithdrawnAt,
        ChangeSummary = m.ChangeSummary,
        AcknowledgedAt = m.AcknowledgedAt,
        Acknowledgements = m.Acknowledgements,
        OpenResponseCount = m.OpenResponseCount,
        TemplateVersionId = m.TemplateVersionId,
        ValuesJson = valuesJson,
        TemplateVersion = tree
    };
}
