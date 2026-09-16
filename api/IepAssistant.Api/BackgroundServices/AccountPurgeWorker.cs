using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Hourly sweep for accounts whose 30-day deletion grace period has elapsed (pilot-gates plan, phase 2,
/// decision 3). Mirrors <c>NotificationEmailWorker</c>'s shape: a scan for candidate ids in one scope,
/// then each id purged in its own scope so one bad account can never crash the loop or block the rest.
/// </summary>
public class AccountPurgeWorker : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountPurgeWorker> _logger;

    public AccountPurgeWorker(IServiceScopeFactory scopeFactory, ILogger<AccountPurgeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Account Purge Worker started");

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

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<int> candidateIds;
        try
        {
            using var scanScope = _scopeFactory.CreateScope();
            var context = scanScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-AccountService.DeletionGraceDays);
            candidateIds = await context.Users.AsNoTracking()
                .Where(u => u.DeletionRequestedAt != null && u.DeletionRequestedAt <= cutoff)
                .Select(u => u.Id)
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Account purge scan failed; skipping this cycle.");
            return;
        }

        if (candidateIds.Count == 0)
            return;

        foreach (var id in candidateIds)
        {
            stoppingToken.ThrowIfCancellationRequested();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IAccountPurgeService>();
                await service.PurgeAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed purge must never crash the worker or block the rest of the batch — it
                // remains eligible and is retried on the next hourly cycle.
                _logger.LogError(ex, "Failed to purge account {UserId}.", id);
            }
        }
    }
}
