namespace IepAssistant.Services.Interfaces;

/// <summary>
/// One-off, idempotent, resumable migration of legacy <c>IepAnalysis</c> / <c>EtrAnalysis</c>
/// rows into single-source <c>AnalysisRun</c> rows. Safe to run on every boot: rows already
/// backfilled (matched by <c>AnalysisRun.BackfillSourceKey</c>) are skipped.
/// </summary>
public interface IAnalysisRunBackfillService
{
    Task<BackfillResult> BackfillAsync(CancellationToken ct = default);
}

/// <summary>Counts reported by a single <see cref="IAnalysisRunBackfillService.BackfillAsync"/> pass.</summary>
public sealed record BackfillResult(int Created, int SkippedExisting, int SkippedOrphan);
