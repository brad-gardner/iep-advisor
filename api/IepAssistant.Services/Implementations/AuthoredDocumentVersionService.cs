using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>
/// Finalize a Draft <see cref="DocumentInstance"/> into an immutable <see cref="AuthoredDocumentVersion"/>
/// snapshot plus version reads (State Document Template Engine, Phase 4). The dynamic-template equivalent
/// of <see cref="IepVersionService"/>.
///
/// <para><b>Concurrency strategy (serializable tx):</b> finalize runs inside a
/// <see cref="IsolationLevel.Serializable"/> transaction. Within it we flip the instance to
/// <see cref="DocumentInstanceStatus.Finalizing"/> (which the DocumentInstanceService edit-freeze honors),
/// validate + snapshot the values, and insert the version. The unique index on
/// <c>(SchoolStudentId, DocumentTypeId, VersionNumber)</c> is the DB backstop — a concurrent finalize that
/// slips past serialization fails with a unique violation, which we translate to a friendly retry. The
/// instance returns to <see cref="DocumentInstanceStatus.Draft"/> afterward so it stays re-finalizable
/// (mirroring the IepDraft flow).</para>
/// </summary>
public class AuthoredDocumentVersionService : IAuthoredDocumentVersionService
{
    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly ITemplateAuthoringService _authoring;
    private readonly IBlobStorageService _blob;
    private readonly IAuditLogger _audit;
    private readonly IGoalRecordService _goalRecords;
    private readonly ILogger<AuthoredDocumentVersionService> _logger;
    private readonly IStringLocalizer<Messages> _localizer;

