using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ParentContributionService : IParentContributionService
{
    private const int MaxPerChild = 30;
    private const string NotFound = "Contribution not found.";

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;
    private readonly IOrgAccessService _orgAccess;

    public ParentContributionService(ApplicationDbContext context, IAccessService access, IOrgAccessService orgAccess)
    {
        _context = context;
        _access = access;
        _orgAccess = orgAccess;
    }

    public async Task<ServiceResult<List<ParentContributionModel>>> GetForChildAsync(int childId, int userId, CancellationToken ct = default)
    {
        if (await _access.GetRoleAsync(childId, userId, ct) == null)
            return ServiceResult<List<ParentContributionModel>>.FailureResult("Child profile not found.");

        var items = await _context.ParentContributions.AsNoTracking()
            .Where(c => c.ChildProfileId == childId)
            .OrderBy(c => c.Kind).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);
        return ServiceResult<List<ParentContributionModel>>.SuccessResult(items.Select(Map).ToList());
    }

    public async Task<ServiceResult<ParentContributionModel>> CreateAsync(int childId, int userId, SaveParentContributionModel model, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<ParentContributionModel>.FailureResult("Child profile not found.");
        var error = Validate(model);
        if (error != null) return ServiceResult<ParentContributionModel>.FailureResult(error);

        var count = await _context.ParentContributions.CountAsync(c => c.ChildProfileId == childId, ct);
        if (count >= MaxPerChild)
            return ServiceResult<ParentContributionModel>.FailureResult($"Maximum of {MaxPerChild} notes per child.");

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
        return ServiceResult<ParentContributionModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult<ParentContributionModel>> UpdateAsync(int id, int userId, SaveParentContributionModel model, CancellationToken ct = default)
    {
        var entity = await _context.ParentContributions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<ParentContributionModel>.FailureResult(NotFound);
        var error = Validate(model);
        if (error != null) return ServiceResult<ParentContributionModel>.FailureResult(error);

        entity.Kind = model.Kind;
        entity.Text = model.Text.Trim();
        entity.IsShared = model.IsShared;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult<ParentContributionModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken ct = default)
    {
        var entity = await _context.ParentContributions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(NotFound);
        _context.ParentContributions.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult<List<ParentContributionModel>>> GetSharedForSchoolStudentAsync(int educatorUserId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(educatorUserId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<ParentContributionModel>>.FailureResult("You do not have permission to view this student.");

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
        return ServiceResult<List<ParentContributionModel>>.SuccessResult(items.Select(Map).ToList());
    }

    private static string? Validate(SaveParentContributionModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Text)) return "Text is required.";
        if (model.Text.Trim().Length > 2000) return "Text must be 2000 characters or fewer.";
        if (!Enum.IsDefined(model.Kind)) return "Invalid kind.";
        return null;
    }

    private static ParentContributionModel Map(ParentContribution c) => new()
    {
        Id = c.Id, ChildProfileId = c.ChildProfileId, Kind = c.Kind, Text = c.Text, IsShared = c.IsShared,
        CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt
    };
}
