using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Verifies the real (non-fake) <see cref="AuditLogger"/>: Record(...) only enqueues (no DB touch),
/// and draining the channel — exactly what the AccessAuditLogWorker does — yields the entry and
/// persists an AccessAuditLog row. This exercises the fire-and-forget queue end-to-end without the
/// hosted service.
/// </summary>
public sealed class AuditLoggerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public AuditLoggerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task Record_Enqueues_AndDrainPersistsRow()
    {
        var logger = new AuditLogger();
        var stampedBefore = DateTime.UtcNow;

        logger.Record(AuditAction.View, actorUserId: 7, resourceType: "IepVersion", resourceId: 42);

        // Drain exactly what the worker would: read queued entries off the stream, then cancel to stop
        // the otherwise-infinite await foreach (the cancellation surfaces as OperationCanceledException).
        using var cts = new CancellationTokenSource();
        var drained = new List<AuditEntry>();
        try
        {
            await foreach (var entry in logger.DequeueAllAsync(cts.Token))
            {
                drained.Add(entry);
                cts.Cancel(); // single expected entry — stop reading
            }
        }
        catch (OperationCanceledException)
        {
            // expected: we cancelled the stream after draining the queued entry
        }

        var queued = Assert.Single(drained);
        Assert.Equal(AuditAction.View, queued.Action);
        Assert.Equal(7, queued.ActorUserId);
        Assert.Equal("IepVersion", queued.ResourceType);
        Assert.Equal(42, queued.ResourceId);
        Assert.Null(queued.RecipientUserId);
        Assert.True(queued.CreatedAt >= stampedBefore); // stamped at Record time

        // Worker-side insert.
        using var ctx = new ApplicationDbContext(_options);
        ctx.AccessAuditLogs.Add(new AccessAuditLog
        {
            Action = queued.Action,
            ActorUserId = queued.ActorUserId,
            ResourceType = queued.ResourceType,
            ResourceId = queued.ResourceId,
            RecipientUserId = queued.RecipientUserId,
            CreatedAt = queued.CreatedAt
        });
        await ctx.SaveChangesAsync();

        var row = Assert.Single(ctx.AccessAuditLogs.ToList());
        Assert.Equal(AuditAction.View, row.Action);
        Assert.Equal("IepVersion", row.ResourceType);
        Assert.Equal(42, row.ResourceId);
    }

    public void Dispose() => _connection.Dispose();
}
