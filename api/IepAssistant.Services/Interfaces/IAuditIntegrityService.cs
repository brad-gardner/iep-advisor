using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Nightly (and on-demand) hash-chain walk over <c>AccessAuditLog</c> (pilot-gates plan, phase 1).</summary>
public interface IAuditIntegrityService
{
    /// <summary>Recomputes the hash chain over every hashed row, in Id order, records the outcome as a
    /// new run, and — on a broken chain — notifies every platform admin. Never throws: an unexpected
    /// failure during the walk itself is recorded as a Failed run.</summary>
    Task<AuditIntegrityRunModel> RunCheckAsync(CancellationToken ct = default);

    /// <summary>The most recent runs, newest first.</summary>
    Task<List<AuditIntegrityRunModel>> GetRecentRunsAsync(int count, CancellationToken ct = default);
}
