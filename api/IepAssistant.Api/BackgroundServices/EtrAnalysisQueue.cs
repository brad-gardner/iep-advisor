using System.Threading.Channels;

namespace IepAssistant.Api.BackgroundServices;

public class EtrAnalysisQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public async ValueTask EnqueueAsync(int etrDocumentId, CancellationToken cancellationToken = default)
        => await _channel.Writer.WriteAsync(etrDocumentId, cancellationToken);

    public IAsyncEnumerable<int> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
