using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IShareService
{
    Task<ServiceResult<ChildAccessModel>> InviteAsync(int childId, int userId, string email, AccessRole role, CancellationToken ct = default);
    Task<ServiceResult> AcceptInviteAsync(int userId, string token, CancellationToken ct = default);
    Task<IEnumerable<ChildAccessModel>> GetAccessListAsync(int childId, int userId, CancellationToken ct = default);
    Task<ServiceResult> RevokeAccessAsync(int childId, int accessId, int userId, CancellationToken ct = default);
}
