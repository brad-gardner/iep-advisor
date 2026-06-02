using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Finalize a draft into an immutable IepVersion snapshot, plus version reads (P5a).
///
/// <para><b>Concurrency strategy (serializable tx, not per-row rowversion):</b> finalize runs
/// inside a <see cref="IsolationLevel.Serializable"/> transaction. Within it we flip the draft to
/// <see cref="IepDraftStatus.Finalizing"/> and read all child collections. The serializable
/// isolation level gives us read/write atomicity — a concurrent edit cannot land a partial write
/// into the snapshot — without having to add a rowversion column to every P4 editable table. It
/// pairs with the IepDraftService "edit-freeze" (mutations refuse when Status==Finalizing) so a
/// concurrent edit that loaded before the freeze is rejected rather than silently captured.</para>
/// </summary>
public class IepVersionService : IIepVersionService
{
    private const string PermissionMessage = "You do not have permission to access this IEP version.";
    private const string DraftPermissionMessage = "You do not have permission to access this IEP draft.";
    private const string DraftNotFoundMessage = "IEP draft not found.";
    private const string VersionNotFoundMessage = "IEP version not found.";

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<IepVersionService> _logger;

    public IepVersionService(ApplicationDbContext context, IAccessService accessService, IBlobStorageService blob, ILogger<IepVersionService> logger)
    {
        _context = context;
        _accessService = accessService;
        _blob = blob;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Finalize

    public async Task<ServiceResult<IepVersionSummaryModel>> FinalizeAsync(int userId, int draftId, DateTime? effectiveDate, CancellationToken ct = default)
    {
        // 1. Collaborator+ access on the draft's student.
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepVersionSummaryModel>.FailureResult(access.Message!);

        IepVersionSummaryModel summary;

        // 2. Serializable transaction — atomic snapshot capture.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // 3. Re-read the draft inside the transaction.
            var draft = await _context.IepDrafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
            if (draft == null)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<IepVersionSummaryModel>.FailureResult(DraftNotFoundMessage);
            }

            if (draft.Status == IepDraftStatus.Finalizing)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<IepVersionSummaryModel>.FailureResult("Draft is already being finalized.");
            }

            // 4. Freeze the draft (blocks concurrent edits via the IepDraftService edit-freeze).
            draft.Status = IepDraftStatus.Finalizing;
            await _context.SaveChangesAsync(ct);

            // 5. Load all 5 child collections.
            var sections = await _context.IepDraftSections.AsNoTracking()
                .Where(s => s.IepDraftId == draftId).ToListAsync(ct);
            var goals = await _context.IepDraftGoals.AsNoTracking()
                .Where(g => g.IepDraftId == draftId).ToListAsync(ct);
            var serviceLines = await _context.IepDraftServiceLines.AsNoTracking()
                .Where(s => s.IepDraftId == draftId).ToListAsync(ct);
            var accommodations = await _context.IepDraftAccommodations.AsNoTracking()
                .Where(a => a.IepDraftId == draftId).ToListAsync(ct);
            var transitionItems = await _context.IepDraftTransitionItems.AsNoTracking()
                .Where(t => t.IepDraftId == draftId).ToListAsync(ct);

            // 6. VersionNumber = max for this student + 1.
            var maxVersion = await _context.IepVersions
                .Where(v => v.SchoolStudentId == draft.SchoolStudentId)
                .Select(v => (int?)v.VersionNumber)
                .MaxAsync(ct);
            var versionNumber = (maxVersion ?? 0) + 1;

            var now = DateTime.UtcNow;

