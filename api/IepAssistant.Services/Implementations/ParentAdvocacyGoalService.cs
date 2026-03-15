using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ParentAdvocacyGoalService : IParentAdvocacyGoalService
{
    private readonly IParentAdvocacyGoalRepository _repository;
    private readonly IChildProfileRepository _childRepository;
    private readonly IAccessService _accessService;
    private readonly ApplicationDbContext _context;

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "academic", "behavioral", "services", "placement"
    };

    public ParentAdvocacyGoalService(
        IParentAdvocacyGoalRepository repository,
        IChildProfileRepository childRepository,
        IAccessService accessService,
        ApplicationDbContext context)
    {
        _repository = repository;
        _childRepository = childRepository;
        _accessService = accessService;
        _context = context;
    }

    public async Task<IEnumerable<ParentAdvocacyGoalModel>> GetByChildIdAsync(int childId, int userId, CancellationToken cancellationToken = default)
    {
        var role = await _accessService.GetRoleAsync(childId, userId, cancellationToken);
        if (role == null) return [];

        var goals = await _repository.GetByChildIdAsync(childId, cancellationToken);
        return goals.Select(MapToModel);
    }

    public async Task<ServiceResult<ParentAdvocacyGoalModel>> CreateAsync(int childId, int userId, CreateAdvocacyGoalModel model, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult<ParentAdvocacyGoalModel>.FailureResult("Child profile not found.");

        if (model.Category != null && !ValidCategories.Contains(model.Category))
            return ServiceResult<ParentAdvocacyGoalModel>.FailureResult("Invalid category. Must be: academic, behavioral, services, or placement.");

        var existingGoals = (await _repository.GetByChildIdAsync(childId, cancellationToken)).ToList();
        if (existingGoals.Count >= 10)
            return ServiceResult<ParentAdvocacyGoalModel>.FailureResult("Maximum of 10 advocacy goals per child. Consider consolidating goals for better analysis.");

        var maxOrder = existingGoals.Count > 0 ? existingGoals.Max(g => g.DisplayOrder) : 0;

        var entity = new ParentAdvocacyGoal
        {
            ChildProfileId = childId,
            GoalText = model.GoalText.Trim(),
            Category = model.Category?.ToLowerInvariant(),
            DisplayOrder = maxOrder + 1,
            CreatedById = userId,
            UpdatedById = userId
        };

        await _repository.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult<ParentAdvocacyGoalModel>.SuccessResult(MapToModel(entity), "Advocacy goal created successfully.");
    }

    public async Task<ServiceResult> UpdateAsync(int id, int userId, UpdateAdvocacyGoalModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithChildAsync(id, cancellationToken);
        if (entity == null)
            return ServiceResult.FailureResult("Advocacy goal not found.");

        if (!await _accessService.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Advocacy goal not found.");

        if (model.GoalText != null)
            entity.GoalText = model.GoalText.Trim();

        if (model.Category != null)
        {
            if (model.Category != "" && !ValidCategories.Contains(model.Category))
                return ServiceResult.FailureResult("Invalid category. Must be: academic, behavioral, services, or placement.");
            entity.Category = model.Category == "" ? null : model.Category.ToLowerInvariant();
        }

        entity.UpdatedById = userId;
        _repository.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Advocacy goal updated successfully.");
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithChildAsync(id, cancellationToken);
        if (entity == null)
            return ServiceResult.FailureResult("Advocacy goal not found.");

        if (!await _accessService.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Advocacy goal not found.");

        entity.IsActive = false;
        entity.UpdatedById = userId;
        _repository.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Advocacy goal deleted successfully.");
    }

    public async Task<ServiceResult> ReorderAsync(int childId, int userId, List<ReorderAdvocacyGoalItem> items, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult("Child profile not found.");

        var goals = (await _repository.GetByChildIdAsync(childId, cancellationToken)).ToList();
        var goalMap = goals.ToDictionary(g => g.Id);

        foreach (var item in items)
        {
            if (goalMap.TryGetValue(item.Id, out var goal))
            {
                goal.DisplayOrder = item.DisplayOrder;
                _repository.Update(goal);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ServiceResult.SuccessResult("Goals reordered successfully.");
    }

    private static ParentAdvocacyGoalModel MapToModel(ParentAdvocacyGoal entity) => new()
    {
        Id = entity.Id,
        ChildProfileId = entity.ChildProfileId,
        GoalText = entity.GoalText,
        Category = entity.Category,
        DisplayOrder = entity.DisplayOrder,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
