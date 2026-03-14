using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
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

        // Requeue checklists stuck in pending/generating from previous runs
        await RequeueStuckChecklistsAsync();

        await foreach (var checklistId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Generating meeting prep checklist {ChecklistId}", checklistId);

                using var scope = _scopeFactory.CreateScope();
                var meetingPrepService = scope.ServiceProvider.GetRequiredService<IMeetingPrepService>();
                await meetingPrepService.GenerateChecklistAsync(checklistId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Meeting Prep Worker shutting down, checklist {ChecklistId} will be retried on next startup", checklistId);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating meeting prep checklist {ChecklistId}", checklistId);
            }
        }
    }

    private async Task RequeueStuckChecklistsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var stuckChecklists = await context.MeetingPrepChecklists
                .Where(c => (c.Status == "pending" || c.Status == "generating") && c.IsActive)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var id in stuckChecklists)
            {
                _logger.LogInformation("Requeuing stuck meeting prep checklist {ChecklistId}", id);
                await _queue.EnqueueAsync(id);
            }

            if (stuckChecklists.Count > 0)
                _logger.LogInformation("Requeued {Count} stuck meeting prep checklists", stuckChecklists.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requeuing stuck meeting prep checklists");
        }
    }
}
