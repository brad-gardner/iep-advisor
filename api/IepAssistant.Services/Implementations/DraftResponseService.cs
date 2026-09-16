using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Per-item family responses to a shared revision (see <see cref="IDraftResponseService"/>, plan 6,
/// decision 4). A parent may respond only on the currently Active revision; staff (Collaborator+) see
/// every response across an instance's revisions and resolve with a reply and/or a "resolved in the
/// draft" flag — at least one is required, so a response can never silently disappear.
/// </summary>
public class DraftResponseService : IDraftResponseService
{
    private const string RevisionNotFoundMessage = "Shared draft revision not found.";
    private const string InstanceNotFoundMessage = "Document not found.";
    private const string PermissionMessage = "You do not have permission to access this document.";
    private const string ResponseNotFoundMessage = "Response not found.";
    private const string NotActiveMessage = "You can only respond to the currently active revision.";
    private const string ResolveRequiresInputMessage = "Provide a reply or mark this resolved in the draft.";
    private const int MaxTextLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IOrgAccessService _orgAccess;
    private readonly INotificationService _notifications;
    private readonly ILogger<DraftResponseService> _logger;

    public DraftResponseService(
        ApplicationDbContext context,
        IAccessService accessService,
        IOrgAccessService orgAccess,
        INotificationService notifications,
        ILogger<DraftResponseService> logger)
    {
        _context = context;
        _accessService = accessService;
        _orgAccess = orgAccess;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<ServiceResult<DraftResponseModel>> CreateAsync(int parentUserId, int revisionId, CreateDraftResponseModel model, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(model.Kind))
            return ServiceResult<DraftResponseModel>.FailureResult("Invalid kind.");
        if (string.IsNullOrWhiteSpace(model.Text))
            return ServiceResult<DraftResponseModel>.FailureResult("Text is required.");
        var text = model.Text.Trim();
        if (text.Length > MaxTextLength)
            return ServiceResult<DraftResponseModel>.FailureResult($"Text must be {MaxTextLength} characters or fewer.");

        var header = await LoadRevisionHeaderAsync(revisionId, ct);
        if (header == null)
            return ServiceResult<DraftResponseModel>.FailureResult(RevisionNotFoundMessage);

        var childId = await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, parentUserId, header.SchoolStudentId, AccessRole.Collaborator, ct);
        if (childId == null)
            return ServiceResult<DraftResponseModel>.FailureResult(PermissionMessage);

        if (header.Status != SharedDraftStatus.Active)
            return ServiceResult<DraftResponseModel>.FailureResult(NotActiveMessage);

        var response = new DraftResponse
        {
            SharedDraftRevisionId = revisionId,
            ParentUserId = parentUserId,
            TargetFieldKey = model.TargetFieldKey,
            TargetRowId = model.TargetRowId,
            Kind = model.Kind,
            Text = text,
            Status = DraftResponseStatus.Open,
            CreatedById = parentUserId,
            UpdatedById = parentUserId
        };
        await _context.DraftResponses.AddAsync(response, ct);
        await _context.SaveChangesAsync(ct);

        await NotifyTeamAsync(header, response, ct);

        return ServiceResult<DraftResponseModel>.SuccessResult(await MapAsync(response.Id, ct));
    }

    public async Task<ServiceResult<List<DraftResponseModel>>> GetForParentAsync(int parentUserId, int revisionId, CancellationToken ct = default)
    {
        var header = await LoadRevisionHeaderAsync(revisionId, ct);
        if (header == null)
            return ServiceResult<List<DraftResponseModel>>.FailureResult(RevisionNotFoundMessage);
        if (await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, parentUserId, header.SchoolStudentId, AccessRole.Viewer, ct) == null)
            return ServiceResult<List<DraftResponseModel>>.FailureResult(PermissionMessage);

        var rows = await ProjectResponseRows(_context.DraftResponses.AsNoTracking()
                .Where(r => r.SharedDraftRevisionId == revisionId && r.ParentUserId == parentUserId)
                .OrderByDescending(r => r.CreatedAt))
            .ToListAsync(ct);

        return ServiceResult<List<DraftResponseModel>>.SuccessResult(await MapRowsAsync(rows, ct));
    }

    public async Task<ServiceResult<List<DraftResponseModel>>> GetForInstanceAsync(int userId, int instanceId, DraftResponseStatus? status, CancellationToken ct = default)
    {
        var schoolStudentId = await _context.DocumentInstances.AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => (int?)i.SchoolStudentId)
            .FirstOrDefaultAsync(ct);
        if (schoolStudentId == null)
            return ServiceResult<List<DraftResponseModel>>.FailureResult(InstanceNotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId.Value, AccessRole.Viewer, ct))
            return ServiceResult<List<DraftResponseModel>>.FailureResult(PermissionMessage);

        var query = _context.DraftResponses.AsNoTracking().Where(r => r.SharedDraftRevision.DocumentInstanceId == instanceId);
        if (status != null)
            query = query.Where(r => r.Status == status.Value);

        var rows = await ProjectResponseRows(query.OrderByDescending(r => r.CreatedAt)).ToListAsync(ct);
        return ServiceResult<List<DraftResponseModel>>.SuccessResult(await MapRowsAsync(rows, ct));
    }

    public async Task<ServiceResult<DraftResponseModel>> ResolveAsync(int userId, int responseId, ResolveDraftResponseModel model, CancellationToken ct = default)
    {
        var staffReply = string.IsNullOrWhiteSpace(model.StaffReply) ? null : model.StaffReply.Trim();
        if (staffReply != null && staffReply.Length > MaxTextLength)
            return ServiceResult<DraftResponseModel>.FailureResult($"Reply must be {MaxTextLength} characters or fewer.");
        var resolvedInDraft = model.ResolvedInDraft ?? false;
        if (staffReply == null && !resolvedInDraft)
            return ServiceResult<DraftResponseModel>.FailureResult(ResolveRequiresInputMessage);

        var response = await _context.DraftResponses.FirstOrDefaultAsync(r => r.Id == responseId, ct);
        if (response == null)
            return ServiceResult<DraftResponseModel>.FailureResult(ResponseNotFoundMessage);

        var schoolStudentId = await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.Id == response.SharedDraftRevisionId)
            .Select(r => r.DocumentInstance.SchoolStudentId)
            .FirstOrDefaultAsync(ct);
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<DraftResponseModel>.FailureResult(PermissionMessage);

        response.Status = DraftResponseStatus.Resolved;
        response.StaffReply = staffReply;
        response.ResolvedInDraft = resolvedInDraft;
        response.ResolvedByUserId = userId;
        response.ResolvedAt = DateTime.UtcNow;
        response.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        await NotifyParentAsync(response, schoolStudentId, ct);

        return ServiceResult<DraftResponseModel>.SuccessResult(await MapAsync(response.Id, ct));
    }

    // ---------------------------------------------------------------- Notifications

    private async Task NotifyTeamAsync(RevisionHeader header, DraftResponse response, CancellationToken ct)
    {
        try
        {
            var teamUserIds = await _context.StudentTeamMembers.AsNoTracking()
                .Where(m => m.SchoolStudentId == header.SchoolStudentId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync(ct);
            var recipientIds = teamUserIds.Append(header.SharedByUserId).Distinct().ToList();
            if (recipientIds.Count == 0)
                return;

            var studentName = await _context.SchoolStudents.AsNoTracking()
                .Where(s => s.Id == header.SchoolStudentId)
                .Select(s => s.FirstName + " " + s.LastName)
                .FirstOrDefaultAsync(ct) ?? "the student";

            const string title = "New family response";
            var body = $"A family member responded ({response.Kind}) to the shared draft for {studentName.Trim()}.";
            var linkPath = $"/educator/documents/{header.DocumentInstanceId}?tab=converge";
            var dedupKey = $"draft-response-{response.Id}";
            await _notifications.NotifyAsync(recipientIds, NotificationKind.ResponseReceived, title, body, linkPath, dedupKey, emailImmediately: true, ct);
        }
        catch (Exception ex)
        {
            // Notification failure must never roll back or fail the response that already committed.
            _logger.LogError(ex, "Failed to queue ResponseReceived notifications for response {ResponseId}.", response.Id);
        }
    }

    private async Task NotifyParentAsync(DraftResponse response, int schoolStudentId, CancellationToken ct)
    {
        try
        {
            const string title = "Your response was resolved";
            var body = response.StaffReply != null
                ? $"The school team replied: {response.StaffReply}"
                : "The school team addressed this in the draft.";
            // The parent route is child-scoped; a student account (no child profile) gets the bare route.
            var childId = await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, response.ParentUserId, schoolStudentId, AccessRole.Viewer, ct);
            var linkPath = childId != null
                ? $"/children/{childId}/shared-drafts/{response.SharedDraftRevisionId}"
                : $"/shared-drafts/{response.SharedDraftRevisionId}";
            var dedupKey = $"draft-response-resolved-{response.Id}";
            await _notifications.NotifyAsync(new[] { response.ParentUserId }, NotificationKind.DraftResponseResolved, title, body, linkPath, dedupKey, emailImmediately: true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue DraftResponseResolved notification for response {ResponseId}.", response.Id);
        }
    }

    // ---------------------------------------------------------------- Loading + mapping

    private sealed record RevisionHeader(int SchoolStudentId, int DocumentInstanceId, SharedDraftStatus Status, int SharedByUserId);

    private async Task<RevisionHeader?> LoadRevisionHeaderAsync(int revisionId, CancellationToken ct) =>
        await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.Id == revisionId)
            .Select(r => new RevisionHeader(r.DocumentInstance.SchoolStudentId, r.DocumentInstanceId, r.Status, r.SharedByUserId))
            .FirstOrDefaultAsync(ct);

    private sealed record ResponseRow(
        int Id, int SharedDraftRevisionId, int DocumentInstanceId, int ParentUserId, string ParentName,
        Guid? TargetFieldKey, string? TargetRowId, DraftResponseKind Kind, string Text, DateTime CreatedAt,
        DraftResponseStatus Status, string? StaffReply, bool ResolvedInDraft, string? ResolvedByName, DateTime? ResolvedAt,
        int DocumentTemplateVersionId, string ValuesJson);

    /// <summary>
    /// Projects a raw, already-filtered <see cref="DraftResponse"/> query into <see cref="ResponseRow"/>.
    /// The filter MUST be applied to <paramref name="source"/> before calling this — EF Core cannot
    /// translate a further <c>.Where()</c> against a property of the projected record afterward.
    /// </summary>
    private IQueryable<ResponseRow> ProjectResponseRows(IQueryable<DraftResponse> source) =>
        source.Select(r => new ResponseRow(
            r.Id,
            r.SharedDraftRevisionId,
            r.SharedDraftRevision.DocumentInstanceId,
            r.ParentUserId,
            r.ParentUser.FirstName + " " + r.ParentUser.LastName,
            r.TargetFieldKey,
            r.TargetRowId,
            r.Kind,
            r.Text,
            r.CreatedAt,
            r.Status,
            r.StaffReply,
            r.ResolvedInDraft,
            r.ResolvedByUserId == null ? null : _context.Users.Where(u => u.Id == r.ResolvedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            r.ResolvedAt,
            r.SharedDraftRevision.DocumentTemplateVersionId,
            r.SharedDraftRevision.ValuesJson));

    private async Task<DraftResponseModel> MapAsync(int responseId, CancellationToken ct)
    {
        var row = await ProjectResponseRows(_context.DraftResponses.AsNoTracking().Where(r => r.Id == responseId)).FirstAsync(ct);
        var models = await MapRowsAsync(new List<ResponseRow> { row }, ct);
        return models[0];
    }

    /// <summary>Resolves each row's target label, caching the pinned schema per distinct template version
    /// so a page of responses spanning several revisions of the same instance loads it once.</summary>
    private async Task<List<DraftResponseModel>> MapRowsAsync(List<ResponseRow> rows, CancellationToken ct)
    {
        var models = new List<DraftResponseModel>();
        var sectionsByVersion = new Dictionary<int, List<TemplateSectionModel>>();

        foreach (var r in rows)
        {
            string? targetLabel = null;
            if (r.TargetFieldKey != null)
            {
                if (!sectionsByVersion.TryGetValue(r.DocumentTemplateVersionId, out var sections))
                {
                    sections = await TemplateSectionLoader.LoadAsync(_context, r.DocumentTemplateVersionId, ct);
                    sectionsByVersion[r.DocumentTemplateVersionId] = sections;
                }
                targetLabel = DraftRowLabeler.ResolveTargetLabel(sections, ValueDocumentJson.Parse(r.ValuesJson), r.TargetFieldKey.Value, r.TargetRowId);
            }

            models.Add(new DraftResponseModel
            {
                Id = r.Id,
                RevisionId = r.SharedDraftRevisionId,
                ParentUserId = r.ParentUserId,
                ParentName = r.ParentName.Trim(),
                TargetFieldKey = r.TargetFieldKey,
                TargetRowId = r.TargetRowId,
                TargetLabel = targetLabel,
                Kind = r.Kind,
                Text = r.Text,
                CreatedAt = r.CreatedAt,
                Status = r.Status,
                StaffReply = r.StaffReply,
                ResolvedInDraft = r.ResolvedInDraft,
                ResolvedByName = r.ResolvedByName?.Trim(),
                ResolvedAt = r.ResolvedAt
            });
        }

        return models;
    }
}
