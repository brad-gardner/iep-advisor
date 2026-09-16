using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Pilot-gates plan, phase 1: the nightly (and on-demand) hash-chain integrity walk.</summary>
public sealed class AuditIntegrityServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public AuditIntegrityServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class CapturingNotificationService : INotificationService
    {
        public List<(IEnumerable<int> UserIds, NotificationKind Kind)> Calls { get; } = new();

        public Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, string title, string body, string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default)
        {
            Calls.Add((userIds, kind));
            return Task.CompletedTask;
        }

        public Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    /// <summary>Seeds a valid 3-row chain and returns the rows (Id order).</summary>
    private List<AccessAuditLog> SeedValidChain()
    {
        using var ctx = CreateContext();
        var rows = new List<AccessAuditLog>
        {
            new() { Action = AuditAction.View, ActorUserId = 1, ResourceType = "R", ResourceId = 1, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new() { Action = AuditAction.Edit, ActorUserId = 2, ResourceType = "R", ResourceId = 2, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc) },
            new() { Action = AuditAction.Share, ActorUserId = 3, ResourceType = "R", ResourceId = 3, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc) },
        };
        ctx.AccessAuditLogs.AddRange(rows);
        ctx.SaveChanges();

        string? tip = null;
        foreach (var row in rows)
        {
            var hash = AuditHashChain.ComputeHash(row.Id, row.Action, row.ActorUserId, row.ResourceType, row.ResourceId, row.RecipientUserId, row.CreatedAt, tip);
            row.PrevHash = tip;
            row.Hash = hash;
            tip = hash;
        }
        ctx.SaveChanges();
        return rows;
    }

    [Fact]
    public async Task RunCheck_ValidChain_ReturnsOk()
    {
        var rows = SeedValidChain();
        var notifications = new CapturingNotificationService();
        using var ctx = CreateContext();
        var service = new AuditIntegrityService(ctx, notifications, NullLogger<AuditIntegrityService>.Instance);

        var result = await service.RunCheckAsync();

        Assert.Equal("Ok", result.Status);
        Assert.Equal(rows.Count, result.RowsChecked);
        Assert.Null(result.FirstBrokenId);
        Assert.Empty(notifications.Calls);
    }

    [Fact]
    public async Task RunCheck_TamperedRow_ReturnsBroken_WithFirstBrokenId_AndNotifiesAdmins()
    {
        var rows = SeedValidChain();
        var tamperedId = rows[1].Id;

        using (var tamperCtx = CreateContext())
        {
            // Tamper with the middle row directly (bypassing the app/worker) — its stored Hash no
            // longer matches what its (now-different) fields would recompute to.
            var row = tamperCtx.AccessAuditLogs.Single(a => a.Id == tamperedId);
            row.ActorUserId = 999;
            tamperCtx.SaveChanges();

            tamperCtx.Users.Add(new User { Email = "admin@example.com", PasswordHash = "x", FirstName = "A", LastName = "D", Role = UserRole.Admin, IsActive = true });
            tamperCtx.SaveChanges();
        }

        var notifications = new CapturingNotificationService();
        using var ctx = CreateContext();
        var service = new AuditIntegrityService(ctx, notifications, NullLogger<AuditIntegrityService>.Instance);

        var result = await service.RunCheckAsync();

        Assert.Equal("Broken", result.Status);
        Assert.Equal(tamperedId, result.FirstBrokenId);
        Assert.Single(notifications.Calls);
        Assert.Equal(NotificationKind.AuditIntegrityBroken, notifications.Calls[0].Kind);
    }

    [Fact]
    public async Task RunCheck_NoAdmins_StillReturnsBroken_ButSendsNoNotification()
    {
        var rows = SeedValidChain();
        using (var tamperCtx = CreateContext())
        {
            var row = tamperCtx.AccessAuditLogs.Single(a => a.Id == rows[0].Id);
            row.ResourceId = 12345;
            tamperCtx.SaveChanges();
        }

        var notifications = new CapturingNotificationService();
        using var ctx = CreateContext();
        var service = new AuditIntegrityService(ctx, notifications, NullLogger<AuditIntegrityService>.Instance);

        var result = await service.RunCheckAsync();

        Assert.Equal("Broken", result.Status);
        Assert.Empty(notifications.Calls); // no Admin users exist in this scenario
    }

    [Fact]
    public async Task GetRecentRuns_ReturnsNewestFirst_UpToCount()
    {
        var notifications = new CapturingNotificationService();
        using var ctx = CreateContext();
        var service = new AuditIntegrityService(ctx, notifications, NullLogger<AuditIntegrityService>.Instance);

        await service.RunCheckAsync();
        await service.RunCheckAsync();
        await service.RunCheckAsync();

        var recent = await service.GetRecentRunsAsync(2);
        Assert.Equal(2, recent.Count);
        Assert.True(recent[0].Id > recent[1].Id);
    }

    public void Dispose() => _connection.Dispose();
}
