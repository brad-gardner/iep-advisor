using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Data;

namespace IepAssistant.Domain.Repositories;

public interface IIepDocumentRepository : IRepository<IepDocument>
{
    Task<IEnumerable<IepDocument>> GetByChildProfileIdAsync(int childProfileId, CancellationToken cancellationToken = default);
    Task<IepDocument?> GetByIdWithChildAsync(int id, CancellationToken cancellationToken = default);
}

public class IepDocumentRepository : Repository<IepDocument>, IIepDocumentRepository
{
    public IepDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<IepDocument>> GetByChildProfileIdAsync(int childProfileId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(d => d.ChildProfileId == childProfileId && d.IsActive)
            .OrderByDescending(d => d.IepDate ?? d.UploadDate)
            .ToListAsync(cancellationToken);

    public async Task<IepDocument?> GetByIdWithChildAsync(int id, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(d => d.ChildProfile)
            .FirstOrDefaultAsync(d => d.Id == id && d.IsActive, cancellationToken);
}
