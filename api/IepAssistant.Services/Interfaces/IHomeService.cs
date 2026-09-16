using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// One server-computed "what needs me now" home per role (plan 5, decision 1) — a single request instead
/// of many client fetches. Dispatches on the caller's active staff org role first (any active
/// <c>StaffProfile</c>), then falls back to their <see cref="Domain.Entities.User.Role"/> (Student vs.
/// everyone else, treated as Parent).
/// </summary>
public interface IHomeService
{
    Task<ServiceResult<HomeModel>> GetForUserAsync(int userId, CancellationToken ct = default);
}
