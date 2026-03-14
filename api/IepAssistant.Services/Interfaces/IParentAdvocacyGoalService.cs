using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IParentAdvocacyGoalService
{
    Task<IEnumerable<ParentAdvocacyGoalModel>> GetByChildIdAsync(int childId, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ParentAdvocacyGoalModel>> CreateAsync(int childId, int userId, CreateAdvocacyGoalModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(int id, int userId, UpdateAdvocacyGoalModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> ReorderAsync(int childId, int userId, List<ReorderAdvocacyGoalItem> items, CancellationToken cancellationToken = default);
}
