using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Runs the legacy-analysis backfill ONCE at startup. The backfill is idempotent (it skips rows
/// already migrated by their unique <c>BackfillSourceKey</c>), so running on every boot is safe and
/// cheap once the data is migrated. Controlled by <c>Backfill:AnalysisRunsEnabled</c>, which
/// defaults to true when the key is absent.
/// </summary>
public class AnalysisRunBackfillHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnalysisRunBackfillHostedService> _logger;

    public AnalysisRunBackfillHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AnalysisRunBackfillHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Default to enabled when the key is absent.
        var enabled = _configuration.GetValue("Backfill:AnalysisRunsEnabled", true);
        if (!enabled)
        {
            _logger.LogInformation("AnalysisRun backfill disabled via Backfill:AnalysisRunsEnabled=false; skipping");
            return;
        }

        try
        {
            _logger.LogInformation("AnalysisRun backfill starting");

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAnalysisRunBackfillService>();
            var result = await service.BackfillAsync(stoppingToken);

            _logger.LogInformation(
                "AnalysisRun backfill finished: Created={Created}, SkippedExisting={SkippedExisting}, SkippedOrphan={SkippedOrphan}",
                result.Created, result.SkippedExisting, result.SkippedOrphan);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("AnalysisRun backfill canceled during shutdown");
        }
        catch (Exception ex)
        {
            // Never let the backfill take down host startup.
            _logger.LogError(ex, "AnalysisRun backfill failed; it will retry on next boot (idempotent)");
        }
    }
}