    public AuthoredDocumentVersionService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IAccessService accessService,
        ITemplateAuthoringService authoring,
        IBlobStorageService blob,
        IAuditLogger audit,
        IGoalRecordService goalRecords,
        ILogger<AuthoredDocumentVersionService> logger,
        IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _authoring = authoring;
        _blob = blob;
        _audit = audit;
        _goalRecords = goalRecords;
        _logger = logger;
        _localizer = localizer;
    }

    private LocalizedString PermissionMessage => _localizer["Documents.Permission"];
    private LocalizedString VersionPermissionMessage => _localizer["AuthoredDocuments.VersionPermission"];
    private LocalizedString InstanceNotFoundMessage => _localizer["Documents.NotFound"];
    private LocalizedString VersionNotFoundMessage => _localizer["AuthoredDocuments.VersionNotFound"];
    private LocalizedString AlreadyFinalizingMessage => _localizer["AuthoredDocuments.AlreadyFinalizing"];
    private LocalizedString NotDraftMessage => _localizer["AuthoredDocuments.NotDraft"];
    private LocalizedString RaceMessage => _localizer["AuthoredDocuments.FinalizeRace"];
    private LocalizedString ValidationSummaryMessage => _localizer["AuthoredDocuments.ValidationSummary"];

    // ---------------------------------------------------------------- Finalize

    public async Task<ServiceResult<AuthoredDocumentVersionSummaryModel>> FinalizeAsync(
        int instanceId, int actingUserId, CancellationToken ct = default)
    {
        // 1. Collaborator+ access on the instance's student.
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(ServiceErrorKind.NotFound, InstanceNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(ServiceErrorKind.Forbidden, PermissionMessage);

        AuthoredDocumentVersionSummaryModel summary;

        // 2. Serializable transaction — atomic validate + snapshot capture.
        // `transactionSettled` tracks whether the transaction has already been committed or rolled
        // back, so the outer catch never rolls back a completed transaction a second time (a double
        // RollbackAsync throws "This SqlTransaction has completed." and would mask the real fault).
        var transactionSettled = false;
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // 3. Re-read the instance inside the transaction.
            var instance = await _context.DocumentInstances.FirstOrDefaultAsync(i => i.Id == instanceId, ct);
            if (instance == null)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(ServiceErrorKind.NotFound, InstanceNotFoundMessage);
            }

            if (instance.Status == DocumentInstanceStatus.Finalizing)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(ServiceErrorKind.Conflict, AlreadyFinalizingMessage);
            }

            if (instance.Status != DocumentInstanceStatus.Draft)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(ServiceErrorKind.Conflict, NotDraftMessage);
            }

            // 4. Load the pinned template version tree and VALIDATE the value-document against it.
            var tree = await _authoring.GetVersionAsync(instance.DocumentTemplateVersionId, ct);
            if (!tree.Success)
            {
                await transaction.RollbackAsync(ct);
                // Propagate the inner failure's ErrorKind rather than the bare-message overload (which
                // defaults to None) — multilingual plan 2026-10-06 phase 3 review fix.
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(
                    tree.ErrorKind, tree.Message ?? _localizer["AuthoredDocuments.TemplateVersionLoadFailed"].Value);
            }

            var errors = ValidateAgainstSchema(tree.Data!, instance.ValuesJson);
            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(ct);
                return new ServiceResult<AuthoredDocumentVersionSummaryModel>
                {
                    Success = false,
                    Message = ValidationSummaryMessage,
                    Errors = errors
                };
            }

            // 5. Freeze the instance (blocks concurrent edits via the DocumentInstanceService edit-freeze).
            instance.Status = DocumentInstanceStatus.Finalizing;
            await _context.SaveChangesAsync(ct);

            // 6. VersionNumber = max for this (student, docType) + 1.
            var maxVersion = await _context.AuthoredDocumentVersions
                .Where(v => v.SchoolStudentId == instance.SchoolStudentId && v.DocumentTypeId == instance.DocumentTypeId)
                .Select(v => (int?)v.VersionNumber)
                .MaxAsync(ct);
            var versionNumber = (maxVersion ?? 0) + 1;

            var now = DateTime.UtcNow;

            // 6b. Plan 2026-10-02-002 review pass 2: a row's `_ownerUserId` is only re-validated against
            // active team membership on a live SAVE (DocumentInstanceService.CoerceTable) or, previously,
            // for the Goals table specifically at finalize (GoalRecordService.ProjectOnFinalizeAsync). An
            // owner who left the team between that save and this finalize — or whose stale owner rode in
            // verbatim via AmendAsync's ValuesJson copy — would otherwise freeze into the immutable version
            // (and from there into the PDF) on every owner-eligible table, not just Goals. Strip it from
            // the SNAPSHOT here, once, using the exact same active-membership query GoalRecordService uses
            // below, so the version, the PDF and the projected GoalRecord can never disagree. The DRAFT
            // instance's own ValuesJson is deliberately left untouched: it returns to Draft at step 8 and
            // stays editable, and the same stale owner is cleaned the next time that row is saved (CoerceTable)
            // or this document is finalized again — mirroring how a non-Goals owner was already handled
            // before this fix (never retroactively rewritten outside a save/finalize).
            var activeTeamUserIds = await OwnerEligibleRowSanitizer.LoadActiveTeamUserIdsAsync(_context, instance.SchoolStudentId, ct);
            var sanitizedValues = OwnerEligibleRowSanitizer.StripInactiveOwners(
                ValueDocumentJson.Parse(instance.ValuesJson), tree.Data!.Sections, activeTeamUserIds);

            // 7. Create the immutable version, snapshotting ValuesJson + the pinned template version id.
            var version = new AuthoredDocumentVersion
            {
                SchoolStudentId = instance.SchoolStudentId,
                DocumentTypeId = instance.DocumentTypeId,
                DocumentTemplateVersionId = instance.DocumentTemplateVersionId,
                VersionNumber = versionNumber,
                ValuesJson = sanitizedValues.ToJsonString(),
                FinalizedByUserId = actingUserId,
                FinalizedAt = now,
                // Plan 7, decision 5: an instance created by AmendAsync carries its amendment fields onto
                // every version finalized from it, forming the amendment chain. Set here (not as a later
                // mutation) — AuthoredDocumentVersion is immutable once inserted, so these must land in
                // the SAME insert as everything else.
                AmendsVersionId = instance.AmendsVersionId,
                AmendmentReason = instance.AmendmentReason,
                EffectiveDate = instance.EffectiveDate,
                CreatedById = actingUserId,
                UpdatedById = actingUserId,
                // The render worker flips this Pending -> Rendered/Error. Explicitly English (multilingual
                // plan phase 7): finalize always renders the default/English PDF first; any other
                // language's row is created on demand by GetPdfStatusAsync the first time it's requested.
                Pdfs = new List<AuthoredDocumentPdf>
                {
                    new()
                    {
                        Language = SupportedLanguages.English,
                        RenderStatus = PdfRenderStatus.Pending,
                        CreatedById = actingUserId,
                        UpdatedById = actingUserId
                    }
                }
            };

            try
            {
                await _context.AuthoredDocumentVersions.AddAsync(version, ct);
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);
                transactionSettled = true;

                // The expected contention outcome is the unique (student, docType, VersionNumber) index
                // rejecting the loser of a concurrent finalize. Confirm precisely — our computed number now
                // exists because someone else took it — so a persistent fault (FK/check/etc.) is never
                // masked behind a "try again" the caller can't recover from. (Rolled back first so this
                // read sees committed data outside the aborted transaction.)
                var lostTheNumberRace = await _context.AuthoredDocumentVersions
                    .AsNoTracking()
                    .AnyAsync(v => v.SchoolStudentId == instance.SchoolStudentId
                                   && v.DocumentTypeId == instance.DocumentTypeId
                                   && v.VersionNumber == versionNumber, ct);

                if (lostTheNumberRace)
                {
                    _logger.LogWarning(ex,
                        "Concurrent finalize race on instance {InstanceId} (version number {VersionNumber} taken); caller asked to retry.",
                        instanceId, versionNumber);
                    return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(ServiceErrorKind.Conflict, RaceMessage);
                }

                _logger.LogError(ex, "Finalize failed persisting AuthoredDocumentVersion for instance {InstanceId}.", instanceId);
                throw;
            }

            // 7b. Plan 7 phase 1: project the Goals table (if any) into first-class GoalRecord rows,
            //     carrying forward/retiring the prior version's lineage — same transaction as the snapshot.
            await _goalRecords.ProjectOnFinalizeAsync(instance, version, ct);

            // 8. Instance returns to Draft so it stays editable; re-finalize creates the next version.
            instance.Status = DocumentInstanceStatus.Draft;
            await _context.SaveChangesAsync(ct);

            // 9. Commit.
            await transaction.CommitAsync(ct);
            transactionSettled = true;

            int? amendsVersionNumber = version.AmendsVersionId.HasValue
                ? await _context.AuthoredDocumentVersions.AsNoTracking()
                    .Where(v => v.Id == version.AmendsVersionId.Value)
                    .Select(v => (int?)v.VersionNumber)
                    .FirstOrDefaultAsync(ct)
                : null;

            summary = new AuthoredDocumentVersionSummaryModel
            {
                Id = version.Id,
                SchoolStudentId = version.SchoolStudentId,
                DocumentTypeId = version.DocumentTypeId,
                VersionNumber = version.VersionNumber,
                FinalizedByUserId = actingUserId,
                FinalizedAt = version.FinalizedAt,
                PdfRenderStatus = PdfRenderStatus.Pending,
                SignatureStatus = version.SignatureStatus,
                AmendsVersionId = version.AmendsVersionId,
                AmendsVersionNumber = amendsVersionNumber,
                AmendmentReason = version.AmendmentReason,
                EffectiveDate = version.EffectiveDate
            };
        }
        catch
        {
            // Only roll back if the transaction hasn't already been settled (the inner DbUpdateException
            // handler rolls back + rethrows; rolling back again here would throw and mask the real fault).
            if (!transactionSettled)
                await transaction.RollbackAsync(ct);
            throw;
        }

        // FERPA audit: record the finalize against the newly-created version, after commit.
        _audit.Record(AuditAction.Finalize, actingUserId, "AuthoredDocumentVersion", summary.Id);
        _logger.LogInformation(
            "User {UserId} finalized document instance {InstanceId} into AuthoredDocumentVersion {VersionId} (v{VersionNumber}).",
            actingUserId, instanceId, summary.Id, summary.VersionNumber);

        // 10. The PDF render is enqueued by the controller AFTER this commit (failure-isolated, outside
        //     the transaction). The AuthoredDocumentPdf row is created Pending above.
        return ServiceResult<AuthoredDocumentVersionSummaryModel>.SuccessResult(summary);
    }

    // ---------------------------------------------------------------- Reads

    public async Task<ServiceResult<List<AuthoredDocumentVersionSummaryModel>>> ListVersionsForStudentAsync(
        int studentId, int actingUserId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.FailureResult(ServiceErrorKind.Forbidden, PermissionMessage);

        var rows = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.SchoolStudentId == studentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(SummaryProjection())
            .ToListAsync(ct);

        return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<List<AuthoredDocumentVersionSummaryModel>>> ListForChildAsync(
        int childId, int actingUserId, CancellationToken ct = default)
    {
        // Parent must have AccessService access to the child...
        if (!await _accessService.HasMinimumRoleAsync(childId, actingUserId, AccessRole.Viewer, ct))
            return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.FailureResult(ServiceErrorKind.Forbidden, PermissionMessage);

        // ...and the version's SchoolStudent must be linked via an active accepted ChildLink.
        var linkedStudentIds = _context.ChildLinks
            .Where(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId);

        var rows = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => linkedStudentIds.Contains(v.SchoolStudentId))
            .OrderByDescending(v => v.VersionNumber)
            .Select(SummaryProjection())
            .ToListAsync(ct);

        return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<AuthoredDocumentVersionDetailModel>> GetVersionAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(ServiceErrorKind.NotFound, VersionNotFoundMessage);

        // Staff (org Viewer+) and a linked parent/student share this same read, but NOT the same
        // ValuesJson: only staff may see a raw `_ownerUserId` on a goals/services/accommodations/
        // transition row. A parent/student reader is redacted to role-only, never the person's name —
        // same rule DraftSharingService.GetForParentAsync applies to a shared draft (plan 2026-10-02-002,
        // design "Resolved Questions" #1). Tracked separately from CanReadStudentAsync (still used by the
        // PDF status/download reads below, which never touch ValuesJson and so need no redaction branch).
        var isStaffAccess = await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Viewer, ct);
        if (!isStaffAccess && !await ParentCanViewStudentAsync(actingUserId, header.SchoolStudentId, ct))
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(ServiceErrorKind.Forbidden, VersionPermissionMessage);

        var version = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new
            {
                v.Id,
                v.SchoolStudentId,
                v.DocumentTypeId,
                DocumentTypeKey = v.DocumentType.Key,
                DocumentTypeDisplayName = v.DocumentType.DisplayName,
                v.DocumentTemplateVersionId,
                v.VersionNumber,
                v.FinalizedByUserId,
                v.FinalizedAt,
                v.ValuesJson,
                // Multilingual plan phase 7: reflects the ENGLISH row specifically (Pdf became Pdfs, one
                // row per language) — the same "the PDF's status" this DTO carried before this phase.
                PdfRenderStatus = v.Pdfs.Where(p => p.Language == null || p.Language == SupportedLanguages.English)
                    .Select(p => (PdfRenderStatus?)p.RenderStatus).FirstOrDefault(),
                PdfBlobUri = v.Pdfs.Where(p => p.Language == null || p.Language == SupportedLanguages.English)
                    .Select(p => p.BlobUri).FirstOrDefault(),
                PdfRenderedAt = v.Pdfs.Where(p => p.Language == null || p.Language == SupportedLanguages.English)
                    .Select(p => p.RenderedAt).FirstOrDefault(),
                v.SignatureStatus,
                SignedArtifactCount = v.SignedArtifacts.Count,
                v.AmendsVersionId,
                AmendsVersionNumber = v.AmendsVersion != null ? (int?)v.AmendsVersion.VersionNumber : null,
                v.AmendmentReason,
                v.EffectiveDate
            })
            .FirstOrDefaultAsync(ct);

        if (version == null)
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(ServiceErrorKind.NotFound, VersionNotFoundMessage);

        // Reuse the Phase 2 tree builder for the pinned version's section/field schema.
        var tree = await _authoring.GetVersionAsync(version.DocumentTemplateVersionId, ct);
        if (!tree.Success)
            // Propagate the inner failure's ErrorKind rather than the bare-message overload (which
            // defaults to None) — multilingual plan 2026-10-06 phase 3 review fix.
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(
                tree.ErrorKind, tree.Message ?? _localizer["AuthoredDocuments.TemplateVersionLoadFailed"].Value);

        var valuesJson = version.ValuesJson;
        if (!isStaffAccess)
        {
            var roleByUserId = await TeamRoleResolver.LoadRoleByUserIdAsync(_context, version.SchoolStudentId, ct);
            valuesJson = FamilyFacingValueRedactor
                .Redact(ValueDocumentJson.Parse(version.ValuesJson), tree.Data!.Sections, roleByUserId)
                .ToJsonString();
        }

        var amendedByVersionIds = await _context.AuthoredDocumentVersions.AsNoTracking()
            .Where(v => v.AmendsVersionId == versionId)
            .Select(v => v.Id)
            .ToListAsync(ct);

        _audit.Record(AuditAction.View, actingUserId, "AuthoredDocumentVersion", versionId);

        return ServiceResult<AuthoredDocumentVersionDetailModel>.SuccessResult(new AuthoredDocumentVersionDetailModel
        {
            Id = version.Id,
            SchoolStudentId = version.SchoolStudentId,
            DocumentTypeId = version.DocumentTypeId,
            DocumentTypeKey = version.DocumentTypeKey,
            DocumentTypeDisplayName = version.DocumentTypeDisplayName,
            DocumentTemplateVersionId = version.DocumentTemplateVersionId,
            VersionNumber = version.VersionNumber,
            FinalizedByUserId = version.FinalizedByUserId,
            FinalizedAt = version.FinalizedAt,
            ValuesJson = valuesJson,
            PdfRenderStatus = version.PdfRenderStatus,
            PdfBlobUri = version.PdfBlobUri,
            PdfRenderedAt = version.PdfRenderedAt,
            SignatureStatus = version.SignatureStatus,
            SignedArtifactCount = version.SignedArtifactCount,
            AmendsVersionId = version.AmendsVersionId,
            AmendsVersionNumber = version.AmendsVersionNumber,
            AmendmentReason = version.AmendmentReason,
            EffectiveDate = version.EffectiveDate,
            AmendedByVersionIds = amendedByVersionIds,
            TemplateVersion = tree.Data!
        });
    }

    // ---------------------------------------------------------------- Amend (plan 7, decision 5)

    public async Task<ServiceResult<AmendResultModel>> AmendAsync(int versionId, int actingUserId, AmendDocumentVersionModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Reason))
            return ServiceResult<AmendResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["AuthoredDocuments.AmendmentReasonRequired"]);
        var reason = model.Reason.Trim();
        if (reason.Length > 1000)
            return ServiceResult<AmendResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["AuthoredDocuments.AmendmentReasonTooLong"]);

        var version = await _context.AuthoredDocumentVersions.AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new { v.Id, v.SchoolStudentId, v.DocumentTypeId, v.DocumentTemplateVersionId, v.ValuesJson })
            .FirstOrDefaultAsync(ct);
        if (version == null)
            return ServiceResult<AmendResultModel>.FailureResult(ServiceErrorKind.NotFound, VersionNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, version.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<AmendResultModel>.FailureResult(ServiceErrorKind.Forbidden, PermissionMessage);

        // Prefilled VERBATIM — the frozen ValuesJson is copied as-is, so every `_rowId` (goal/service/
        // accommodation lineage) is preserved exactly as it was at finalize time. This does NOT re-check
        // a row's `_ownerUserId` against current active team membership (the copy never goes through
        // DocumentInstanceService.CoerceTable) — a stale/departed owner rides along into the new draft
        // until either an edit to that table re-validates it (a normal save does) or this draft is
        // finalized again, at which point AuthoredDocumentVersionService.FinalizeAsync strips it from the
        // snapshot (OwnerEligibleRowSanitizer, every owner-eligible table — not just Goals).
        var now = DateTime.UtcNow;
        var instance = new DocumentInstance
        {
            SchoolStudentId = version.SchoolStudentId,
            DocumentTypeId = version.DocumentTypeId,
            DocumentTemplateVersionId = version.DocumentTemplateVersionId,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = version.ValuesJson,
            // Same as DocumentInstanceService.CreateAsync: a live concurrency token from the first read,
            // so two staff opening the fresh amendment cannot silently overwrite each other.
            RowVersion = Guid.NewGuid().ToByteArray(),
            AmendsVersionId = version.Id,
            AmendmentReason = reason,
            EffectiveDate = model.EffectiveDate,
            LastEditedByUserId = actingUserId,
            LastEditedAt = now,
            CreatedById = actingUserId,
            UpdatedById = actingUserId
        };
        await _context.DocumentInstances.AddAsync(instance, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, actingUserId, "DocumentInstance", instance.Id);

        return ServiceResult<AmendResultModel>.SuccessResult(new AmendResultModel { InstanceId = instance.Id });
    }

    // ---------------------------------------------------------------- PDF status + retry

    public async Task<ServiceResult<AuthoredDocumentPdfStatusModel>> GetPdfStatusAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<AuthoredDocumentPdfStatusModel>.FailureResult(ServiceErrorKind.NotFound, VersionNotFoundMessage);

        if (!await CanReadStudentAsync(actingUserId, header.SchoolStudentId, ct))
            return ServiceResult<AuthoredDocumentPdfStatusModel>.FailureResult(ServiceErrorKind.Forbidden, VersionPermissionMessage);

        // Multilingual plan phase 7: the requesting user's UI language (download endpoints run in-request,
        // so CurrentUICulture already reflects their saved preference/Accept-Language). English keeps the
        // pre-phase-7 single-row behavior; any other language's row is created HERE, Pending, the first
        // time it's polled — NeedsRender tells the controller to enqueue the render (after this commit,
        // same after-commit/isolated convention as Finalize/Retry), so a render is kicked off exactly once
        // per (version, language). This is the one place this method is no longer side-effect-free on a
        // first poll for a non-English language — still no SAS/audit (those stay in GetPdfDownloadUrlAsync).
        var language = SupportedLanguages.CurrentUiLanguage();
        var pdf = await _context.AuthoredDocumentPdfs.FirstOrDefaultAsync(
            p => p.AuthoredDocumentVersionId == versionId && (p.Language == language
                || (language == SupportedLanguages.English && p.Language == null)), ct);

        var needsRender = false;
        if (pdf == null)
        {
            pdf = new AuthoredDocumentPdf
            {
                AuthoredDocumentVersionId = versionId,
                Language = language,
                RenderStatus = PdfRenderStatus.Pending,
                CreatedById = actingUserId,
                UpdatedById = actingUserId
            };
            await _context.AuthoredDocumentPdfs.AddAsync(pdf, ct);
            await _context.SaveChangesAsync(ct);
            needsRender = true;
        }

        var model = new AuthoredDocumentPdfStatusModel
        {
            VersionId = versionId,
            Language = language,
            NeedsRender = needsRender,
            RenderStatus = pdf.RenderStatus,
            RenderedAt = pdf.RenderedAt,
            ErrorMessage = pdf.ErrorMessage
        };

        return ServiceResult<AuthoredDocumentPdfStatusModel>.SuccessResult(model);
    }

    public async Task<ServiceResult<string>> GetPdfDownloadUrlAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<string>.FailureResult(ServiceErrorKind.NotFound, VersionNotFoundMessage);

        if (!await CanReadStudentAsync(actingUserId, header.SchoolStudentId, ct))
            return ServiceResult<string>.FailureResult(ServiceErrorKind.Forbidden, VersionPermissionMessage);

        // Multilingual plan phase 7: the same resolved language GetPdfStatusAsync used to poll/create the
        // row — the download only succeeds once THAT language's row is Rendered.
        var language = SupportedLanguages.CurrentUiLanguage();
        var renderStatus = await _context.AuthoredDocumentPdfs
            .AsNoTracking()
            .Where(p => p.AuthoredDocumentVersionId == versionId && (p.Language == language
                || (language == SupportedLanguages.English && p.Language == null)))
            .Select(p => (PdfRenderStatus?)p.RenderStatus)
            .FirstOrDefaultAsync(ct);

        if (renderStatus != PdfRenderStatus.Rendered)
            return ServiceResult<string>.FailureResult(ServiceErrorKind.Validation, _localizer["AuthoredDocuments.PdfNotAvailableYet"]);

        // Mint a short-lived download URL from the deterministic blob path (SAS when supported).
        var blobPath = IAuthoredDocumentPdfService.BlobPathFor(versionId, header.VersionNumber, language);
        var url = await _blob.GetDownloadUrlAsync(blobPath);

        // FERPA audit: this is an actual export of the finalized document (unlike a status poll).
        _audit.Record(AuditAction.Export, actingUserId, "AuthoredDocumentVersion", versionId);

        return ServiceResult<string>.SuccessResult(url);
    }

    public async Task<ServiceResult<int>> RequestPdfRetryAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<int>.FailureResult(ServiceErrorKind.NotFound, VersionNotFoundMessage);

        // Retry is an authoring action — Collaborator+ educator on the student's school.
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<int>.FailureResult(ServiceErrorKind.Forbidden, PermissionMessage);

        // Multilingual plan phase 7: retries the row for the CALLER's current language (the controller
        // resolves the same ambient CurrentUICulture independently to enqueue the matching render), never
        // "the" row, since a version can have one per language.
        var language = SupportedLanguages.CurrentUiLanguage();
        var pdf = await _context.AuthoredDocumentPdfs.FirstOrDefaultAsync(
            p => p.AuthoredDocumentVersionId == versionId && (p.Language == language
                || (language == SupportedLanguages.English && p.Language == null)), ct);
        if (pdf == null)
            return ServiceResult<int>.FailureResult(ServiceErrorKind.Validation, _localizer["Pdf.NoRecordToRetry"]);

        if (pdf.RenderStatus == PdfRenderStatus.Rendered)
            return ServiceResult<int>.FailureResult(ServiceErrorKind.Validation, _localizer["Pdf.AlreadyRendered"]);

        // Error or Pending -> set Pending so the UI shows "generating" until the worker re-renders.
        pdf.RenderStatus = PdfRenderStatus.Pending;
        pdf.ErrorMessage = null;
        pdf.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<int>.SuccessResult(versionId);
    }

    // ---------------------------------------------------------------- Schema validation (finalize)

    /// <summary>
    /// Validates the frozen value-document against the pinned template schema, returning a COMPLETE list
    /// of friendly errors (empty when valid). Each message identifies section + field label (+ row index
    /// for table cells). Walks sections/fields in display order for stable, deterministic error ordering.
    /// </summary>
    public static List<string> ValidateAgainstSchema(TemplateVersionDetailModel tree, string? valuesJson)
    {
        var errors = new List<string>();

        JsonObject values;
        try
        {
            values = (string.IsNullOrWhiteSpace(valuesJson)
                ? new JsonObject()
                : JsonNode.Parse(valuesJson) as JsonObject) ?? new JsonObject();
        }
        catch (JsonException)
        {
            values = new JsonObject();
        }

        foreach (var section in tree.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            foreach (var field in section.Fields.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id))
            {
                values.TryGetPropertyValue(field.FieldKey.ToString(), out var node);
                ValidateField(section.Title, field, node, errors);
            }
        }

        return errors;
    }

    private static void ValidateField(string sectionTitle, TemplateFieldModel field, JsonNode? node, List<string> errors)
    {
        switch (field.FieldType)
        {
            case FieldType.Table:
                ValidateTable(sectionTitle, field, node, errors);
                break;

            case FieldType.Select:
            {
                var value = AsString(node);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (field.Required) errors.Add(RequiredError(sectionTitle, field.Label));
                }
                else if (!ParseSelectValues(field.ConfigJson).Contains(value))
                {
                    errors.Add(FieldError(sectionTitle, field.Label, $"\"{value}\" is not a valid option."));
                }
                break;
            }

            case FieldType.Date:
            {
                var value = AsString(node);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (field.Required) errors.Add(RequiredError(sectionTitle, field.Label));
                }
                else if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    errors.Add(FieldError(sectionTitle, field.Label, "Enter a valid date."));
                }
                break;
            }

            case FieldType.Checkbox:
                // A required checkbox must be checked (the "I certify …" pattern). An unchecked/absent
                // required checkbox is a validation error; optional checkboxes are always satisfied.
                if (field.Required && AsBool(node) != true)
                    errors.Add(FieldError(sectionTitle, field.Label, "This must be checked."));
                break;

            default: // Text, RichText
                if (field.Required && string.IsNullOrWhiteSpace(AsString(node)))
                    errors.Add(RequiredError(sectionTitle, field.Label));
                break;
        }
    }

    private static void ValidateTable(string sectionTitle, TemplateFieldModel field, JsonNode? node, List<string> errors)
    {
        var (columns, minRows, maxRows) = ParseTableConfig(field.ConfigJson);
        var rows = node as JsonArray;
        var rowCount = rows?.Count ?? 0;

        if (minRows is int min && rowCount < min)
            errors.Add(FieldError(sectionTitle, field.Label, $"At least {min} row(s) are required."));
        else if (field.Required && (minRows is null or 0) && rowCount == 0)
            errors.Add(RequiredError(sectionTitle, field.Label));

        if (maxRows is int max && rowCount > max)
            errors.Add(FieldError(sectionTitle, field.Label, $"No more than {max} row(s) are allowed."));

        if (rows == null)
            return;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] as JsonObject;
            var rowNum = i + 1; // 1-based for humans

            foreach (var col in columns)
            {
                JsonNode? cell = null;
                row?.TryGetPropertyValue(col.ColumnKey.ToString(), out cell);

                if (IsCellEmpty(col.Type, cell))
                {
                    if (col.Required)
                        errors.Add(TableCellError(sectionTitle, field.Label, rowNum, col.Label, "This field is required."));
                    continue;
                }

                switch (col.Type)
                {
                    case FieldType.Date:
                        if (!DateTime.TryParse(AsString(cell), CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                            errors.Add(TableCellError(sectionTitle, field.Label, rowNum, col.Label, "Enter a valid date."));
                        break;

                    case FieldType.Select:
                        var v = AsString(cell);
                        if (v != null && !ParseSelectValues(col.ConfigJson).Contains(v))
                            errors.Add(TableCellError(sectionTitle, field.Label, rowNum, col.Label, $"\"{v}\" is not a valid option."));
                        break;
                }
            }
        }
    }

    private static bool IsCellEmpty(FieldType columnType, JsonNode? cell)
    {
        if (cell == null)
            return true;
        // A Checkbox column always carries a bool value, so it is never "empty".
        if (columnType == FieldType.Checkbox)
            return AsBool(cell) == null;
        return string.IsNullOrWhiteSpace(AsString(cell));
    }

    private static string? AsString(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? AsBool(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static HashSet<string> ParseSelectValues(string? configJson)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(configJson))
            return set;
        try
        {
            var cfg = JsonSerializer.Deserialize<SelectFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            if (cfg?.Options != null)
                foreach (var option in cfg.Options)
                    set.Add(option.Value);
        }
        catch (JsonException)
        {
            // A malformed config yields an empty option set; membership checks then fail loudly.
        }
        return set;
    }

    private static (List<TableColumn> Columns, int? MinRows, int? MaxRows) ParseTableConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return (new List<TableColumn>(), null, null);
        try
        {
            var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            return (cfg?.Columns ?? new List<TableColumn>(), cfg?.MinRows, cfg?.MaxRows);
        }
        catch (JsonException)
        {
            return (new List<TableColumn>(), null, null);
        }
    }

    private static string RequiredError(string sectionTitle, string fieldLabel)
        => FieldError(sectionTitle, fieldLabel, "This field is required.");

    private static string FieldError(string sectionTitle, string fieldLabel, string message)
        => $"Section \"{sectionTitle}\" → \"{fieldLabel}\": {message}";

    private static string TableCellError(string sectionTitle, string fieldLabel, int rowNum, string columnLabel, string message)
        => $"Section \"{sectionTitle}\" → \"{fieldLabel}\" (row {rowNum}) → \"{columnLabel}\": {message}";

    // ---------------------------------------------------------------- Access helpers

    /// <summary>Educator with org access (Viewer+) OR a linked parent — read authorization for a version's student.</summary>
    private async Task<bool> CanReadStudentAsync(int userId, int studentId, CancellationToken ct)
    {
        if (await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return true;
        return await ParentCanViewStudentAsync(userId, studentId, ct);
    }

    /// <summary>
    /// True when the caller is a parent linked to this SchoolStudent: an active accepted ChildLink to a
    /// ChildProfile the caller has AccessService (Viewer+) access to (mirrors IepVersionService).
    /// </summary>
    private async Task<bool> ParentCanViewStudentAsync(int userId, int studentId, CancellationToken ct)
    {
        // One rule for "is this caller a parent of this student" — ParentAccessResolver (plan 6) is
        // the shared implementation; the version/artifact read paths must never drift from it.
        return await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, userId, studentId, AccessRole.Viewer, ct) != null;
    }

    private sealed record InstanceHeader(int SchoolStudentId, int DocumentTypeId, DocumentInstanceStatus Status);

    private async Task<InstanceHeader?> LoadInstanceHeaderAsync(int instanceId, CancellationToken ct) =>
        await _context.DocumentInstances
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => new InstanceHeader(i.SchoolStudentId, i.DocumentTypeId, i.Status))
            .FirstOrDefaultAsync(ct);

    private sealed record VersionHeader(int SchoolStudentId, int VersionNumber);

    private async Task<VersionHeader?> LoadVersionHeaderAsync(int versionId, CancellationToken ct) =>
        await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new VersionHeader(v.SchoolStudentId, v.VersionNumber))
            .FirstOrDefaultAsync(ct);

    // ---------------------------------------------------------------- Mappers

    // EF-translatable projection (PdfRenderStatus + DocumentType lookup join via nav properties). An
    // instance method (not a static field) so the AmendedByVersionIds correlated subquery can close over
    // `_context` — EF translates the closure to a correlated SELECT, not an in-memory round trip.
    private Expression<Func<AuthoredDocumentVersion, AuthoredDocumentVersionSummaryModel>> SummaryProjection() =>
        v => new AuthoredDocumentVersionSummaryModel
        {
            Id = v.Id,
            SchoolStudentId = v.SchoolStudentId,
            DocumentTypeId = v.DocumentTypeId,
            DocumentTypeKey = v.DocumentType.Key,
            DocumentTypeDisplayName = v.DocumentType.DisplayName,
            VersionNumber = v.VersionNumber,
            FinalizedByUserId = v.FinalizedByUserId,
            FinalizedAt = v.FinalizedAt,
            // Multilingual plan phase 7: reflects the ENGLISH row specifically (Pdf became Pdfs, one row
            // per language) — the same "the PDF's status" this summary showed before this phase.
            PdfRenderStatus = v.Pdfs.Where(p => p.Language == null || p.Language == SupportedLanguages.English)
                .Select(p => (PdfRenderStatus?)p.RenderStatus).FirstOrDefault(),
            SignatureStatus = v.SignatureStatus,
            SignedArtifactCount = v.SignedArtifacts.Count,
            AmendsVersionId = v.AmendsVersionId,
            AmendsVersionNumber = v.AmendsVersion != null ? (int?)v.AmendsVersion.VersionNumber : null,
            AmendmentReason = v.AmendmentReason,
            EffectiveDate = v.EffectiveDate,
            AmendedByVersionIds = _context.AuthoredDocumentVersions.Where(v2 => v2.AmendsVersionId == v.Id).Select(v2 => v2.Id).ToList()
        };
}
