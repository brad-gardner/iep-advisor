using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class EtrAnalysisWorker : BackgroundService
{
    private readonly EtrAnalysisQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EtrAnalysisWorker> _logger;

    public EtrAnalysisWorker(
        EtrAnalysisQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EtrAnalysisWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ETR Analysis Worker started");

        await foreach (var etrId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Analyzing ETR {EtrId}", etrId);

                using var scope = _scopeFactory.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<IEtrAnalysisService>();
                await analysisService.AnalyzeDocumentAsync(etrId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing ETR {EtrId}", etrId);
            }
        }
    }
}
