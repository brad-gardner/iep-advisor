using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IPasswordResetService
{
    Task InitiateResetAsync(string email, CancellationToken ct = default);
    Task<ServiceResult> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default);
}
