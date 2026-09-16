using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Plain-language, parent-advocate-framed explanation of a whole shared revision (plan 6, decision 3).
/// One Claude call per revision, cached forever in <c>SharedDraftExplanation</c> — never regenerated.
/// Runs on the exact frozen revision, never the heavy AnalysisRun pipeline.
/// </summary>
public interface IDraftExplanationService
{
    /// <summary>Returns the cached explanation, generating it via Claude on the first call. Usage is recorded against the district, never the parent's subscription.</summary>
    Task<ServiceResult<DraftExplanationModel>> GetOrGenerateAsync(int parentUserId, int revisionId, CancellationToken ct = default);
}
