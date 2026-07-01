using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Structured IEP authoring (P4a). Reads require any active SchoolStudentAccess (Viewer+);
/// mutations require Role &gt;= Collaborator. Editing is last-write-wins: every create/update
/// stamps the caller + timestamp on the affected entity AND on the parent draft.
/// </summary>
public class IepDraftService : IIepDraftService
{
    private const string PermissionMessage = "You do not have permission to access this IEP draft.";
    private const string DraftNotFoundMessage = "IEP draft not found.";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuditLogger _audit;
    private readonly ILogger<IepDraftService> _logger;

    public IepDraftService(ApplicationDbContext context, IOrgAccessService orgAccess, IAuditLogger audit, ILogger<IepDraftService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _audit = audit;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Drafts

    public async Task<ServiceResult<IepDraftModel>> CreateDraftAsync(int userId, int studentId, string? title, CancellationToken ct = default)
    {
        var access = await CheckStudentAccessAsync(userId, studentId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftModel>.FailureResult(access.Message!);

        var now = DateTime.UtcNow;
        var draft = new IepDraft
        {
            SchoolStudentId = studentId,
            Status = IepDraftStatus.Draft,
            DocumentType = IepDocumentType.Iep,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            LastEditedByUserId = userId,
            LastEditedAt = now,
            CreatedById = userId
        };
        await _context.IepDrafts.AddAsync(draft, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<IepDraftModel>.SuccessResult(MapDraft(draft));
    }

    public async Task<ServiceResult<List<IepDraftModel>>> ListDraftsAsync(int userId, int studentId, CancellationToken ct = default)
    {
        var access = await CheckStudentAccessAsync(userId, studentId, AccessRole.Viewer, ct);
        if (!access.Success)
            return ServiceResult<List<IepDraftModel>>.FailureResult(access.Message!);

        var drafts = await _context.IepDrafts
            .AsNoTracking()
            .Where(d => d.SchoolStudentId == studentId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        // List view returns draft headers only (no child collections).
        return ServiceResult<List<IepDraftModel>>.SuccessResult(drafts.Select(MapDraft).ToList());
    }

    public async Task<ServiceResult<IepDraftModel>> GetDraftAsync(int userId, int draftId, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Viewer, ct);
        if (!access.Success)
            return ServiceResult<IepDraftModel>.FailureResult(access.Message!);

        var draft = await _context.IepDrafts
            .AsNoTracking()
            .AsSplitQuery() // 5 collection Includes — avoid a cartesian-explosion join
            .Include(d => d.Sections)
            .Include(d => d.Goals)
            .Include(d => d.ServiceLines)
            .Include(d => d.Accommodations)
            .Include(d => d.TransitionItems)
            .FirstOrDefaultAsync(d => d.Id == draftId, ct);

        if (draft == null)
            return ServiceResult<IepDraftModel>.FailureResult(DraftNotFoundMessage);

        _audit.Record(AuditAction.View, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftModel>.SuccessResult(MapDraftFull(draft));
    }

    // ---------------------------------------------------------------- Sections

    public async Task<ServiceResult<IepDraftSectionModel>> AddSectionAsync(int userId, int draftId, UpsertIepDraftSectionModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftSectionModel>.FailureResult(access.Message!);

        var now = DateTime.UtcNow;
        var order = await NextOrderAsync(_context.IepDraftSections.Where(s => s.IepDraftId == draftId).Select(s => s.DisplayOrder), ct);
        var entity = new IepDraftSection
        {
            IepDraftId = draftId,
            SectionKind = model.SectionKind,
            RichText = model.RichText,
            DisplayOrder = order,
            LineageId = Guid.NewGuid(),
            LastEditedByUserId = userId,
            LastEditedAt = now,
            CreatedById = userId
        };
        await _context.IepDraftSections.AddAsync(entity, ct);
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftSectionModel>.SuccessResult(MapSection(entity));
    }

    public async Task<ServiceResult<IepDraftSectionModel>> UpdateSectionAsync(int userId, int draftId, int id, UpsertIepDraftSectionModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftSectionModel>.FailureResult(access.Message!);

        var entity = await _context.IepDraftSections.FirstOrDefaultAsync(s => s.Id == id && s.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult<IepDraftSectionModel>.FailureResult("Section not found.");

        var now = DateTime.UtcNow;
        entity.SectionKind = model.SectionKind;
        entity.RichText = model.RichText;
        entity.LastEditedByUserId = userId;
        entity.LastEditedAt = now;
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftSectionModel>.SuccessResult(MapSection(entity));
    }

    public async Task<ServiceResult> DeleteSectionAsync(int userId, int draftId, int id, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult.FailureResult(access.Message!);

        var entity = await _context.IepDraftSections.FirstOrDefaultAsync(s => s.Id == id && s.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult.FailureResult("Section not found.");

        _context.IepDraftSections.Remove(entity);
        await StampDraftAsync(draftId, userId, DateTime.UtcNow, ct);
        await _context.SaveChangesAsync(ct);
        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Goals

    public async Task<ServiceResult<IepDraftGoalModel>> AddGoalAsync(int userId, int draftId, UpsertIepDraftGoalModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftGoalModel>.FailureResult(access.Message!);

        var now = DateTime.UtcNow;
        var order = await NextOrderAsync(_context.IepDraftGoals.Where(g => g.IepDraftId == draftId).Select(g => g.DisplayOrder), ct);
        var entity = new IepDraftGoal
        {
            IepDraftId = draftId,
            Domain = model.Domain,
            GoalText = model.GoalText,
            Baseline = model.Baseline,
            TargetCriteria = model.TargetCriteria,
            MeasurementMethod = model.MeasurementMethod,
            Timeframe = model.Timeframe,
            DisplayOrder = order,
            LineageId = Guid.NewGuid(),
            LastEditedByUserId = userId,
            LastEditedAt = now,
            CreatedById = userId
        };
        await _context.IepDraftGoals.AddAsync(entity, ct);
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftGoalModel>.SuccessResult(MapGoal(entity));
    }

    public async Task<ServiceResult<IepDraftGoalModel>> UpdateGoalAsync(int userId, int draftId, int id, UpsertIepDraftGoalModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftGoalModel>.FailureResult(access.Message!);

        var entity = await _context.IepDraftGoals.FirstOrDefaultAsync(g => g.Id == id && g.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult<IepDraftGoalModel>.FailureResult("Goal not found.");

        var now = DateTime.UtcNow;
        entity.Domain = model.Domain;
        entity.GoalText = model.GoalText;
        entity.Baseline = model.Baseline;
        entity.TargetCriteria = model.TargetCriteria;
        entity.MeasurementMethod = model.MeasurementMethod;
        entity.Timeframe = model.Timeframe;
        entity.LastEditedByUserId = userId;
        entity.LastEditedAt = now;
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftGoalModel>.SuccessResult(MapGoal(entity));
    }

    public async Task<ServiceResult> DeleteGoalAsync(int userId, int draftId, int id, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult.FailureResult(access.Message!);

        var entity = await _context.IepDraftGoals.FirstOrDefaultAsync(g => g.Id == id && g.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult.FailureResult("Goal not found.");

        _context.IepDraftGoals.Remove(entity);
        await StampDraftAsync(draftId, userId, DateTime.UtcNow, ct);
        await _context.SaveChangesAsync(ct);
        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Service lines

    public async Task<ServiceResult<IepDraftServiceLineModel>> AddServiceLineAsync(int userId, int draftId, UpsertIepDraftServiceLineModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftServiceLineModel>.FailureResult(access.Message!);

        var now = DateTime.UtcNow;
        var order = await NextOrderAsync(_context.IepDraftServiceLines.Where(s => s.IepDraftId == draftId).Select(s => s.DisplayOrder), ct);
        var entity = new IepDraftServiceLine
        {
            IepDraftId = draftId,
            ServiceType = model.ServiceType,
            Frequency = model.Frequency,
            Duration = model.Duration,
            Location = model.Location,
            ProviderRole = model.ProviderRole,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            DisplayOrder = order,
            LineageId = Guid.NewGuid(),
            LastEditedByUserId = userId,
            LastEditedAt = now,
            CreatedById = userId
        };
        await _context.IepDraftServiceLines.AddAsync(entity, ct);
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftServiceLineModel>.SuccessResult(MapServiceLine(entity));
    }

    public async Task<ServiceResult<IepDraftServiceLineModel>> UpdateServiceLineAsync(int userId, int draftId, int id, UpsertIepDraftServiceLineModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftServiceLineModel>.FailureResult(access.Message!);

        var entity = await _context.IepDraftServiceLines.FirstOrDefaultAsync(s => s.Id == id && s.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult<IepDraftServiceLineModel>.FailureResult("Service line not found.");

        var now = DateTime.UtcNow;
        entity.ServiceType = model.ServiceType;
        entity.Frequency = model.Frequency;
        entity.Duration = model.Duration;
        entity.Location = model.Location;
        entity.ProviderRole = model.ProviderRole;
        entity.StartDate = model.StartDate;
        entity.EndDate = model.EndDate;
        entity.LastEditedByUserId = userId;
        entity.LastEditedAt = now;
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftServiceLineModel>.SuccessResult(MapServiceLine(entity));
    }

    public async Task<ServiceResult> DeleteServiceLineAsync(int userId, int draftId, int id, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult.FailureResult(access.Message!);

        var entity = await _context.IepDraftServiceLines.FirstOrDefaultAsync(s => s.Id == id && s.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult.FailureResult("Service line not found.");

        _context.IepDraftServiceLines.Remove(entity);
        await StampDraftAsync(draftId, userId, DateTime.UtcNow, ct);
        await _context.SaveChangesAsync(ct);
        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Accommodations

    public async Task<ServiceResult<IepDraftAccommodationModel>> AddAccommodationAsync(int userId, int draftId, UpsertIepDraftAccommodationModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftAccommodationModel>.FailureResult(access.Message!);

        var now = DateTime.UtcNow;
        var order = await NextOrderAsync(_context.IepDraftAccommodations.Where(a => a.IepDraftId == draftId).Select(a => a.DisplayOrder), ct);
        var entity = new IepDraftAccommodation
        {
            IepDraftId = draftId,
            Category = model.Category,
            Text = model.Text,
            DisplayOrder = order,
            LineageId = Guid.NewGuid(),
            LastEditedByUserId = userId,
            LastEditedAt = now,
            CreatedById = userId
        };
        await _context.IepDraftAccommodations.AddAsync(entity, ct);
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftAccommodationModel>.SuccessResult(MapAccommodation(entity));
    }

    public async Task<ServiceResult<IepDraftAccommodationModel>> UpdateAccommodationAsync(int userId, int draftId, int id, UpsertIepDraftAccommodationModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftAccommodationModel>.FailureResult(access.Message!);

        var entity = await _context.IepDraftAccommodations.FirstOrDefaultAsync(a => a.Id == id && a.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult<IepDraftAccommodationModel>.FailureResult("Accommodation not found.");

        var now = DateTime.UtcNow;
        entity.Category = model.Category;
        entity.Text = model.Text;
        entity.LastEditedByUserId = userId;
        entity.LastEditedAt = now;
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftAccommodationModel>.SuccessResult(MapAccommodation(entity));
    }

    public async Task<ServiceResult> DeleteAccommodationAsync(int userId, int draftId, int id, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult.FailureResult(access.Message!);

        var entity = await _context.IepDraftAccommodations.FirstOrDefaultAsync(a => a.Id == id && a.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult.FailureResult("Accommodation not found.");

        _context.IepDraftAccommodations.Remove(entity);
        await StampDraftAsync(draftId, userId, DateTime.UtcNow, ct);
        await _context.SaveChangesAsync(ct);
        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Transition items

    public async Task<ServiceResult<IepDraftTransitionItemModel>> AddTransitionItemAsync(int userId, int draftId, UpsertIepDraftTransitionItemModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftTransitionItemModel>.FailureResult(access.Message!);

        var now = DateTime.UtcNow;
        var order = await NextOrderAsync(_context.IepDraftTransitionItems.Where(t => t.IepDraftId == draftId).Select(t => t.DisplayOrder), ct);
        var entity = new IepDraftTransitionItem
        {
            IepDraftId = draftId,
            PostsecondaryGoalArea = model.PostsecondaryGoalArea,
            ServicesText = model.ServicesText,
            DisplayOrder = order,
            LineageId = Guid.NewGuid(),
            LastEditedByUserId = userId,
            LastEditedAt = now,
            CreatedById = userId
        };
        await _context.IepDraftTransitionItems.AddAsync(entity, ct);
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftTransitionItemModel>.SuccessResult(MapTransitionItem(entity));
    }

    public async Task<ServiceResult<IepDraftTransitionItemModel>> UpdateTransitionItemAsync(int userId, int draftId, int id, UpsertIepDraftTransitionItemModel model, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult<IepDraftTransitionItemModel>.FailureResult(access.Message!);

        var entity = await _context.IepDraftTransitionItems.FirstOrDefaultAsync(t => t.Id == id && t.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult<IepDraftTransitionItemModel>.FailureResult("Transition item not found.");

        var now = DateTime.UtcNow;
        entity.PostsecondaryGoalArea = model.PostsecondaryGoalArea;
        entity.ServicesText = model.ServicesText;
        entity.LastEditedByUserId = userId;
        entity.LastEditedAt = now;
        await StampDraftAsync(draftId, userId, now, ct);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult<IepDraftTransitionItemModel>.SuccessResult(MapTransitionItem(entity));
    }

    public async Task<ServiceResult> DeleteTransitionItemAsync(int userId, int draftId, int id, CancellationToken ct = default)
    {
        var access = await ResolveDraftAccessAsync(userId, draftId, AccessRole.Collaborator, ct);
        if (!access.Success)
            return ServiceResult.FailureResult(access.Message!);

        var entity = await _context.IepDraftTransitionItems.FirstOrDefaultAsync(t => t.Id == id && t.IepDraftId == draftId, ct);
        if (entity == null)
            return ServiceResult.FailureResult("Transition item not found.");

        _context.IepDraftTransitionItems.Remove(entity);
        await StampDraftAsync(draftId, userId, DateTime.UtcNow, ct);
        await _context.SaveChangesAsync(ct);
        _audit.Record(AuditAction.Edit, userId, "IepDraft", draftId);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Access helpers

    /// <summary>
    /// Org access check delegated to <see cref="IOrgAccessService"/> (player-coach: admins pass within
    /// scope; teachers need an active SchoolStudentAccess with Role &gt;= <paramref name="minimumRole"/>).
    /// Insufficient access yields a "permission" failure.
    /// </summary>
    private async Task<ServiceResult> CheckStudentAccessAsync(int userId, int studentId, AccessRole minimumRole, CancellationToken ct)
    {
        return await _orgAccess.CanActOnStudentAsync(userId, studentId, minimumRole, ct)
            ? ServiceResult.SuccessResult()
            : ServiceResult.FailureResult(PermissionMessage);
    }

    /// <summary>
    /// Resolves the draft to its student, then runs the SchoolId-bound + role check.
    /// For mutating callers (<paramref name="minimumRole"/> &gt;= Collaborator) this also enforces the
    /// P5 edit-freeze: if the draft is currently being finalized (Status == Finalizing) the mutation
    /// is rejected. Paired with the serializable finalize transaction, this guarantees a concurrent
    /// edit can never be partially captured into the immutable snapshot.
    /// </summary>
    private async Task<ServiceResult> ResolveDraftAccessAsync(int userId, int draftId, AccessRole minimumRole, CancellationToken ct)
    {
        var draftInfo = await _context.IepDrafts
            .AsNoTracking()
            .Where(d => d.Id == draftId)
            .Select(d => new { d.SchoolStudentId, d.Status })
            .FirstOrDefaultAsync(ct);

        if (draftInfo == null)
            return ServiceResult.FailureResult(DraftNotFoundMessage);

        var access = await CheckStudentAccessAsync(userId, draftInfo.SchoolStudentId, minimumRole, ct);
        if (!access.Success)
            return access;

        if (minimumRole >= AccessRole.Collaborator && draftInfo.Status == IepDraftStatus.Finalizing)
            return ServiceResult.FailureResult("The draft is being finalized; try again in a moment.");

        return access;
    }

    private static async Task<int> NextOrderAsync(IQueryable<int> displayOrders, CancellationToken ct)
    {
        // DisplayOrder = max + 1; projecting to int? lets MAX return NULL on an empty set
        // (translates cleanly on SQL Server and SQLite), which we treat as -1 so the first row is 0.
        var max = await displayOrders.Select(o => (int?)o).MaxAsync(ct);
        return (max ?? -1) + 1;
    }

    /// <summary>Stamps the parent draft so it surfaces overall last-edited (last-write-wins).</summary>
    private async Task StampDraftAsync(int draftId, int userId, DateTime now, CancellationToken ct)
    {
        var draft = await _context.IepDrafts.FirstOrDefaultAsync(d => d.Id == draftId, ct);
        if (draft == null)
            return;
        draft.LastEditedByUserId = userId;
        draft.LastEditedAt = now;
    }

    // ---------------------------------------------------------------- Mappers

    private static IepDraftModel MapDraft(IepDraft d) => new()
    {
        Id = d.Id,
        SchoolStudentId = d.SchoolStudentId,
        Status = d.Status,
        DocumentType = d.DocumentType,
        Title = d.Title,
        LastEditedByUserId = d.LastEditedByUserId,
        LastEditedAt = d.LastEditedAt,
        CreatedAt = d.CreatedAt
    };

    private static IepDraftModel MapDraftFull(IepDraft d)
    {
        var model = MapDraft(d);
        // ThenBy(Id) gives stable ordering even when concurrent adds produce duplicate DisplayOrder.
        model.Sections = d.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).Select(MapSection).ToList();
        model.Goals = d.Goals.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id).Select(MapGoal).ToList();
        model.ServiceLines = d.ServiceLines.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).Select(MapServiceLine).ToList();
        model.Accommodations = d.Accommodations.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id).Select(MapAccommodation).ToList();
        model.TransitionItems = d.TransitionItems.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).Select(MapTransitionItem).ToList();
        return model;
    }

    private static IepDraftSectionModel MapSection(IepDraftSection s) => new()
    {
        Id = s.Id,
        IepDraftId = s.IepDraftId,
        SectionKind = s.SectionKind,
        RichText = s.RichText,
        DisplayOrder = s.DisplayOrder,
        LineageId = s.LineageId,
        LastEditedByUserId = s.LastEditedByUserId,
        LastEditedAt = s.LastEditedAt
    };

    private static IepDraftGoalModel MapGoal(IepDraftGoal g) => new()
    {
        Id = g.Id,
        IepDraftId = g.IepDraftId,
        Domain = g.Domain,
        GoalText = g.GoalText,
        Baseline = g.Baseline,
        TargetCriteria = g.TargetCriteria,
        MeasurementMethod = g.MeasurementMethod,
        Timeframe = g.Timeframe,
        DisplayOrder = g.DisplayOrder,
        LineageId = g.LineageId,
        LastEditedByUserId = g.LastEditedByUserId,
        LastEditedAt = g.LastEditedAt
    };

    private static IepDraftServiceLineModel MapServiceLine(IepDraftServiceLine s) => new()
    {
        Id = s.Id,
        IepDraftId = s.IepDraftId,
        ServiceType = s.ServiceType,
        Frequency = s.Frequency,
        Duration = s.Duration,
        Location = s.Location,
        ProviderRole = s.ProviderRole,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        DisplayOrder = s.DisplayOrder,
        LineageId = s.LineageId,
        LastEditedByUserId = s.LastEditedByUserId,
        LastEditedAt = s.LastEditedAt
    };

    private static IepDraftAccommodationModel MapAccommodation(IepDraftAccommodation a) => new()
    {
        Id = a.Id,
        IepDraftId = a.IepDraftId,
        Category = a.Category,
        Text = a.Text,
        DisplayOrder = a.DisplayOrder,
        LineageId = a.LineageId,
        LastEditedByUserId = a.LastEditedByUserId,
        LastEditedAt = a.LastEditedAt
    };

    private static IepDraftTransitionItemModel MapTransitionItem(IepDraftTransitionItem t) => new()
    {
        Id = t.Id,
        IepDraftId = t.IepDraftId,
        PostsecondaryGoalArea = t.PostsecondaryGoalArea,
        ServicesText = t.ServicesText,
        DisplayOrder = t.DisplayOrder,
        LineageId = t.LineageId,
        LastEditedByUserId = t.LastEditedByUserId,
        LastEditedAt = t.LastEditedAt
    };
}
