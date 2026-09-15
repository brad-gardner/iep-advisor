using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Commit-once guard shared by the importers. A batch is claimed with a conditional UPDATE
/// (<c>Previewed → Committing</c>) that is committed before the row work starts, so two overlapping
/// commits of the same batch (double-click, retry) cannot both pass the status check; the loser sees
/// "already been committed". A claim is released back to <c>Previewed</c> when the commit fails.
/// </summary>
internal static class ImportBatchClaim
{
    public static async Task<bool> TryClaimAsync(ApplicationDbContext context, int batchId, CancellationToken ct)
    {
        var claimed = await context.ImportBatches
            .Where(b => b.Id == batchId && b.Status == ImportBatchStatus.Previewed)
            .ExecuteUpdateAsync(u => u
                .SetProperty(b => b.Status, ImportBatchStatus.Committing)
                .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), ct);
        return claimed == 1;
    }

    /// <summary>Undoes a claim after a failed commit (not cancellable: the reset must land even when the request was aborted).</summary>
    public static Task ReleaseAsync(ApplicationDbContext context, int batchId)
        => context.ImportBatches
            .Where(b => b.Id == batchId && b.Status == ImportBatchStatus.Committing)
            .ExecuteUpdateAsync(u => u
                .SetProperty(b => b.Status, ImportBatchStatus.Previewed)
                .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), CancellationToken.None);
}
