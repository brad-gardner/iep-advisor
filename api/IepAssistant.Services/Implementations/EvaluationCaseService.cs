using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Evaluation case lifecycle: referral → consent → clock → determination → ETR handoff
/// (see <see cref="IEvaluationCaseService"/>, plan 7 decision 1).
///
/// <para>Multilingual plan (2026-10-06) phase 5: every failure <c>EvaluationCaseController</c> maps to
/// a status carries an explicit <see cref="ServiceErrorKind"/>, and every message is localized
/// (<c>Messages.resx</c>/<c>.es.resx</c>).</para>
/// </summary>
public class EvaluationCaseService : IEvaluationCaseService
{
    private const long MaxConsentFileBytes = 10 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IBlobStorageService _blob;
    private readonly INotificationService _notifications;
    private readonly IDocumentInstanceService _documentInstanceService;
    private readonly ILogger<EvaluationCaseService> _logger;
    private readonly IStringLocalizer<Notifications> _notificationsLocalizer;
    private readonly IStringLocalizer<Messages> _localizer;

    public EvaluationCaseService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IBlobStorageService blob,
        INotificationService notifications,
        IDocumentInstanceService documentInstanceService,
        ILogger<EvaluationCaseService> logger,
        IStringLocalizer<Notifications> notificationsLocalizer,
        IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _orgAccess = orgAccess;
        _blob = blob;
        _notifications = notifications;
        _documentInstanceService = documentInstanceService;
        _logger = logger;
        _notificationsLocalizer = notificationsLocalizer;
        _localizer = localizer;
    }

    // ---------------------------------------------------------------- Reads

    public async Task<ServiceResult<EvaluationCaseModel>> GetForStudentAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        var kase = await LoadCaseForReadAsync(schoolStudentId, ct);
        if (kase == null)
            return ServiceResult<EvaluationCaseModel>.SuccessResult(null!);

        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    // ---------------------------------------------------------------- Create

    public async Task<ServiceResult<EvaluationCaseModel>> CreateAsync(int userId, int schoolStudentId, CreateEvaluationCaseModel model, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        var hasOpen = await _context.EvaluationCases.AsNoTracking()
            .AnyAsync(c => c.SchoolStudentId == schoolStudentId && c.Status != EvaluationCaseStatus.Closed, ct);
        if (hasOpen)
            return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.AlreadyHasOpenCase"]);

        var kase = new EvaluationCase
        {
            SchoolStudentId = schoolStudentId,
            Kind = model.Kind,
            ReferralDate = model.ReferralDate.Date,
            ReferralSource = Trim(model.ReferralSource),
            Status = EvaluationCaseStatus.Open,
            CreatedByUserId = userId,
            CreatedById = userId,
            UpdatedById = userId
        };

        await _context.EvaluationCases.AddAsync(kase, ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsOneOpenCaseCollision(ex))
        {
            _context.Entry(kase).State = EntityState.Detached;
            return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.AlreadyHasOpenCase"]);
        }

        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    // ---------------------------------------------------------------- Consent

    public async Task<ServiceResult<EvaluationCaseModel>> RequestConsentAsync(int userId, int schoolStudentId, DateTime? requestedAt, CancellationToken ct = default)
    {
        var (kase, kind, error) = await LoadOpenCaseForWriteAsync(userId, schoolStudentId, ct);
        if (error != null) return ServiceResult<EvaluationCaseModel>.FailureResult(kind, error);

        kase!.ConsentRequestedAt = requestedAt ?? DateTime.UtcNow;
        if (kase.Status == EvaluationCaseStatus.Open)
            kase.Status = EvaluationCaseStatus.ConsentPending;
        kase.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    public async Task<ServiceResult<EvaluationCaseModel>> ReceiveConsentAsync(int userId, int schoolStudentId, ReceiveConsentModel model, CancellationToken ct = default)
    {
        var (kase, kind, error) = await LoadOpenCaseForWriteAsync(userId, schoolStudentId, ct);
        if (error != null) return ServiceResult<EvaluationCaseModel>.FailureResult(kind, error);

        if (model.FileStream != null)
        {
            if (model.ContentType != null && !string.Equals(model.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.ConsentMustBePdf"]);
            if (model.FileStream.CanSeek && model.FileStream.Length > MaxConsentFileBytes)
                return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.ConsentTooLarge"]);
            if (!await PdfUploadGuard.LooksLikePdfAsync(model.FileStream, ct))
                return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.ConsentMustBePdf"]);

            var blobPath = $"evaluations/{kase!.Id}/consent.pdf";
            await _blob.UploadAsync(blobPath, model.FileStream, "application/pdf", ct);
            kase.ConsentBlobPath = blobPath;
            kase.ConsentFileName = PdfUploadGuard.SafeFileName(model.FileName, "consent.pdf");
        }

        kase!.ConsentReceivedAt = model.ReceivedAt;
        kase.DeterminationDueDate = EvaluationCaseRules.ComputeDeterminationDueDate(model.ReceivedAt);
        kase.DueDateOverrideReason = null;
        if (kase.Status is EvaluationCaseStatus.Open or EvaluationCaseStatus.ConsentPending)
            kase.Status = EvaluationCaseStatus.InProgress;
        kase.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    public async Task<ServiceResult<string>> GetConsentDownloadUrlAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<string>.FailureResult(ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        var kase = await LoadCaseForReadAsync(schoolStudentId, ct);
        if (kase?.ConsentBlobPath == null)
            return ServiceResult<string>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.NoConsentOnFile"]);

        var url = await _blob.GetDownloadUrlAsync(kase.ConsentBlobPath);
        return ServiceResult<string>.SuccessResult(url);
    }

    public async Task<ServiceResult<EvaluationCaseModel>> OverrideDueDateAsync(int userId, int schoolStudentId, OverrideDueDateModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
            return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.ReasonRequiredForDueDate"]);

        var (kase, kind, error) = await LoadOpenCaseForWriteAsync(userId, schoolStudentId, ct);
        if (error != null) return ServiceResult<EvaluationCaseModel>.FailureResult(kind, error);

        kase!.DeterminationDueDate = model.DeterminationDueDate.Date;
        kase.DueDateOverrideReason = model.Reason.Trim();
        kase.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    // ---------------------------------------------------------------- Assignments

    public async Task<ServiceResult<EvaluatorAssignmentModel>> AddAssignmentAsync(int userId, int schoolStudentId, CreateEvaluatorAssignmentModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Domain))
            return ServiceResult<EvaluatorAssignmentModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.DomainRequired"]);

        var (kase, kind, error) = await LoadOpenCaseForWriteAsync(userId, schoolStudentId, ct);
        if (error != null) return ServiceResult<EvaluatorAssignmentModel>.FailureResult(kind, error);

        var evaluator = await _context.Users.AsNoTracking().Where(u => u.Id == model.UserId)
            .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
            .FirstOrDefaultAsync(ct);
        if (evaluator == null)
            return ServiceResult<EvaluatorAssignmentModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["EvaluationCases.EvaluatorNotFound"]);

        var assignment = new EvaluatorAssignment
        {
            EvaluationCaseId = kase!.Id,
            UserId = model.UserId,
            Domain = model.Domain.Trim(),
            DueDate = model.DueDate,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.EvaluatorAssignments.AddAsync(assignment, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<EvaluatorAssignmentModel>.SuccessResult(MapAssignment(assignment, evaluator.Name, DateTime.UtcNow.Date));
    }

    public async Task<ServiceResult<EvaluatorAssignmentModel>> UpdateAssignmentAsync(int userId, int assignmentId, UpdateEvaluatorAssignmentModel model, CancellationToken ct = default)
    {
        var assignment = await _context.EvaluatorAssignments
            .Include(a => a.EvaluationCase)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment == null)
            return ServiceResult<EvaluatorAssignmentModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["EvaluationCases.AssignmentNotFound"]);

        if (!await _orgAccess.CanActOnStudentAsync(userId, assignment.EvaluationCase.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<EvaluatorAssignmentModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        if (model.SubmittedAt.HasValue) assignment.SubmittedAt = model.SubmittedAt;
        if (model.Notes != null) assignment.Notes = model.Notes.Trim();
        if (model.DueDate.HasValue) assignment.DueDate = model.DueDate;
        assignment.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        var name = (assignment.User.FirstName + " " + assignment.User.LastName).Trim();
        return ServiceResult<EvaluatorAssignmentModel>.SuccessResult(MapAssignment(assignment, name, DateTime.UtcNow.Date));
    }

    public async Task<ServiceResult> RemoveAssignmentAsync(int userId, int assignmentId, CancellationToken ct = default)
    {
        var assignment = await _context.EvaluatorAssignments.Include(a => a.EvaluationCase).FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["EvaluationCases.AssignmentNotFound"]);

        if (!await _orgAccess.CanActOnStudentAsync(userId, assignment.EvaluationCase.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        _context.EvaluatorAssignments.Remove(assignment);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Determination / close

    public async Task<ServiceResult<EvaluationCaseModel>> DetermineAsync(int userId, int schoolStudentId, DetermineEvaluationModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Rationale))
            return ServiceResult<EvaluationCaseModel>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.RationaleRequired"]);

        var (kase, kind, error) = await LoadOpenCaseForWriteAsync(userId, schoolStudentId, ct);
        if (error != null) return ServiceResult<EvaluationCaseModel>.FailureResult(kind, error);

        kase!.EligibilityOutcome = model.Outcome;
        kase.DeterminationDate = model.DeterminationDate.Date;
        kase.DeterminationRationale = model.Rationale.Trim();
        kase.EtrAuthoredVersionId = model.EtrAuthoredVersionId;
        kase.UpdatedById = userId;

        if (model.Outcome == EligibilityOutcome.Eligible)
        {
            kase.Status = EvaluationCaseStatus.Determined;
        }
        else
        {
            kase.Status = EvaluationCaseStatus.Closed;
            kase.ClosedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    public async Task<ServiceResult<EvaluationCaseModel>> CloseAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        // Unlike the other write paths, Close is exactly how a Determined (Eligible) case is meant to
        // wrap up (e.g. after the IEP is created) — so it must NOT be blocked by the Determined guard
        // that protects consent/due-date/assignment mutations from happening on an already-decided case.
        var (kase, kind, error) = await LoadCaseForWriteAsync(userId, schoolStudentId, ct, allowDetermined: true);
        if (error != null) return ServiceResult<EvaluationCaseModel>.FailureResult(kind, error);

        kase!.Status = EvaluationCaseStatus.Closed;
        kase.ClosedAt = DateTime.UtcNow;
        kase.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<EvaluationCaseModel>.SuccessResult(await BuildModelAsync(kase, ct));
    }

    // ---------------------------------------------------------------- ETR handoff

    public async Task<ServiceResult<int>> CreateIepFromEtrAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<int>.FailureResult(ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        var iepTypeId = await _context.DocumentTypes.AsNoTracking()
            .Where(t => t.Key == "IEP").Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (iepTypeId == 0)
            return ServiceResult<int>.FailureResult(ServiceErrorKind.Validation, _localizer["EvaluationCases.IepTypeNotConfigured"]);

        var result = await _documentInstanceService.CreateAsync(schoolStudentId, iepTypeId, userId, ct);
        if (!result.Success)
            return ServiceResult<int>.FailureResult(result.ErrorKind, result.Message ?? _localizer["EvaluationCases.CouldNotCreateIepDraft"]);

        return ServiceResult<int>.SuccessResult(result.Data!.Id);
    }

    // ---------------------------------------------------------------- Notifications

    public async Task RunOverdueNotificationsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var overdue = await _context.EvaluatorAssignments.AsNoTracking()
            .Where(a => a.SubmittedAt == null && a.DueDate != null && a.DueDate.Value.Date < today
                        && a.EvaluationCase.Status != EvaluationCaseStatus.Closed
                        && a.EvaluationCase.Status != EvaluationCaseStatus.Determined)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.Domain,
                a.DueDate,
                StudentId = a.EvaluationCase.SchoolStudentId,
                a.EvaluationCase.CreatedByUserId,
                LeadUserId = a.EvaluationCase.SchoolStudent.CaseManagerUserId,
                StudentName = (a.EvaluationCase.SchoolStudent.FirstName + " " + a.EvaluationCase.SchoolStudent.LastName).Trim()
            })
            .ToListAsync(ct);

        foreach (var a in overdue)
        {
            var recipients = new HashSet<int> { a.UserId, a.LeadUserId ?? a.CreatedByUserId };
            var dedupKey = $"evaluator-overdue-{a.Id}-{today:yyyyMMdd}";
            // Multilingual plan phase 4 review: re-checked whether this should localize like
            // DraftResponseKind now does. EvaluatorAssignment.Domain is free text a staff member types
            // when creating the assignment (AddAssignmentAsync only validates non-empty; see
            // add-assignment-form.tsx's plain text input and demo seed values like "Cognitive ability and
            // observation") — not a closed C# enum, so there is no fixed value set to add resx keys for.
            // It stays in English for every recipient, same as other staff-authored free text in this
            // codebase (StaffInvite's org-role name, DraftResponse.StaffReply, Meeting.Title).
            var domain = a.Domain;
            var studentName = a.StudentName;
            var dueDate = a.DueDate!.Value;

            (string Title, string Body) BuildText(string lang) => (
                _notificationsLocalizer["Notifications.EvaluatorOverdue.Title", domain],
                _notificationsLocalizer["Notifications.EvaluatorOverdue.Body", domain, studentName, LocalizedDateFormat.ShortDate(dueDate, lang)]);

            try
            {
                await _notifications.NotifyAsync(recipients, NotificationKind.EvaluatorOverdue, BuildText,
                    $"/educator/students/{a.StudentId}", dedupKey, emailImmediately: true, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send EvaluatorOverdue notification for assignment {AssignmentId}.", a.Id);
            }
        }
    }

    // ---------------------------------------------------------------- Loading + mapping

    private async Task<EvaluationCase?> LoadCaseForReadAsync(int schoolStudentId, CancellationToken ct)
    {
        var open = await _context.EvaluationCases
            .Include(c => c.Assignments).ThenInclude(a => a.User)
            .Include(c => c.SchoolStudent).ThenInclude(s => s.CaseManager)
            .Where(c => c.SchoolStudentId == schoolStudentId && c.Status != EvaluationCaseStatus.Closed)
            .FirstOrDefaultAsync(ct);
        if (open != null) return open;

        return await _context.EvaluationCases
            .Include(c => c.Assignments).ThenInclude(a => a.User)
            .Include(c => c.SchoolStudent).ThenInclude(s => s.CaseManager)
            .Where(c => c.SchoolStudentId == schoolStudentId && c.Status == EvaluationCaseStatus.Closed)
            .OrderByDescending(c => c.ClosedAt ?? c.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Loads the student's OPEN case (Status != Closed, and not yet Determined unless
    /// <paramref name="allowDetermined"/>) for a write, after checking Collaborator+ access. Returns
    /// (null, None, null) on success, or (null, kind, errorMessage) on any failure.</summary>
    private async Task<(EvaluationCase? Case, ServiceErrorKind Kind, string? Error)> LoadCaseForWriteAsync(int userId, int schoolStudentId, CancellationToken ct, bool allowDetermined)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return (null, ServiceErrorKind.Forbidden, _localizer["EvaluationCases.Permission"]);

        var kase = await _context.EvaluationCases
            .Include(c => c.Assignments).ThenInclude(a => a.User)
            .Include(c => c.SchoolStudent).ThenInclude(s => s.CaseManager)
            .Where(c => c.SchoolStudentId == schoolStudentId && c.Status != EvaluationCaseStatus.Closed)
            .FirstOrDefaultAsync(ct);
        if (kase == null)
            return (null, ServiceErrorKind.Validation, _localizer["EvaluationCases.NoOpenCase"]);
        if (!allowDetermined && kase.Status == EvaluationCaseStatus.Determined)
            return (null, ServiceErrorKind.Validation, _localizer["EvaluationCases.CaseNoLongerOpen"]);

        return (kase, ServiceErrorKind.None, null);
    }

    /// <summary>Convenience overload for every write EXCEPT Close, which must also accept a Determined case.</summary>
    private Task<(EvaluationCase? Case, ServiceErrorKind Kind, string? Error)> LoadOpenCaseForWriteAsync(int userId, int schoolStudentId, CancellationToken ct)
        => LoadCaseForWriteAsync(userId, schoolStudentId, ct, allowDetermined: false);

    private async Task<EvaluationCaseModel> BuildModelAsync(EvaluationCase kase, CancellationToken ct)
    {
        var creatorName = await _context.Users.AsNoTracking().Where(u => u.Id == kase.CreatedByUserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(ct) ?? string.Empty;

        var today = DateTime.UtcNow.Date;
        var assignments = kase.Assignments
            .OrderBy(a => a.Domain)
            .Select(a => MapAssignment(a, (a.User.FirstName + " " + a.User.LastName).Trim(), today))
            .ToList();

        var timeline = new List<EvaluationTimelineEntryModel> { new() { At = kase.ReferralDate, Label = "Referral" } };
        if (kase.ConsentRequestedAt.HasValue) timeline.Add(new() { At = kase.ConsentRequestedAt.Value, Label = "Consent requested" });
        if (kase.ConsentReceivedAt.HasValue) timeline.Add(new() { At = kase.ConsentReceivedAt.Value, Label = "Consent received" });
        if (kase.DeterminationDueDate.HasValue) timeline.Add(new() { At = kase.DeterminationDueDate.Value, Label = "Determination due" });
        if (kase.DeterminationDate.HasValue) timeline.Add(new() { At = kase.DeterminationDate.Value, Label = $"Determined: {kase.EligibilityOutcome}" });
        if (kase.ClosedAt.HasValue) timeline.Add(new() { At = kase.ClosedAt.Value, Label = "Closed" });

        // Owner = the lead case manager, else the case creator (contract: "owner = case creator / lead").
        var ownerUserId = kase.SchoolStudent?.CaseManagerUserId ?? kase.CreatedByUserId;
        var ownerName = kase.SchoolStudent?.CaseManager != null
            ? (kase.SchoolStudent.CaseManager.FirstName + " " + kase.SchoolStudent.CaseManager.LastName).Trim()
            : creatorName;
        var obligation = BuildDeterminationObligation(kase, today, ownerUserId, ownerName);

        return new EvaluationCaseModel
        {
            Id = kase.Id,
            SchoolStudentId = kase.SchoolStudentId,
            Kind = kase.Kind,
            Status = kase.Status,
            ReferralDate = kase.ReferralDate,
            ReferralSource = kase.ReferralSource,
            ConsentRequestedAt = kase.ConsentRequestedAt,
            ConsentReceivedAt = kase.ConsentReceivedAt,
            HasConsentDocument = !string.IsNullOrEmpty(kase.ConsentBlobPath),
            ConsentFileName = kase.ConsentFileName,
            DeterminationDueDate = kase.DeterminationDueDate,
            DueDateOverrideReason = kase.DueDateOverrideReason,
            EligibilityOutcome = kase.EligibilityOutcome,
            DeterminationDate = kase.DeterminationDate,
            DeterminationRationale = kase.DeterminationRationale,
            EtrAuthoredVersionId = kase.EtrAuthoredVersionId,
            ClosedAt = kase.ClosedAt,
            CreatedByName = creatorName,
            Assignments = assignments,
            Timeline = timeline.OrderBy(t => t.At).ToList(),
            Obligation = obligation
        };
    }

    /// <summary>Mirrors <c>ObligationService</c>'s EvaluationDetermination row for the case's OWN detail
    /// view (the batched cross-student version lives in <c>ObligationService</c> itself).</summary>
    internal static ObligationModel? BuildDeterminationObligation(EvaluationCase kase, DateTime today, int? ownerUserId, string? ownerName)
    {
        if (kase.Status is EvaluationCaseStatus.Determined or EvaluationCaseStatus.Closed)
            return null;

        return new ObligationModel
        {
            Kind = ObligationKind.EvaluationDetermination,
            DueDate = kase.DeterminationDueDate,
            Status = ObligationRules.ResolveStatus(kase.DeterminationDueDate, today),
            SourceLabel = kase.DueDateOverrideReason ?? "Consent received + 60 calendar days",
            OwnerUserId = ownerUserId,
            OwnerName = ownerName,
            SchoolStudentId = kase.SchoolStudentId,
            DaysUntilDue = ObligationRules.DaysUntilDue(kase.DeterminationDueDate, today),
            RuleProfile = ObligationRules.DefaultProfile
        };
    }

    private static EvaluatorAssignmentModel MapAssignment(EvaluatorAssignment a, string displayName, DateTime today) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        DisplayName = displayName,
        Domain = a.Domain,
        DueDate = a.DueDate,
        SubmittedAt = a.SubmittedAt,
        Notes = a.Notes,
        IsOverdue = a.SubmittedAt == null && a.DueDate.HasValue && a.DueDate.Value.Date < today
    };

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>True when the failure is genuinely the one-open-case-per-student filtered unique index
    /// rejecting a concurrent create (mirrors <c>MeetingReminderService.IsReminderUniqueIndexCollision</c>).</summary>
    private static bool IsOneOpenCaseCollision(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_EvaluationCases_OneOpenPerStudent", StringComparison.OrdinalIgnoreCase);
    }
}
