using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Data;

namespace IepAssistant.Domain.Repositories;

public interface IProgressReportRepository : IRepository<ProgressReport>
{
    Task<IEnumerable<ProgressReport>> GetByIepDocumentIdAsync(int iepDocumentId, CancellationToken cancellationToken = default);
    Task<ProgressReport?> GetByIdWithIepAsync(int id, CancellationToken cancellationToken = default);
}

public class ProgressReportRepository : Repository<ProgressReport>, IProgressReportRepository
{
    public ProgressReportRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProgressReport>> GetByIepDocumentIdAsync(int iepDocumentId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(p => p.IepDocumentId == iepDocumentId && p.IsActive)
            .OrderByDescending(p => p.ReportingPeriodEnd ?? p.UploadDate)
            .ToListAsync(cancellationToken);

    public async Task<ProgressReport?> GetByIdWithIepAsync(int id, CancellationToken cancellationToken = default)
        => await _dbSet
            .Include(p => p.IepDocument)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);
}
