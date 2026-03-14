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
        => await _dbSet.Where(c => c.UserId == userId && c.IsActive).OrderBy(c => c.FirstName).ToListAsync(cancellationToken);

    public async Task<ChildProfile?> GetByIdForUserAsync(int id, int userId, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.IsActive, cancellationToken);
}
