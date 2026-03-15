using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Interfaces;

public interface IAccessService
{
    Task<AccessRole?> GetRoleAsync(int childId, int userId, CancellationToken ct = default);
    Task<bool> HasMinimumRoleAsync(int childId, int userId, AccessRole minimumRole, CancellationToken ct = default);
}
