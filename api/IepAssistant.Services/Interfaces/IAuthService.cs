using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResult?> RefreshTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<ServiceResult> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default);
    Task<UserModel?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> UpdateProfileAsync(int userId, UpdateProfileModel model, CancellationToken cancellationToken = default);
    int? ValidateMfaPendingToken(string token);
    Task<AuthResult?> CompleteMfaLoginAsync(int userId, CancellationToken cancellationToken = default);
    Task<ServiceResult> CompleteOnboardingAsync(int userId, CancellationToken cancellationToken = default);
}
