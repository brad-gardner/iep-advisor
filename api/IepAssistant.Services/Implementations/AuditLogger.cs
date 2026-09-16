using System.Threading.Channels;
using Microsoft.Extensions.Logging;
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
/// performs the real INSERTs in its own DI scope.
///
/// <para>The channel is <b>bounded</b> (<see cref="Capacity"/>): during a database outage the worker
/// cannot persist or even stage events, so an unbounded queue would grow until the process died and
/// then lose everything. Instead the newest event is refused once the backlog is full — the refusal is
/// counted (<see cref="DroppedCount"/>) and logged at Error so the gap is visible, and the pending
/// backlog is bounded to what a restart can lose. <c>Record</c> never blocks or throws into the caller.</para>
/// </summary>
public class AuditLogger : IAuditLogger
{
    /// <summary>Worst-case in-memory backlog (~ a few minutes of peak traffic); the worker drains
    /// batches of 200 with retry, so a healthy database never approaches it.</summary>
    public const int Capacity = 20_000;

    private readonly Channel<AuditEntry> _channel = Channel.CreateBounded<AuditEntry>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }); // Wait: TryWrite returns false when full (never blocks); DropWrite would report success while discarding
    private readonly ILogger<AuditLogger>? _logger;
    private long _dropped;

    public AuditLogger(ILogger<AuditLogger>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>Audit events refused because the backlog was full — an operator signal, never silent.</summary>
    public long DroppedCount => Interlocked.Read(ref _dropped);

    /// <summary>Entries currently waiting to be persisted.</summary>
    public int Depth => _channel.Reader.Count;

    public void Record(AuditAction action, int actorUserId, string resourceType, int resourceId, int? recipientUserId = null)
    {
        var entry = new AuditEntry(action, actorUserId, resourceType, resourceId, recipientUserId, DateTime.UtcNow);
        if (_channel.Writer.TryWrite(entry))
            return;
        var dropped = Interlocked.Increment(ref _dropped);
        if (dropped == 1 || dropped % 1000 == 0)
            _logger?.LogError("Audit backlog full ({Capacity}); refused {Action} on {ResourceType}/{ResourceId} by user {ActorUserId} — {Dropped} event(s) dropped so far",
                Capacity, action, resourceType, resourceId, actorUserId, dropped);
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
