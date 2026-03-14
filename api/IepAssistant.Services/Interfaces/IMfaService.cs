using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IMfaService
{
    Task<MfaSetupResult> SetupAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult<List<string>>> VerifySetupAsync(int userId, string code, CancellationToken ct = default);
    Task<bool> ValidateCodeAsync(int userId, string code, CancellationToken ct = default);
    Task<bool> ValidateRecoveryCodeAsync(int userId, string code, CancellationToken ct = default);
    Task<ServiceResult> DisableAsync(int userId, string password, string totpCode, CancellationToken ct = default);
}
