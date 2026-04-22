using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Data;

namespace IepAssistant.Domain.Repositories;

public interface IEtrSectionRepository : IRepository<EtrSection>
{
    Task<IEnumerable<EtrSection>> GetByDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default);
    Task DeleteAllByDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default);
}

public class EtrSectionRepository : Repository<EtrSection>, IEtrSectionRepository
{
    public EtrSectionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<EtrSection>> GetByDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(s => s.EtrDocumentId == etrDocumentId)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task DeleteAllByDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbSet.Where(s => s.EtrDocumentId == etrDocumentId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
            _dbSet.RemoveRange(existing);
    }
}
