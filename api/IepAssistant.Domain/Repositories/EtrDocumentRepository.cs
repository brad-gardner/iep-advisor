using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Data;

namespace IepAssistant.Domain.Repositories;

public interface IEtrDocumentRepository : IRepository<EtrDocument>
{
    Task<IEnumerable<EtrDocument>> GetByChildProfileIdAsync(int childProfileId, CancellationToken cancellationToken = default);
    Task<EtrDocument?> GetByIdWithChildAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EtrDocument>> GetAllByUserAsync(int userId, CancellationToken cancellationToken = default);
}

public class EtrDocumentRepository : Repository<EtrDocument>, IEtrDocumentRepository
{
    public EtrDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EtrDocument>> GetByChildProfileIdAsync(int childProfileId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(d => d.ChildProfileId == childProfileId && d.IsActive)
            .OrderByDescending(d => d.EvaluationDate ?? d.UploadDate)
            .ToListAsync(cancellationToken);

    public async Task<EtrDocument?> GetByIdWithChildAsync(int id, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(d => d.ChildProfile)
            .FirstOrDefaultAsync(d => d.Id == id && d.IsActive, cancellationToken);

    public async Task<IEnumerable<EtrDocument>> GetAllByUserAsync(int userId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(d => d.ChildProfile)
            .Where(d => d.IsActive
                && d.ChildProfile!.IsActive
                && _context.Set<ChildAccess>()
                    .Any(ca => ca.ChildProfileId == d.ChildProfileId
                            && ca.UserId == userId
                            && ca.IsActive
                            && ca.AcceptedAt != null))
            .OrderByDescending(d => d.EvaluationDate ?? d.UploadDate)
            .ToListAsync(cancellationToken);
}