            // 7. Create the immutable version aggregate, copying LineageId verbatim (carry-forward).
            var version = new IepVersion
            {
                SchoolStudentId = draft.SchoolStudentId,
                SourceDraftId = draftId,
                VersionNumber = versionNumber,
                DocumentType = draft.DocumentType,
                Title = draft.Title,
                EffectiveDate = effectiveDate,
                FinalizedByUserId = userId,
                FinalizedAt = now,
                CreatedById = userId,
                Sections = sections.Select(s => new IepVersionSection
                {
                    SectionKind = s.SectionKind,
                    RichText = s.RichText,
                    DisplayOrder = s.DisplayOrder,
                    LineageId = s.LineageId
                }).ToList(),
                Goals = goals.Select(g => new IepVersionGoal
                {
                    Domain = g.Domain,
                    GoalText = g.GoalText,
                    Baseline = g.Baseline,
                    TargetCriteria = g.TargetCriteria,
                    MeasurementMethod = g.MeasurementMethod,
                    Timeframe = g.Timeframe,
                    DisplayOrder = g.DisplayOrder,
                    LineageId = g.LineageId
                }).ToList(),
                ServiceLines = serviceLines.Select(s => new IepVersionServiceLine
                {
                    ServiceType = s.ServiceType,
                    Frequency = s.Frequency,
                    Duration = s.Duration,
                    Location = s.Location,
                    ProviderRole = s.ProviderRole,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    DisplayOrder = s.DisplayOrder,
                    LineageId = s.LineageId
                }).ToList(),
                Accommodations = accommodations.Select(a => new IepVersionAccommodation
                {
                    Category = a.Category,
                    Text = a.Text,
                    DisplayOrder = a.DisplayOrder,
                    LineageId = a.LineageId
                }).ToList(),
                TransitionItems = transitionItems.Select(t => new IepVersionTransitionItem
                {
                    PostsecondaryGoalArea = t.PostsecondaryGoalArea,
                    ServicesText = t.ServicesText,
                    DisplayOrder = t.DisplayOrder,
                    LineageId = t.LineageId
                }).ToList(),
                // P5b render worker flips this Pending -> Rendered/Error.
                Pdf = new IepVersionPdf
                {
                    RenderStatus = PdfRenderStatus.Pending,
                    CreatedById = userId
                }
            };

            await _context.IepVersions.AddAsync(version, ct);
            await _context.SaveChangesAsync(ct);

            // 8. Draft returns to Draft so it stays editable; re-finalize creates the next version.
            draft.Status = IepDraftStatus.Draft;
            await _context.SaveChangesAsync(ct);

            // 9. Commit.
            await transaction.CommitAsync(ct);

            summary = new IepVersionSummaryModel
            {
                Id = version.Id,
                SchoolStudentId = version.SchoolStudentId,
                SourceDraftId = draftId,
                VersionNumber = version.VersionNumber,
                DocumentType = version.DocumentType,
                Title = version.Title,
                EffectiveDate = effectiveDate,
                FinalizedByUserId = userId,
                FinalizedAt = version.FinalizedAt,
                PdfRenderStatus = PdfRenderStatus.Pending
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        // 10. The PDF render is enqueued by the controller AFTER this commit, failure-isolated and
        //     OUTSIDE the transaction (the worker must never read an uncommitted version). The
        //     IepVersionPdf row is created Pending above; the worker flips it to Rendered/Error.
        return ServiceResult<IepVersionSummaryModel>.SuccessResult(summary);
    }

    // ---------------------------------------------------------------- Reads

    public async Task<ServiceResult<List<IepVersionSummaryModel>>> ListForStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        var access = await CheckStudentAccessAsync(userId, studentId, AccessRole.Viewer, ct);
        if (!access.Success)
            return ServiceResult<List<IepVersionSummaryModel>>.FailureResult(access.Message!);

        var versions = await _context.IepVersions
            .AsNoTracking()
            .Where(v => v.SchoolStudentId == studentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        return ServiceResult<List<IepVersionSummaryModel>>.SuccessResult(versions);
    }

    public async Task<ServiceResult<List<IepVersionSummaryModel>>> ListForChildAsync(int userId, int childId, CancellationToken ct = default)
    {
        // Parent must have AccessService access to the child...
        if (!await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
            return ServiceResult<List<IepVersionSummaryModel>>.FailureResult(PermissionMessage);

        // ...and the version's SchoolStudent must be linked via an active accepted ChildLink.
        var versions = await ChildLinkedVersions(childId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        return ServiceResult<List<IepVersionSummaryModel>>.SuccessResult(versions);
    }

    public async Task<ServiceResult<IepVersionModel>> GetVersionAsync(int userId, int versionId, CancellationToken ct = default)
    {
        var studentId = await _context.IepVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => (int?)v.SchoolStudentId)
            .FirstOrDefaultAsync(ct);

        if (studentId == null)
            return ServiceResult<IepVersionModel>.FailureResult(VersionNotFoundMessage);

        // Authorize: an educator with SchoolStudentAccess OR a linked parent with child access.
        var educatorAccess = await CheckStudentAccessAsync(userId, studentId.Value, AccessRole.Viewer, ct);
        if (!educatorAccess.Success && !await ParentCanViewStudentAsync(userId, studentId.Value, ct))
            return ServiceResult<IepVersionModel>.FailureResult(PermissionMessage);

        var version = await _context.IepVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Sections)
            .Include(v => v.Goals)
            .Include(v => v.ServiceLines)
            .Include(v => v.Accommodations)
            .Include(v => v.TransitionItems)
            .Include(v => v.Pdf)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);

        if (version == null)
            return ServiceResult<IepVersionModel>.FailureResult(VersionNotFoundMessage);

