using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;

namespace IepAssistant.Domain.Repositories;

public interface IEtrAnalysisRepository : IRepository<EtrAnalysis>
{
    Task<EtrAnalysis?> GetByDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default);
    Task<EtrAnalysis> UpsertAsync(EtrAnalysis analysis, CancellationToken cancellationToken = default);
}

public class EtrAnalysisRepository : Repository<EtrAnalysis>, IEtrAnalysisRepository
{
    public EtrAnalysisRepository(ApplicationDbContext context) : base(context) { }

    public async Task<EtrAnalysis?> GetByDocumentAsync(int etrDocumentId, CancellationToken cancellationToken = default)
        => await _dbSet.FirstOrDefaultAsync(a => a.EtrDocumentId == etrDocumentId, cancellationToken);

    public async Task<EtrAnalysis> UpsertAsync(EtrAnalysis analysis, CancellationToken cancellationToken = default)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(a => a.EtrDocumentId == analysis.EtrDocumentId, cancellationToken);
        if (existing == null)
        {
            await _dbSet.AddAsync(analysis, cancellationToken);
            return analysis;
        }

        existing.Status = analysis.Status;
        existing.AssessmentCompleteness = analysis.AssessmentCompleteness;
        existing.EligibilityReview = analysis.EligibilityReview;
        existing.OverallRedFlags = analysis.OverallRedFlags;
        existing.SuggestedQuestions = analysis.SuggestedQuestions;
        existing.OverallSummary = analysis.OverallSummary;
        existing.ErrorMessage = analysis.ErrorMessage;
        return existing;
    }
}
