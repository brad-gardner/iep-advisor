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
}