        return ServiceResult<IepVersionModel>.SuccessResult(MapVersionFull(version));
    }

    // ---------------------------------------------------------------- PDF retry + download

    public async Task<ServiceResult<int>> RequestPdfRetryAsync(int userId, int versionId, CancellationToken ct = default)
    {
        var version = await _context.IepVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new { v.SchoolStudentId })
            .FirstOrDefaultAsync(ct);

        if (version == null)
            return ServiceResult<int>.FailureResult(VersionNotFoundMessage);

        // Retry is an authoring action — Collaborator+ educator on the student's school.
        var access = await CheckStudentAccessAsync(userId, version.SchoolStudentId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<int>.FailureResult(access.Message!);

        var pdf = await _context.IepVersionPdfs.FirstOrDefaultAsync(p => p.IepVersionId == versionId, ct);
        if (pdf == null)
            return ServiceResult<int>.FailureResult("This version has no PDF record to retry.");

        if (pdf.RenderStatus == PdfRenderStatus.Rendered)
            return ServiceResult<int>.FailureResult("This version's PDF is already rendered.");

        // Error or Pending -> set Pending so the UI shows "generating" until the worker re-renders.
        pdf.RenderStatus = PdfRenderStatus.Pending;
        pdf.ErrorMessage = null;
        pdf.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<int>.SuccessResult(versionId);
    }

    public async Task<ServiceResult<IepVersionPdfStatusModel>> GetPdfStatusAsync(int userId, int versionId, CancellationToken ct = default)
    {
        var version = await _context.IepVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new { v.SchoolStudentId, v.VersionNumber })
            .FirstOrDefaultAsync(ct);

        if (version == null)
            return ServiceResult<IepVersionPdfStatusModel>.FailureResult(VersionNotFoundMessage);

        // Same authorization as GetVersionAsync: educator-with-access OR linked-parent-with-access.
        var educatorAccess = await CheckStudentAccessAsync(userId, version.SchoolStudentId, AccessRole.Viewer, ct);
        if (!educatorAccess.Success && !await ParentCanViewStudentAsync(userId, version.SchoolStudentId, ct))
            return ServiceResult<IepVersionPdfStatusModel>.FailureResult(PermissionMessage);

        var pdf = await _context.IepVersionPdfs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IepVersionId == versionId, ct);

        var model = new IepVersionPdfStatusModel
        {
            VersionId = versionId,
            RenderStatus = pdf?.RenderStatus ?? PdfRenderStatus.Pending,
            RenderedAt = pdf?.RenderedAt,
            ErrorMessage = pdf?.ErrorMessage
        };

        if (pdf?.RenderStatus == PdfRenderStatus.Rendered)
        {
            // Build a short-lived download URL from the deterministic blob path (SAS when supported).
            var blobPath = IIepVersionPdfService.BlobPathFor(versionId, version.VersionNumber);
            model.Url = await _blob.GetDownloadUrlAsync(blobPath);
        }

        return ServiceResult<IepVersionPdfStatusModel>.SuccessResult(model);
    }

    // ---------------------------------------------------------------- Access helpers

    /// <summary>SchoolId-bound + role check, mirroring IepDraftService.CheckStudentAccessAsync.</summary>
    private async Task<ServiceResult> CheckStudentAccessAsync(int userId, int studentId, AccessRole minimumRole, CancellationToken ct)
    {
        var profile = await _context.TeacherProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (profile == null)
            return ServiceResult.FailureResult(PermissionMessage);

        var student = await _context.SchoolStudents
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == profile.SchoolId, ct);
        if (student == null)
            return ServiceResult.FailureResult(PermissionMessage);

        var hasAccess = await _context.SchoolStudentAccesses
            .AsNoTracking()
            .AnyAsync(a => a.SchoolStudentId == studentId && a.UserId == userId && a.IsActive && a.Role >= minimumRole, ct);

        return hasAccess
            ? ServiceResult.SuccessResult()
            : ServiceResult.FailureResult(PermissionMessage);
    }

    private async Task<ServiceResult> ResolveDraftAccessAsync(int userId, int draftId, AccessRole minimumRole, CancellationToken ct)
    {
        var studentId = await _context.IepDrafts
            .AsNoTracking()
            .Where(d => d.Id == draftId)
            .Select(d => (int?)d.SchoolStudentId)
            .FirstOrDefaultAsync(ct);

        if (studentId == null)
            return ServiceResult.FailureResult(DraftNotFoundMessage);

        var result = await CheckStudentAccessAsync(userId, studentId.Value, minimumRole, ct);
        // Surface a draft-flavored permission message for the finalize path.
        return result.Success ? result : ServiceResult.FailureResult(DraftPermissionMessage);
    }

    /// <summary>
    /// True when the caller is a parent linked to this SchoolStudent: an active accepted ChildLink
    /// to a ChildProfile the caller has AccessService (Viewer+) access to.
    /// </summary>
    private async Task<bool> ParentCanViewStudentAsync(int userId, int studentId, CancellationToken ct)
    {
        var linkedChildIds = await _context.ChildLinks
            .AsNoTracking()
            .Where(l => l.SchoolStudentId == studentId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
            .Select(l => l.ChildProfileId!.Value)
            .ToListAsync(ct);

        foreach (var childId in linkedChildIds)
        {
            if (await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
                return true;
        }
        return false;
    }

    /// <summary>Versions whose SchoolStudent is linked to <paramref name="childId"/> via an active accepted ChildLink.</summary>
    private IQueryable<IepVersion> ChildLinkedVersions(int childId)
    {
        var linkedStudentIds = _context.ChildLinks
            .Where(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId);

        return _context.IepVersions
            .AsNoTracking()
            .Where(v => linkedStudentIds.Contains(v.SchoolStudentId));
    }

    // ---------------------------------------------------------------- Mappers

    // EF-translatable projection expression (PdfRenderStatus joins via the optional 1:1 Pdf nav).
    private static readonly System.Linq.Expressions.Expression<Func<IepVersion, IepVersionSummaryModel>> SummaryProjection =
        v => new IepVersionSummaryModel
        {
            Id = v.Id,
            SchoolStudentId = v.SchoolStudentId,
            SourceDraftId = v.SourceDraftId,
            VersionNumber = v.VersionNumber,
            DocumentType = v.DocumentType,
            Title = v.Title,
            EffectiveDate = v.EffectiveDate,
            FinalizedByUserId = v.FinalizedByUserId,
            FinalizedAt = v.FinalizedAt,
            PdfRenderStatus = v.Pdf != null ? v.Pdf.RenderStatus : (PdfRenderStatus?)null
        };

    private static IepVersionModel MapVersionFull(IepVersion v)
    {
        var model = new IepVersionModel
        {
            Id = v.Id,
            SchoolStudentId = v.SchoolStudentId,
            SourceDraftId = v.SourceDraftId,
            VersionNumber = v.VersionNumber,
            DocumentType = v.DocumentType,
            Title = v.Title,
            EffectiveDate = v.EffectiveDate,
            FinalizedByUserId = v.FinalizedByUserId,
            FinalizedAt = v.FinalizedAt,
            PdfRenderStatus = v.Pdf?.RenderStatus,
            PdfBlobUri = v.Pdf?.BlobUri,
            PdfRenderedAt = v.Pdf?.RenderedAt
        };

        model.Sections = v.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).Select(s => new IepVersionSectionModel
        {
            Id = s.Id, IepVersionId = s.IepVersionId, SectionKind = s.SectionKind,
            RichText = s.RichText, DisplayOrder = s.DisplayOrder, LineageId = s.LineageId
        }).ToList();

        model.Goals = v.Goals.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id).Select(g => new IepVersionGoalModel
        {
            Id = g.Id, IepVersionId = g.IepVersionId, Domain = g.Domain, GoalText = g.GoalText,
            Baseline = g.Baseline, TargetCriteria = g.TargetCriteria, MeasurementMethod = g.MeasurementMethod,
            Timeframe = g.Timeframe, DisplayOrder = g.DisplayOrder, LineageId = g.LineageId
        }).ToList();

        model.ServiceLines = v.ServiceLines.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).Select(s => new IepVersionServiceLineModel
        {
            Id = s.Id, IepVersionId = s.IepVersionId, ServiceType = s.ServiceType, Frequency = s.Frequency,
            Duration = s.Duration, Location = s.Location, ProviderRole = s.ProviderRole,
            StartDate = s.StartDate, EndDate = s.EndDate, DisplayOrder = s.DisplayOrder, LineageId = s.LineageId
        }).ToList();

        model.Accommodations = v.Accommodations.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id).Select(a => new IepVersionAccommodationModel
        {
            Id = a.Id, IepVersionId = a.IepVersionId, Category = a.Category, Text = a.Text,
            DisplayOrder = a.DisplayOrder, LineageId = a.LineageId
        }).ToList();

        model.TransitionItems = v.TransitionItems.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).Select(t => new IepVersionTransitionItemModel
        {
            Id = t.Id, IepVersionId = t.IepVersionId, PostsecondaryGoalArea = t.PostsecondaryGoalArea,
            ServicesText = t.ServicesText, DisplayOrder = t.DisplayOrder, LineageId = t.LineageId
        }).ToList();

        return model;
    }
}
