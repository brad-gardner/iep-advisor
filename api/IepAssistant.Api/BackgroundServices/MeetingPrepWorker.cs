using System.Threading.Channels;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Api.BackgroundServices;

public class MeetingPrepQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int checklistId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(checklistId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

public class MeetingPrepWorker : BackgroundService
{
    private readonly MeetingPrepQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingPrepWorker> _logger;

    public MeetingPrepWorker(
        MeetingPrepQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MeetingPrepWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meeting Prep Worker started");

        await foreach (var checklistId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Generating meeting prep checklist {ChecklistId}", checklistId);

                using var scope = _scopeFactory.CreateScope();
                var meetingPrepService = scope.ServiceProvider.GetRequiredService<IMeetingPrepService>();
                await meetingPrepService.GenerateChecklistAsync(checklistId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating meeting prep checklist {ChecklistId}", checklistId);
            }
        }
    }
}
