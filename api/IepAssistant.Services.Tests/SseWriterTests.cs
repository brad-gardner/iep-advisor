using System.Text;
using IepAssistant.Api.Streaming;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>The SSE frame writer used by the advocate message endpoint: one event/data pair per frame, camelCase JSON on one line, flushed.</summary>
public class SseWriterTests
{
    private sealed class FlushCountingStream : MemoryStream
    {
        public int Flushes { get; private set; }
        public override Task FlushAsync(CancellationToken cancellationToken) { Flushes++; return base.FlushAsync(cancellationToken); }
    }

    [Fact]
    public async Task WriteEventAsync_WritesEventAndCamelCaseJsonOnOneLine_ThenBlankLine_AndFlushes()
    {
        var stream = new FlushCountingStream();

        await SseWriter.WriteEventAsync(stream, "delta", new { Text = "line one\nline two", Nothing = (string?)null, Kind = ConsoleColor.Red }, CancellationToken.None);

        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("event: delta\ndata: {\"text\":\"line one\\nline two\",\"kind\":\"Red\"}\n\n", text);
        Assert.Equal(1, stream.Flushes);
    }

    [Fact]
    public async Task WriteEventAsync_RejectsEventNamesWithLineBreaks()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => SseWriter.WriteEventAsync(new MemoryStream(), "done\nevent: evil", new { }, CancellationToken.None));
    }

    [Fact]
    public async Task WritePingAsync_WritesACommentFrame()
    {
        var stream = new FlushCountingStream();
        await SseWriter.WritePingAsync(stream, CancellationToken.None);
        Assert.Equal(": ping\n\n", Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(1, stream.Flushes);
    }

    [Fact]
    public async Task AwaitWithPingsAsync_PingsWhileWaiting_ThenReturnsTheResult()
    {
        var stream = new FlushCountingStream();
        var gate = new TaskCompletionSource<int>();
        var pending = SseWriter.AwaitWithPingsAsync(stream, gate.Task, TimeSpan.FromMilliseconds(20), CancellationToken.None);

        await Task.Delay(120);
        gate.SetResult(42);

        Assert.Equal(42, await pending);
        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains(": ping\n\n", text);
        Assert.True(stream.Flushes >= 2, $"expected several pings, got {stream.Flushes}");
    }

    [Fact]
    public async Task AwaitWithPingsAsync_ReturnsImmediatelyWhenAlreadyComplete_WithoutPinging()
    {
        var stream = new FlushCountingStream();
        var result = await SseWriter.AwaitWithPingsAsync(stream, Task.FromResult("done"), TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Equal("done", result);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task AwaitWithPingsAsync_HonoursCancellation()
    {
        using var cts = new CancellationTokenSource(50);
        var never = new TaskCompletionSource<int>().Task;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SseWriter.AwaitWithPingsAsync(new MemoryStream(), never, TimeSpan.FromSeconds(30), cts.Token));
    }
}
