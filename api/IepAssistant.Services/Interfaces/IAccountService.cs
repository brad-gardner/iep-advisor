using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IAccountService
{
    Task<object> ExportDataAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult> ScheduleDeletionAsync(int userId, string password, string? mfaCode, CancellationToken ct = default);
    Task<ServiceResult> CancelDeletionAsync(int userId, CancellationToken ct = default);
}
