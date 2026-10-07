using Microsoft.Extensions.Localization;
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
    private readonly IStringLocalizer<Messages> _localizer;

    private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "academic", "behavioral", "services", "placement"
    };

    public ParentAdvocacyGoalService(
        IParentAdvocacyGoalRepository repository,
        IChildProfileRepository childRepository,
        IAccessService accessService,
        ApplicationDbContext context,
        IStringLocalizer<Messages> localizer)
    {
        _repository = repository;
        _childRepository = childRepository;
        _accessService = accessService;
        _context = context;
        _localizer = localizer;
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
            return ServiceResult<ParentAdvocacyGoalModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Children.NotFound"]);

        if (model.Category != null && !ValidCategories.Contains(model.Category))
            return ServiceResult<ParentAdvocacyGoalModel>.FailureResult(ServiceErrorKind.Validation, _localizer["AdvocacyGoals.InvalidCategory"]);

        var existingGoals = (await _repository.GetByChildIdAsync(childId, cancellationToken)).ToList();
        if (existingGoals.Count >= 10)
            return ServiceResult<ParentAdvocacyGoalModel>.FailureResult(ServiceErrorKind.Validation, _localizer["AdvocacyGoals.MaxGoalsReached"]);

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

        return ServiceResult<ParentAdvocacyGoalModel>.SuccessResult(MapToModel(entity), _localizer["AdvocacyGoals.CreatedSuccessfully"]);
    }

    public async Task<ServiceResult> UpdateAsync(int id, int userId, UpdateAdvocacyGoalModel model, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithChildAsync(id, cancellationToken);
        if (entity == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["AdvocacyGoals.NotFound"]);

        if (!await _accessService.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["AdvocacyGoals.NotFound"]);

        if (model.GoalText != null)
            entity.GoalText = model.GoalText.Trim();

        if (model.Category != null)
        {
            if (model.Category != "" && !ValidCategories.Contains(model.Category))
                return ServiceResult.FailureResult(ServiceErrorKind.Validation, _localizer["AdvocacyGoals.InvalidCategory"]);
            entity.Category = model.Category == "" ? null : model.Category.ToLowerInvariant();
        }

        entity.UpdatedById = userId;
        _repository.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult(_localizer["AdvocacyGoals.UpdatedSuccessfully"]);
    }

    public async Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdWithChildAsync(id, cancellationToken);
        if (entity == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["AdvocacyGoals.NotFound"]);

        if (!await _accessService.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["AdvocacyGoals.NotFound"]);

        entity.IsActive = false;
        entity.UpdatedById = userId;
        _repository.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult(_localizer["AdvocacyGoals.DeletedSuccessfully"]);
    }

    public async Task<ServiceResult> ReorderAsync(int childId, int userId, List<ReorderAdvocacyGoalItem> items, CancellationToken cancellationToken = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, cancellationToken))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Children.NotFound"]);

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
        return ServiceResult.SuccessResult(_localizer["AdvocacyGoals.ReorderedSuccessfully"]);
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
