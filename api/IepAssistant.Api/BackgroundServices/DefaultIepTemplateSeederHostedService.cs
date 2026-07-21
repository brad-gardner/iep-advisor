using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

/// <summary>
/// Seeds the DEFAULT IEP template ONCE at startup (Phase 5, State Document Template Engine). The seed is
/// idempotent — it skips when a default IEP template already exists and treats a concurrent-insert race as
/// a no-op — so running on every boot is safe and cheap. Mirrors <see cref="AnalysisRunBackfillHostedService"/>:
/// resolves a scoped service, wraps in try/catch so a failure never takes down host startup, and can be
/// disabled via <c>Seed:DefaultIepTemplateEnabled</c> (defaults to true when the key is absent).
/// </summary>
public class DefaultIepTemplateSeederHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultIepTemplateSeederHostedService> _logger;

    public DefaultIepTemplateSeederHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DefaultIepTemplateSeederHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue("Seed:DefaultIepTemplateEnabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Default IEP template seed disabled via Seed:DefaultIepTemplateEnabled=false; skipping");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IDefaultIepTemplateSeeder>();
            var result = await seeder.SeedAsync(stoppingToken);

            _logger.LogInformation("Default IEP template seed finished: {Outcome}", result.Outcome);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Default IEP template seed canceled during shutdown");
        }
        catch (Exception ex)
        {
            // Never let the seed take down host startup; it retries on next boot (idempotent).
            _logger.LogError(ex, "Default IEP template seed failed; it will retry on next boot (idempotent)");
        }
    }
}
