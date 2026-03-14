using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IChildProfileService
{
    Task<IEnumerable<ChildProfileModel>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<ChildProfileModel?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ChildProfileModel>> CreateAsync(int userId, CreateChildProfileModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateAsync(int id, int userId, UpdateChildProfileModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
}
