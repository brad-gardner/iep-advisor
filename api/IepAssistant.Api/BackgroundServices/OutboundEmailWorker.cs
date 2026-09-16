using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Drains <see cref="OutboundEmail"/> rows queued by <see cref="IEmailService"/> and actually sends them
/// via <see cref="IEmailTransport"/> (pilot-gates plan, phase 1, decision 2). Mirrors
/// <c>NotificationEmailWorker</c>'s shape: a scan for due candidate ids in one scope, then each id
/// claimed and processed in its own scope so one bad row can never crash the loop or block the batch.
///
/// SINGLE-INSTANCE ASSUMPTION: claiming (Queued → Sending) is a plain read-then-write with no row lock,
/// which prevents a double-send only within one instance — same caveat as every other worker in this
/// pattern (see <c>NotificationEmailWorker</c>). Acceptable for a single-App-Service-instance pilot.
/// </summary>
public class OutboundEmailWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 50;
    private const int MaxAttempts = 6;
    private const int MaxErrorLength = 1000;

    /// <summary>Backoff after attempt 1..5 respectively (index = attempts-1). The 6th failed attempt has
    /// no further backoff — the row goes terminal (Failed) instead.</summary>
    private static readonly TimeSpan[] RetryBackoffs =
    {
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2), TimeSpan.FromHours(6)
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboundEmailWorker> _logger;

    public OutboundEmailWorker(IServiceScopeFactory scopeFactory, ILogger<OutboundEmailWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbound Email Worker started");

        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await RunCycleAsync(stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
    }

    /// <summary>A row left in Sending this long was abandoned mid-send (a deploy stopped the host
    /// between the claim and the final save) — hand it back to the queue for another attempt.</summary>
    private static readonly TimeSpan StaleSendingAfter = TimeSpan.FromMinutes(10);

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<int> candidateIds;
        try
        {
            using var scanScope = _scopeFactory.CreateScope();
            var context = scanScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;

            var staleCutoff = now - StaleSendingAfter;
            var reclaimed = await context.OutboundEmails
                .Where(e => e.Status == OutboundEmailStatus.Sending && e.UpdatedAt <= staleCutoff)
                .ExecuteUpdateAsync(u => u
                    .SetProperty(e => e.Status, OutboundEmailStatus.Queued)
                    .SetProperty(e => e.NextAttemptAt, now)
                    .SetProperty(e => e.UpdatedAt, now), stoppingToken);
            if (reclaimed > 0)
                _logger.LogWarning("Re-queued {Count} outbound email(s) abandoned in Sending", reclaimed);

            candidateIds = await context.OutboundEmails.AsNoTracking()
                .Where(e => e.Status == OutboundEmailStatus.Queued && e.NextAttemptAt <= now)
                .OrderBy(e => e.NextAttemptAt)
                .Take(BatchSize)
                .Select(e => e.Id)
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Outbound email scan failed; skipping this cycle.");
            return;
        }

        foreach (var id in candidateIds)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await ProcessOneAsync(scope.ServiceProvider, id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process outbound email {OutboundEmailId}.", id);
            }
        }
    }

    private async Task ProcessOneAsync(IServiceProvider services, int id, CancellationToken ct)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        var email = await context.OutboundEmails.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (email == null || email.Status != OutboundEmailStatus.Queued || email.NextAttemptAt > DateTime.UtcNow)
            return; // a concurrent cycle or admin action already resolved this row

        email.Status = OutboundEmailStatus.Sending;
        email.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct); // claim

        var transport = services.GetRequiredService<IEmailTransport>();
        try
        {
            var attachments = OutboundEmailQueue.DecodeAttachments(email.AttachmentsJson);
            await transport.SendAsync(email.ToEmail, email.Subject, email.HtmlBody, email.TextBody, attachments, ct);

            email.Status = OutboundEmailStatus.Sent;
            email.SentAt = DateTime.UtcNow;
            email.LastError = null;
            email.Attempts++;
        }
        catch (OperationCanceledException)
        {
            // Host shutdown mid-send: hand the row straight back rather than leaving it in Sending
            // (the stale-Sending reclaim above is the backstop if even this save cannot run).
            email.Status = OutboundEmailStatus.Queued;
            email.NextAttemptAt = DateTime.UtcNow;
            email.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (EmailDeliveryException ex)
        {
            email.Attempts++;
            email.LastError = Truncate(ex.InnerException?.Message ?? ex.Message);

            if (email.Attempts >= MaxAttempts)
            {
                email.Status = OutboundEmailStatus.Failed;
                _logger.LogError(ex, "Outbound email {OutboundEmailId} failed permanently after {Attempts} attempt(s)", email.Id, email.Attempts);
            }
            else
            {
                email.Status = OutboundEmailStatus.Queued;
                email.NextAttemptAt = DateTime.UtcNow + RetryBackoffs[email.Attempts - 1];
                _logger.LogWarning(ex, "Outbound email {OutboundEmailId} send attempt {Attempt}/{MaxAttempts} failed; retrying", email.Id, email.Attempts, MaxAttempts);
            }
        }

        email.UpdatedAt = DateTime.UtcNow;
        RedactSecretsIfTerminal(email);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Emails that carry a one-time link (sign-in, password reset, cancel-deletion, invites) must not
    /// keep the live secret at rest once they reach a terminal state: the body is replaced by a marker.
    /// Such rows cannot be re-sent; the admin page says to request a fresh link instead.
    /// </summary>
    internal static void RedactSecretsIfTerminal(OutboundEmail email)
    {
        if (email.Status is not (OutboundEmailStatus.Sent or OutboundEmailStatus.Failed or OutboundEmailStatus.Cancelled))
            return;
        if (!OutboundEmailKinds.CarriesOneTimeSecret(email.Kind))
            return;
        email.HtmlBody = OutboundEmailKinds.RedactedBody;
        email.TextBody = null;
    }

    private static string Truncate(string message) => message.Length <= MaxErrorLength ? message : message[..MaxErrorLength];
}
