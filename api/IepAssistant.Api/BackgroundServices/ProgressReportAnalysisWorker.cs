using System.Threading.Channels;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class ProgressReportAnalysisQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int progressReportId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(progressReportId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public class ProgressReportAnalysisWorker : BackgroundService
{
    private readonly ProgressReportAnalysisQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProgressReportAnalysisWorker> _logger;

    public ProgressReportAnalysisWorker(
        ProgressReportAnalysisQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ProgressReportAnalysisWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Progress Report Analysis Worker started");

        await foreach (var progressReportId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Analyzing progress report {Id}", progressReportId);
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IProgressReportAnalysisService>();
                await service.AnalyzeAsync(progressReportId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing progress report {Id}", progressReportId);
            }
        }
    }
}
