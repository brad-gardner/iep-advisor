using System.Threading.Channels;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class IepProcessingQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int documentId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(documentId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public class IepProcessingWorker : BackgroundService
{
    private readonly IepProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IepProcessingWorker> _logger;

    public IepProcessingWorker(
        IepProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<IepProcessingWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IEP Processing Worker started");

        await foreach (var documentId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing document {DocumentId}", documentId);

                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IIepProcessingService>();
                await processingService.ProcessDocumentAsync(documentId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document {DocumentId}", documentId);
            }
        }
    }
}
