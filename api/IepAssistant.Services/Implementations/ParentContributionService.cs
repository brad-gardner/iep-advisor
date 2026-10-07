using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Multilingual plan (2026-10-06) phase 5: every failure a controller maps to a status carries
/// an explicit <see cref="ServiceErrorKind"/>, and every message is localized (<c>Messages.resx</c>/
/// <c>.es.resx</c>). <c>ParentContributionsController</c> maps status per endpoint rather than through
/// the shared mapper, so the SAME "Child profile not found." text intentionally carries a different kind
/// at each call site below: <see cref="GetForChildAsync"/>'s controller route always 404s, while
/// <see cref="CreateAsync"/>'s route always 400s on any failure (pre-existing, pinned by tests) — the
/// kind lives on the <see cref="ServiceResult"/> instance, not derived from the shared message text.</summary>
public class ParentContributionService : IParentContributionService
{
    private const int MaxPerChild = 30;
    private const int MaxTextLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuditLogger _audit;
    private readonly IStringLocalizer<Messages> _localizer;

    public ParentContributionService(ApplicationDbContext context, IAccessService access, IOrgAccessService orgAccess, IAuditLogger audit, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _access = access;
        _orgAccess = orgAccess;
        _audit = audit;
        _localizer = localizer;
    }

    public async Task<ServiceResult<List<ParentContributionModel>>> GetForChildAsync(int childId, int userId, CancellationToken ct = default)
    {
        if (await _access.GetRoleAsync(childId, userId, ct) == null)
            return ServiceResult<List<ParentContributionModel>>.FailureResult(ServiceErrorKind.NotFound, _localizer["Contributions.ChildNotFound"]);

        var items = await _context.ParentContributions.AsNoTracking()
            .Where(c => c.ChildProfileId == childId)
            .OrderBy(c => c.Kind).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);
        return ServiceResult<List<ParentContributionModel>>.SuccessResult(items.Select(Map).ToList());
    }

    public async Task<ServiceResult<ParentContributionModel>> CreateAsync(int childId, int userId, SaveParentContributionModel model, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<ParentContributionModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Contributions.ChildNotFound"]);
        var error = Validate(model);
        if (error != null) return ServiceResult<ParentContributionModel>.FailureResult(ServiceErrorKind.Validation, error);

        var count = await _context.ParentContributions.CountAsync(c => c.ChildProfileId == childId, ct);
        if (count >= MaxPerChild)
            return ServiceResult<ParentContributionModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Contributions.MaxPerChild", MaxPerChild]);

        var entity = new ParentContribution
        {
            ChildProfileId = childId,
            Kind = model.Kind,
            Text = model.Text.Trim(),
            IsShared = model.IsShared,
            CreatedById = userId,
            UpdatedById = userId
        };
        _context.ParentContributions.Add(entity);
        await _context.SaveChangesAsync(ct);
        if (entity.IsShared)
            _audit.Record(AuditAction.Share, userId, "ParentContribution", entity.Id);
        return ServiceResult<ParentContributionModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult<ParentContributionModel>> UpdateAsync(int id, int userId, SaveParentContributionModel model, CancellationToken ct = default)
    {
        var entity = await _context.ParentContributions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<ParentContributionModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Contributions.NotFound"]);
        var error = Validate(model);
        if (error != null) return ServiceResult<ParentContributionModel>.FailureResult(ServiceErrorKind.Validation, error);

        var newlyShared = model.IsShared && !entity.IsShared;
        entity.Kind = model.Kind;
        entity.Text = model.Text.Trim();
        entity.IsShared = model.IsShared;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);
        if (newlyShared)
            _audit.Record(AuditAction.Share, userId, "ParentContribution", entity.Id);
        return ServiceResult<ParentContributionModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken ct = default)
    {
        var entity = await _context.ParentContributions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Contributions.NotFound"]);
        _context.ParentContributions.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult<List<ParentContributionModel>>> GetSharedForSchoolStudentAsync(int educatorUserId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(educatorUserId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<ParentContributionModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Contributions.NoPermissionViewStudent"]);

        // Only accepted, active links; only notes the family explicitly marked shared.
        var childIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.SchoolStudentId == schoolStudentId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
            .Select(l => l.ChildProfileId!.Value)
            .ToListAsync(ct);
        if (childIds.Count == 0)
            return ServiceResult<List<ParentContributionModel>>.SuccessResult(new List<ParentContributionModel>());

        var items = await _context.ParentContributions.AsNoTracking()
            .Where(c => childIds.Contains(c.ChildProfileId) && c.IsShared)
            .OrderBy(c => c.Kind).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);
        // Staff read of family-authored text: leave the same access trace as other student reads.
        if (items.Count > 0)
            _audit.Record(AuditAction.View, educatorUserId, "ParentContributions", schoolStudentId);
        return ServiceResult<List<ParentContributionModel>>.SuccessResult(items.Select(Map).ToList());
    }

    private string? Validate(SaveParentContributionModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Text)) return _localizer["Contributions.TextRequired"];
        if (model.Text.Trim().Length > MaxTextLength) return _localizer["Contributions.TextTooLong", MaxTextLength];
        if (!Enum.IsDefined(model.Kind)) return _localizer["Contributions.InvalidKind"];
        return null;
    }

    private static ParentContributionModel Map(ParentContribution c) => new()
    {
        Id = c.Id, ChildProfileId = c.ChildProfileId, Kind = c.Kind, Text = c.Text, IsShared = c.IsShared,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };
}
