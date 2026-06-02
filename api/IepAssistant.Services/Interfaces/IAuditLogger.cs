using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// FERPA-aligned access logging (P6a). <see cref="Record"/> is synchronous and non-blocking: it only
/// enqueues an in-memory entry — it never touches the DbContext on the caller's thread/scope, so it is
/// cheap and safe to inject and call from any service or controller on a hot read path. A background
/// writer drains the queue and performs the actual INSERT out-of-band (fire-and-forget). Because it is
/// a stateless, thread-safe singleton, audit failures can never crash, slow, or roll back a caller.
/// </summary>
public interface IAuditLogger
{
    void Record(AuditAction action, int actorUserId, string resourceType, int resourceId, int? recipientUserId = null);
}
