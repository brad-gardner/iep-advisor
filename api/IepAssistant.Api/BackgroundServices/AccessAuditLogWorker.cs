using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Drains the singleton <see cref="AuditLogger"/> channel and persists each queued event as an
/// <see cref="AccessAuditLog"/> row (P6a). This is the only place audit rows are written, keeping the
/// INSERT off every read path. Each item runs in its own DI scope with a fresh DbContext and is wrapped
/// in try/catch: a single failed audit write is logged and skipped, never crashing the worker or
/// affecting the originating request (which already returned long ago).
/// </summary>
public class AccessAuditLogWorker : BackgroundService
{
    private readonly AuditLogger _auditLogger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccessAuditLogWorker> _logger;

    public AccessAuditLogWorker(
        AuditLogger auditLogger,
        IServiceScopeFactory scopeFactory,
        ILogger<AccessAuditLogWorker> logger)
    {
        _auditLogger = auditLogger;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Access Audit Log Worker started");

        await foreach (var entry in _auditLogger.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                context.AccessAuditLogs.Add(new AccessAuditLog
                {
                    Action = entry.Action,
                    ActorUserId = entry.ActorUserId,
                    ResourceType = entry.ResourceType,
                    ResourceId = entry.ResourceId,
                    RecipientUserId = entry.RecipientUserId,
                    CreatedAt = entry.CreatedAt
                });
                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Audit failure must never crash anything — log and continue with the next entry.
                _logger.LogError(ex,
                    "Failed to persist access audit log entry ({Action} {ResourceType}:{ResourceId} by user {ActorUserId})",
                    entry.Action, entry.ResourceType, entry.ResourceId, entry.ActorUserId);
            }
        }
    }
}
