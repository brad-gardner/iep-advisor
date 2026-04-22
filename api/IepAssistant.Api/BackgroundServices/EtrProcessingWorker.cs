using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class EtrProcessingWorker : BackgroundService
{
    private readonly EtrProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EtrProcessingWorker> _logger;

    public EtrProcessingWorker(
        EtrProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EtrProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ETR Processing Worker started");

        await foreach (var etrId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing ETR {EtrId}", etrId);

                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IEtrProcessingService>();
                await processingService.ProcessDocumentAsync(etrId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ETR {EtrId}", etrId);
            }
        }
    }
}
