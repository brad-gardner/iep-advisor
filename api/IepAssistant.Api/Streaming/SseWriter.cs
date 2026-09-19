using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IepAssistant.Api.Streaming;

/// <summary>
/// Server-sent-events framing (<c>event: name\ndata: json\n\n</c>) over a raw stream, flushed per frame so a
/// proxy or the client sees each event as soon as it is written. JSON is camelCase with nulls omitted;
/// System.Text.Json escapes line breaks inside strings, so a frame's data is always a single line.
/// </summary>
public static class SseWriter
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly byte[] Ping = Encoding.UTF8.GetBytes(": ping\n\n");

    public static async Task WriteEventAsync(Stream output, string eventName, object payload, CancellationToken ct)
    {
        if (eventName.Any(c => c is '\n' or '\r'))
            throw new ArgumentException("Event names cannot contain line breaks.", nameof(eventName));

        var frame = $"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, Json)}\n\n";
        await output.WriteAsync(Encoding.UTF8.GetBytes(frame), ct);
        await output.FlushAsync(ct);
    }

    /// <summary>A comment frame that keeps intermediaries from timing out an idle stream; clients ignore it.</summary>
    public static async Task WritePingAsync(Stream output, CancellationToken ct)
    {
        await output.WriteAsync(Ping, ct);
        await output.FlushAsync(ct);
    }

    /// <summary>
    /// Awaits <paramref name="next"/>, writing a ping every <paramref name="pingInterval"/> while it is still
    /// pending. Returns the awaited result.
    /// </summary>
    /// <remarks>
    /// <paramref name="next"/> is typically a caller's pending <c>MoveNextAsync()</c> on an async iterator.
    /// If anything in the ping loop faults while <paramref name="next"/> is still pending — <paramref name="ct"/>
    /// being cancelled, or a ping write itself failing (e.g. <see cref="IOException"/> on a half-closed
    /// socket, which can happen before <paramref name="ct"/> ever fires) — this method still observes
    /// <paramref name="next"/> (swallowing whatever it completes with; the original fault is the error to
    /// report, not that) before rethrowing, so <see langword="await using"/> disposal back in the caller
    /// never races a still-in-flight <c>MoveNextAsync</c> (which would throw <see cref="NotSupportedException"/>
    /// from the compiler-generated state machine).
    /// </remarks>
    public static async Task<T> AwaitWithPingsAsync<T>(Stream output, Task<T> next, TimeSpan pingInterval, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var delay = Task.Delay(pingInterval, delayCts.Token);
                var completed = await Task.WhenAny(next, delay);
                if (completed == next)
                {
                    delayCts.Cancel();
                    return await next;
                }
                ct.ThrowIfCancellationRequested();
                await WritePingAsync(output, ct);
            }
        }
        catch
        {
            if (!next.IsCompleted)
            {
                try { await next; }
                catch { /* swallowed: we are already unwinding via the original fault, not whatever `next` completed with */ }
            }
            throw;
        }
    }
}
