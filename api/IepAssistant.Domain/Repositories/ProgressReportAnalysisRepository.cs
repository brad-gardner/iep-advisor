using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;

namespace IepAssistant.Domain.Repositories;

public interface IProgressReportAnalysisRepository : IRepository<ProgressReportAnalysis>
{
    Task<ProgressReportAnalysis?> GetByProgressReportIdAsync(int progressReportId, CancellationToken cancellationToken = default);
}

public class ProgressReportAnalysisRepository : Repository<ProgressReportAnalysis>, IProgressReportAnalysisRepository
{
    public ProgressReportAnalysisRepository(ApplicationDbContext context) : base(context) { }

    public async Task<ProgressReportAnalysis?> GetByProgressReportIdAsync(int progressReportId, CancellationToken cancellationToken = default)
        => await _dbSet
            .Where(a => a.ProgressReportId == progressReportId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
}
