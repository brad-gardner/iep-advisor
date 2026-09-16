using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IAccountService
{
    Task<object> ExportDataAsync(int userId, CancellationToken ct = default);
    Task<ServiceResult> ScheduleDeletionAsync(int userId, string password, string? mfaCode, CancellationToken ct = default);
    Task<ServiceResult> CancelDeletionAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// Anonymous counterpart to <see cref="CancelDeletionAsync"/> (pilot-gates plan, phase 2). Required
    /// because <see cref="ScheduleDeletionAsync"/> deactivates the account and bumps its SecurityStamp
    /// immediately, which invalidates the very session that could otherwise call the authenticated
    /// endpoint — the signed <paramref name="token"/> (emailed at request time) is the only way back in.
    /// </summary>
    Task<ServiceResult> CancelDeletionByTokenAsync(string token, CancellationToken ct = default);
}
