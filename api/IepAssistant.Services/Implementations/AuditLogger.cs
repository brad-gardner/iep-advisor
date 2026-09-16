using System.Threading.Channels;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <summary>One queued audit event. CreatedAt is stamped at <see cref="AuditLogger.Record"/> time
/// (when the action actually happened), not when the background writer drains it.</summary>
public readonly record struct AuditEntry(
    AuditAction Action,
    int ActorUserId,
    string ResourceType,
    int ResourceId,
    int? RecipientUserId,
    DateTime CreatedAt);

/// <summary>
/// Singleton, fire-and-forget audit writer (P6a). <see cref="Record"/> only does a non-blocking
/// <c>TryWrite</c> onto an unbounded channel — no DbContext, no I/O, no await — so it is safe on any
/// hot read path. A hosted <c>AccessAuditLogWorker</c> consumes <see cref="DequeueAllAsync"/> and
/// performs the real INSERTs in its own DI scope. Writes are drop-safe: if the channel ever refuses
/// (it won't, being unbounded), an audit row is silently lost rather than throwing into the caller.
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly Channel<AuditEntry> _channel = Channel.CreateUnbounded<AuditEntry>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Record(AuditAction action, int actorUserId, string resourceType, int resourceId, int? recipientUserId = null)
    {
        var entry = new AuditEntry(action, actorUserId, resourceType, resourceId, recipientUserId, DateTime.UtcNow);
        _channel.Writer.TryWrite(entry); // drop-safe; never blocks the caller
    }

    public IAsyncEnumerable<AuditEntry> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>Blocks until at least one entry is available (or the channel completes), then reads
    /// everything immediately available up to <paramref name="maxBatchSize"/>. Used by
    /// <c>AccessAuditLogWorker</c> to batch persistence (pilot-gates plan, phase 1). Returns an empty list
    /// only when the channel has completed with nothing left to read.</summary>
    public async Task<List<AuditEntry>> ReadBatchAsync(int maxBatchSize, CancellationToken cancellationToken)
    {
        var batch = new List<AuditEntry>();
        if (!await _channel.Reader.WaitToReadAsync(cancellationToken))
            return batch; // channel completed, nothing left

        while (batch.Count < maxBatchSize && _channel.Reader.TryRead(out var entry))
            batch.Add(entry);

        return batch;
    }

    /// <summary>Non-blocking drain of whatever is immediately sitting in the channel — used on host
    /// shutdown (<c>AccessAuditLogWorker.StopAsync</c>) to sweep up entries the main loop never got to
    /// read before the host's shutdown grace period ended.</summary>
    public List<AuditEntry> DrainImmediately()
    {
        var drained = new List<AuditEntry>();
        while (_channel.Reader.TryRead(out var entry))
            drained.Add(entry);
        return drained;
    }
}
