using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserModel>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<UserModel?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateUserAsync(int id, UpdateUserModel model, CancellationToken cancellationToken = default);
    Task<ServiceResult> DeleteUserAsync(int id, CancellationToken cancellationToken = default);
}
