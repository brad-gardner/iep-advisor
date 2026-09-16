using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Runs <see cref="IDigestService.RunForDateAsync"/> once per day at 07:00 in the district time zone
/// (plan 4, decision 3). Computes the delay until the next 07:00 from "now" (rather than a fixed interval)
/// so a late start, a missed tick, or a host restart never causes a double-run or a multi-hour drift.
/// </summary>
public class DigestWorker : BackgroundService
{
    internal const string DistrictTimeZoneId = "America/New_York";
    private static readonly TimeSpan RunAtLocalTime = TimeSpan.FromHours(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigestWorker> _logger;

    public DigestWorker(IServiceScopeFactory scopeFactory, ILogger<DigestWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Digest Worker started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = ComputeDelayUntilNextRun(DateTime.UtcNow);
                await Task.Delay(delay, stoppingToken);
                await RunCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow.
        }
    }

    /// <summary>Next 07:00 in <see cref="DistrictTimeZoneId"/> strictly after <paramref name="utcNow"/>,
    /// expressed as a delay from now (never negative).</summary>
    internal static TimeSpan ComputeDelayUntilNextRun(DateTime utcNow)
    {
        var tz = ResolveTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        var todayRun = nowLocal.Date + RunAtLocalTime;
        var nextLocal = nowLocal < todayRun ? todayRun : todayRun.AddDays(1);
        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(nextLocal, DateTimeKind.Unspecified), tz);
        var delay = nextUtc - utcNow;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DistrictTimeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDigestService>();
            var tz = ResolveTimeZone();
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
            await service.RunForDateAsync(localDate, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Digest cycle failed; skipping.");
        }
    }
}
