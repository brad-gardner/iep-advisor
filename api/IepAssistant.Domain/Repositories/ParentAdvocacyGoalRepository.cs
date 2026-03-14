using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Data;

namespace IepAssistant.Domain.Repositories;

public interface IParentAdvocacyGoalRepository : IRepository<ParentAdvocacyGoal>
{
    Task<IEnumerable<ParentAdvocacyGoal>> GetByChildIdAsync(int childProfileId, CancellationToken cancellationToken = default);
    Task<ParentAdvocacyGoal?> GetByIdWithChildAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetActiveCountAsync(int childProfileId, CancellationToken cancellationToken = default);
    Task<int> GetMaxDisplayOrderAsync(int childProfileId, CancellationToken cancellationToken = default);
}

public class ParentAdvocacyGoalRepository : Repository<ParentAdvocacyGoal>, IParentAdvocacyGoalRepository
{
    public ParentAdvocacyGoalRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ParentAdvocacyGoal>> GetByChildIdAsync(int childProfileId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(g => g.ChildProfileId == childProfileId && g.IsActive)
            .OrderBy(g => g.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<ParentAdvocacyGoal?> GetByIdWithChildAsync(int id, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(g => g.ChildProfile)
            .FirstOrDefaultAsync(g => g.Id == id && g.IsActive, cancellationToken);

    public async Task<int> GetActiveCountAsync(int childProfileId, CancellationToken cancellationToken = default)
        => await _dbSet.CountAsync(g => g.ChildProfileId == childProfileId && g.IsActive, cancellationToken);

    public async Task<int> GetMaxDisplayOrderAsync(int childProfileId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(g => g.ChildProfileId == childProfileId && g.IsActive)
            .Select(g => (int?)g.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;
    }
}
