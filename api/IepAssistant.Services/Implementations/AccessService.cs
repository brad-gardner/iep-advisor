using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

public class AccessService : IAccessService
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<(int, int), AccessRole?> _cache = new();

    public AccessService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AccessRole?> GetRoleAsync(int childId, int userId, CancellationToken ct = default)
    {
        var key = (childId, userId);
        if (_cache.TryGetValue(key, out var cachedRole))
            return cachedRole;

        var role = await _context.ChildAccesses
            .Where(ca => ca.ChildProfileId == childId
                      && ca.UserId == userId
                      && ca.IsActive
                      && ca.AcceptedAt != null)
            .OrderByDescending(ca => ca.Role)
            .Select(ca => (AccessRole?)ca.Role)
            .FirstOrDefaultAsync(ct);

        _cache[key] = role;
        return role;
    }

    public async Task<bool> HasMinimumRoleAsync(int childId, int userId, AccessRole minimumRole, CancellationToken ct = default)
    {
        var role = await GetRoleAsync(childId, userId, ct);
        return role != null && role >= minimumRole;
    }
}
