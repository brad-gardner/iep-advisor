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

/// <summary>Counts reported by a single <see cref="IAnalysisRunBackfillService.BackfillAsync"/> pass.
/// <paramref name="Updated"/> counts IEP runs rebuilt in place because the legacy row changed since the
/// first backfill, or the run still held the pre-conversion <c>annual_goals</c> array shape (ETR rows are
/// not yet upserted — <c>SkippedExisting</c> covers them as before).</summary>
public sealed record BackfillResult(int Created, int SkippedExisting, int SkippedOrphan, int Updated = 0);
