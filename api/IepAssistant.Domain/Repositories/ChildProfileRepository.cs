using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Data;

namespace IepAssistant.Domain.Repositories;

public interface IChildProfileRepository : IRepository<ChildProfile>
{
    Task<IEnumerable<ChildProfile>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<ChildProfile?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default);
}

public class ChildProfileRepository : Repository<ChildProfile>, IChildProfileRepository
{
    public ChildProfileRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ChildProfile>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await _context.ChildProfiles
            .Where(c => c.IsActive && _context.Set<ChildAccess>()
                .Any(ca => ca.ChildProfileId == c.Id && ca.UserId == userId && ca.IsActive && ca.AcceptedAt != null))
            .OrderBy(c => c.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<ChildProfile?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default)
        => await _context.ChildProfiles
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive && _context.Set<ChildAccess>()
                .Any(ca => ca.ChildProfileId == c.Id && ca.UserId == userId && ca.IsActive && ca.AcceptedAt != null), cancellationToken);
}
