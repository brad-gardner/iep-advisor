using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Test double for <see cref="IAuditLogger"/> that captures every Record(...) call into an in-memory
/// list (instead of the real channel) so tests can assert exactly which audit events a service emitted.
/// </summary>
public sealed class CapturingAuditLogger : IAuditLogger
{
    public List<AuditEntry> Entries { get; } = new();

    public void Record(AuditAction action, int actorUserId, string resourceType, int resourceId, int? recipientUserId = null)
        => Entries.Add(new AuditEntry(action, actorUserId, resourceType, resourceId, recipientUserId, DateTime.UtcNow));
}
