using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Pilot-gates plan, phase 1, decision 2: <c>OutboundEmailWorker</c> is the only real sender. Exercises
/// its private <c>ProcessOneAsync</c> directly (mirrors <c>AuthoredDocumentVersionServiceTests</c>'s
/// reflection pattern) so a "retry then Failed" test does not need to wait through real backoff delays
/// (1m/5m/30m/2h/6h) — each call simulates exactly one worker cycle.
/// </summary>
public sealed class OutboundEmailWorkerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public OutboundEmailWorkerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class FakeTransport : IEmailTransport
    {
        public bool ShouldThrow { get; set; }
        public bool IsConfigured { get; set; } = true;
        public int CallCount { get; private set; }
        public string? LastHtml { get; private set; }

        public Task SendAsync(string toEmail, string subject, string htmlBody, string? textBody, IReadOnlyList<OutboundEmailAttachmentDraft>? attachments, CancellationToken ct = default)
        {
            CallCount++;
            LastHtml = htmlBody;
            if (ShouldThrow)
                throw new EmailDeliveryException(toEmail, subject, new InvalidOperationException("simulated ACS outage"));
            return Task.CompletedTask;
        }
    }

    private IServiceProvider BuildProvider(IEmailTransport transport)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        services.AddSingleton(transport);
        return services.BuildServiceProvider();
    }

    private static readonly OutboundEmailWorker WorkerInstance = new(
        new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
        NullLogger<OutboundEmailWorker>.Instance);

    /// <summary>Invokes the worker's private instance method <c>ProcessOneAsync</c> directly, passing
    /// our own <see cref="IServiceProvider"/> in place of a scope — <c>ProcessOneAsync</c> only ever
    /// calls <c>GetRequiredService</c> on whatever is passed, so a root provider works identically to
    /// a real scope for this purpose. <see cref="WorkerInstance"/>'s own scope factory is never used
    /// (the worker's scanning loop is not under test here).</summary>
    private static Task ProcessOneAsync(IServiceProvider provider, int id) =>
        (Task)typeof(OutboundEmailWorker)
            .GetMethod("ProcessOneAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(WorkerInstance, new object[] { provider, id, CancellationToken.None })!;

    /// <summary>Drives one full worker cycle (stale-Sending reclaim, scan, per-row processing) against a
    /// worker whose scope factory resolves the test's own DbContext + transport.</summary>
    private Task RunCycleAsync(IServiceProvider provider)
    {
        var worker = new OutboundEmailWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboundEmailWorker>.Instance);
        return (Task)typeof(OutboundEmailWorker)
            .GetMethod("RunCycleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(worker, new object[] { CancellationToken.None })!;
    }

    private int SeedQueuedEmail(string kind = "Notification", string html = "<p>hi</p>", OutboundEmailStatus status = OutboundEmailStatus.Queued, DateTime? updatedAt = null)
    {
        using var ctx = CreateContext();
        var email = new OutboundEmail
        {
            ToEmail = "parent@example.com",
            Subject = "Test",
            HtmlBody = html,
            Kind = kind,
            Status = status,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };
        ctx.OutboundEmails.Add(email);
        ctx.SaveChanges();
        return email.Id;
    }

    [Fact]
    public async Task ProcessOne_OneTimeLinkEmail_IsRedactedOnceSent()
    {
        var id = SeedQueuedEmail(kind: "MagicLink", html: "<a href=\"https://app/auth/magic?token=SECRET\">Sign in</a>");
        var transport = new FakeTransport { ShouldThrow = false };

        await ProcessOneAsync(BuildProvider(transport), id);

        using var ctx = CreateContext();
        var email = ctx.OutboundEmails.Find(id)!;
        Assert.Equal(OutboundEmailStatus.Sent, email.Status);
        Assert.DoesNotContain("SECRET", email.HtmlBody);          // the live token does not stay at rest
        Assert.True(OutboundEmailKinds.IsRedacted(email));
        Assert.Contains("SECRET", transport.LastHtml ?? "");       // …but it was delivered intact
    }

    [Fact]
    public async Task RunCycle_ReclaimsARowAbandonedInSending()
    {
        var stale = SeedQueuedEmail(status: OutboundEmailStatus.Sending, updatedAt: DateTime.UtcNow.AddMinutes(-30));
        var fresh = SeedQueuedEmail(status: OutboundEmailStatus.Sending, updatedAt: DateTime.UtcNow.AddMinutes(-1));
        var transport = new FakeTransport { ShouldThrow = false };

        await RunCycleAsync(BuildProvider(transport));

        using var ctx = CreateContext();
        Assert.Equal(OutboundEmailStatus.Sent, ctx.OutboundEmails.Find(stale)!.Status);     // re-queued, then delivered in the same cycle
        Assert.Equal(OutboundEmailStatus.Sending, ctx.OutboundEmails.Find(fresh)!.Status);  // a live claim is left alone
    }

    [Fact]
    public async Task ProcessOne_SuccessfulSend_MarksSentAndClearsError()
    {
        var id = SeedQueuedEmail();
        var transport = new FakeTransport { ShouldThrow = false };

        await ProcessOneAsync(BuildProvider(transport), id);

        using var ctx = CreateContext();
        var email = ctx.OutboundEmails.Find(id)!;
        Assert.Equal(OutboundEmailStatus.Sent, email.Status);
        Assert.NotNull(email.SentAt);
        Assert.Null(email.LastError);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ProcessOne_FailedSend_RecordsErrorAndSchedulesRetry_ThenFailsAfterMaxAttempts()
    {
        var id = SeedQueuedEmail();
        var transport = new FakeTransport { ShouldThrow = true };

        // Attempts 1-5: still Queued, with backoff pushing NextAttemptAt into the future. A fresh
        // provider (and so a fresh, un-cached ApplicationDbContext) per call mirrors the real worker,
        // which opens a new IServiceScopeFactory scope per item per cycle — reusing one provider
        // across calls would let EF Core's identity map return the FIRST call's stale tracked entity
        // on every subsequent call instead of re-reading this test's own out-of-band DB edits.
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await ProcessOneAsync(BuildProvider(transport), id);

            using var ctx = CreateContext();
            var email = ctx.OutboundEmails.Find(id)!;
            Assert.Equal(OutboundEmailStatus.Queued, email.Status);
            Assert.Equal(attempt, email.Attempts);
            Assert.Contains("simulated ACS outage", email.LastError);
            Assert.True(email.NextAttemptAt > DateTime.UtcNow);

            // Force the row eligible again immediately so the next loop iteration can retry it —
            // this test intentionally does not wait through real backoff delays.
            email.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
            ctx.SaveChanges();
        }

        // 6th attempt: exhausted — terminal Failed, no further NextAttemptAt scheduling implied.
        await ProcessOneAsync(BuildProvider(transport), id);

        using var finalCtx = CreateContext();
        var final = finalCtx.OutboundEmails.Find(id)!;
        Assert.Equal(OutboundEmailStatus.Failed, final.Status);
        Assert.Equal(6, final.Attempts);
        Assert.Contains("simulated ACS outage", final.LastError);
        Assert.Equal(6, transport.CallCount);
    }

    /// <summary>
    /// "resend re-queues" (AdminController.ResendOutboundEmail resets a Failed row to
    /// Status=Queued/Attempts=0/LastError=null/NextAttemptAt=now): the worker must then actually pick
    /// the row back up and be able to deliver it, not just flip a status flag nobody reads.
    /// </summary>
    [Fact]
    public async Task ResendReset_MakesAFailedRow_PickedUpAndDeliveredByTheWorker()
    {
        var id = SeedQueuedEmail();
        var flakyTransport = new FakeTransport { ShouldThrow = true };
        for (var i = 0; i < 6; i++)
        {
            await ProcessOneAsync(BuildProvider(flakyTransport), id);
            using var ctx = CreateContext();
            var email = ctx.OutboundEmails.Find(id)!;
            if (email.Status == OutboundEmailStatus.Queued)
            {
                email.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
                ctx.SaveChanges();
            }
        }

        using (var verifyCtx = CreateContext())
            Assert.Equal(OutboundEmailStatus.Failed, verifyCtx.OutboundEmails.Find(id)!.Status);

        // The admin "resend" reset, applied exactly as AdminController.ResendOutboundEmail does it.
        using (var resendCtx = CreateContext())
        {
            var email = resendCtx.OutboundEmails.Find(id)!;
            email.Status = OutboundEmailStatus.Queued;
            email.Attempts = 0;
            email.LastError = null;
            email.NextAttemptAt = DateTime.UtcNow;
            resendCtx.SaveChanges();
        }

        var recoveredTransport = new FakeTransport { ShouldThrow = false };
        await ProcessOneAsync(BuildProvider(recoveredTransport), id);

        using var finalCtx = CreateContext();
        var final = finalCtx.OutboundEmails.Find(id)!;
        Assert.Equal(OutboundEmailStatus.Sent, final.Status);
        Assert.Equal(1, recoveredTransport.CallCount);
    }

    [Fact]
    public async Task ProcessOne_RowAlreadyResolvedByAnotherCycle_IsANoOp()
    {
        var id = SeedQueuedEmail();
        using (var ctx = CreateContext())
        {
            var email = ctx.OutboundEmails.Find(id)!;
            email.Status = OutboundEmailStatus.Cancelled; // resolved between scan and claim
            ctx.SaveChanges();
        }

        var transport = new FakeTransport();
        await ProcessOneAsync(BuildProvider(transport), id);

        Assert.Equal(0, transport.CallCount);
        using var verifyCtx = CreateContext();
        Assert.Equal(OutboundEmailStatus.Cancelled, verifyCtx.OutboundEmails.Find(id)!.Status);
    }

    public void Dispose() => _connection.Dispose();
}
