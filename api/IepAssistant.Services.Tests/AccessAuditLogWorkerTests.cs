using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Pilot-gates plan, phase 1: durability (a batch that cannot be persisted is staged into
/// <see cref="PendingAuditEvent"/>, and a staged row is replayed into a real, hashed
/// <see cref="AccessAuditLog"/> row on the next start) and the hash chain itself. Exercises
/// <see cref="AccessAuditLogWorker"/>'s private methods directly via reflection (mirrors
/// <c>AuthoredDocumentVersionServiceTests</c>'s pattern) rather than driving the full retry-with-backoff
/// loop, which would otherwise make a "batch failure" test take 36 real seconds (1s+5s+30s).
/// </summary>
public sealed class AccessAuditLogWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public AccessAuditLogWorkerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private IServiceScopeFactory CreateScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static AccessAuditLogWorker CreateWorker(IServiceScopeFactory scopeFactory) =>
        new(new AuditLogger(), scopeFactory, NullLogger<AccessAuditLogWorker>.Instance);

    private static Task InvokePrivateAsync(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(target, args)!;
    }

    private static AuditEntry Entry(int resourceId, DateTime? createdAt = null) =>
        new(AuditAction.View, 42, "SmokeTest", resourceId, null, createdAt ?? DateTime.UtcNow);

    [Fact]
    public async Task StageBatchAsPending_WritesOneRowPerEntry()
    {
        var worker = CreateWorker(CreateScopeFactory());
        var batch = new List<AuditEntry> { Entry(1), Entry(2), Entry(3) };

        await InvokePrivateAsync(worker, "StageBatchAsPendingAsync", batch);

        using var ctx = CreateContext();
        var pending = ctx.PendingAuditEvents.OrderBy(p => p.ResourceId).ToList();
        Assert.Equal(3, pending.Count);
        Assert.Equal(new[] { 1, 2, 3 }, pending.Select(p => p.ResourceId));
        Assert.All(pending, p => Assert.Equal(AuditAction.View, p.ActionValue));
        Assert.All(pending, p => Assert.Equal(42, p.ActorUserId));
        // Nothing was written to AccessAuditLogs — staging is the fallback when persisting there failed.
        Assert.Empty(ctx.AccessAuditLogs.ToList());
    }

    [Fact]
    public async Task ReplayPendingEvents_TurnsStagedRowsIntoHashedAccessAuditLogs_AndDeletesThePendingRows()
    {
        using (var seedCtx = CreateContext())
        {
            seedCtx.PendingAuditEvents.AddRange(
                new PendingAuditEvent { ActionValue = AuditAction.View, ActorUserId = 1, ResourceType = "R", ResourceId = 10, OccurredAt = DateTime.UtcNow },
                new PendingAuditEvent { ActionValue = AuditAction.Edit, ActorUserId = 2, ResourceType = "R", ResourceId = 11, OccurredAt = DateTime.UtcNow });
            seedCtx.SaveChanges();
        }

        var worker = CreateWorker(CreateScopeFactory());
        await InvokePrivateAsync(worker, "ReplayPendingEventsAsync", CancellationToken.None);

        using var ctx = CreateContext();
        Assert.Empty(ctx.PendingAuditEvents.ToList());

        var rows = ctx.AccessAuditLogs.OrderBy(a => a.Id).ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.NotNull(r.Hash));
        Assert.Null(rows[0].PrevHash); // first row in a fresh chain
        Assert.Equal(rows[0].Hash, rows[1].PrevHash); // chained to the first

        var expectedHash0 = AuditHashChain.ComputeHash(
            rows[0].Id, rows[0].Action, rows[0].ActorUserId, rows[0].ResourceType, rows[0].ResourceId, rows[0].RecipientUserId, rows[0].CreatedAt, null);
        Assert.Equal(expectedHash0, rows[0].Hash);
    }

    [Fact]
    public async Task ReplayPendingEvents_ContinuesChain_FromExistingLiveRows()
    {
        // A live AccessAuditLog row already exists (e.g. inserted before the crash that produced the
        // pending rows) — the replay must chain off its Hash, not start a new chain from null.
        string existingHash;
        using (var seedCtx = CreateContext())
        {
            var existing = new AccessAuditLog { Action = AuditAction.View, ActorUserId = 1, ResourceType = "R", ResourceId = 1, CreatedAt = DateTime.UtcNow };
            seedCtx.AccessAuditLogs.Add(existing);
            seedCtx.SaveChanges();
            existingHash = AuditHashChain.ComputeHash(existing.Id, existing.Action, existing.ActorUserId, existing.ResourceType, existing.ResourceId, existing.RecipientUserId, existing.CreatedAt, null);
            existing.Hash = existingHash;
            seedCtx.SaveChanges();

            seedCtx.PendingAuditEvents.Add(new PendingAuditEvent { ActionValue = AuditAction.Edit, ActorUserId = 2, ResourceType = "R", ResourceId = 2, OccurredAt = DateTime.UtcNow });
            seedCtx.SaveChanges();
        }

        var worker = CreateWorker(CreateScopeFactory());
        // Chain tip must be resolved first (mirrors ExecuteAsync's real startup order).
        await InvokePrivateAsync(worker, "BackfillHashesAsync");
        await InvokePrivateAsync(worker, "ReplayPendingEventsAsync", CancellationToken.None);

        using var ctx = CreateContext();
        var replayed = ctx.AccessAuditLogs.Single(a => a.ResourceId == 2);
        Assert.Equal(existingHash, replayed.PrevHash);
    }

    [Fact]
    public async Task BackfillHashes_ComputesChainForHistoricalNullHashRows_InIdOrder()
    {
        using (var seedCtx = CreateContext())
        {
            seedCtx.AccessAuditLogs.AddRange(
                new AccessAuditLog { Action = AuditAction.View, ActorUserId = 1, ResourceType = "R", ResourceId = 1, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new AccessAuditLog { Action = AuditAction.Edit, ActorUserId = 2, ResourceType = "R", ResourceId = 2, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc) });
            seedCtx.SaveChanges();
        }

        var worker = CreateWorker(CreateScopeFactory());
        await InvokePrivateAsync(worker, "BackfillHashesAsync");

        using var ctx = CreateContext();
        var rows = ctx.AccessAuditLogs.OrderBy(a => a.Id).ToList();
        Assert.All(rows, r => Assert.NotNull(r.Hash));
        Assert.Null(rows[0].PrevHash);
        Assert.Equal(rows[0].Hash, rows[1].PrevHash);
    }

    public void Dispose() => _connection.Dispose();
}
