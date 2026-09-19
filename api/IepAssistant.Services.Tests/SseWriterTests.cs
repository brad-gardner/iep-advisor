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
        // A real `next` is a MoveNextAsync on an enumerable that also observes `ct`, so it completes
        // (here: cancels) once `ct` fires — never a task that hangs forever, which is what a caller's
        // `await using` disposal (racing a live MoveNextAsync) would otherwise force this helper into.
        var next = new TaskCompletionSource<int>();
        using var cts = new CancellationTokenSource();
        cts.Token.Register(() => next.TrySetCanceled(cts.Token));
        cts.CancelAfter(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SseWriter.AwaitWithPingsAsync(new MemoryStream(), next.Task, TimeSpan.FromSeconds(30), cts.Token));
    }

    [Fact]
    public async Task AwaitWithPingsAsync_OnCancellation_AwaitsThePendingNextTask_BeforeThrowing()
    {
        // Reproduces todos/171: cancelling `ct` while `next` (the caller's pending MoveNextAsync) is
        // still outstanding must not throw until `next` is observed — otherwise the caller's
        // `await using` disposes an iterator with a MoveNextAsync in flight, which throws
        // NotSupportedException from the compiler-generated state machine.
        var next = new TaskCompletionSource<int>();
        using var cts = new CancellationTokenSource();
        var pending = SseWriter.AwaitWithPingsAsync(new MemoryStream(), next.Task, TimeSpan.FromMilliseconds(10), cts.Token);

        cts.Cancel();
        await Task.Delay(50);
        Assert.False(pending.IsCompleted, "must keep awaiting the pending next task before unwinding on cancellation");

        // `next` finally settles with its own (unrelated) fault — proving the helper actually observed
        // it (swallowing this) rather than abandoning it, and still reports cancellation, not this.
        next.TrySetException(new InvalidOperationException("the iterator's own failure — never the reported error"));
        var ex = await Record.ExceptionAsync(() => pending);

        Assert.IsType<OperationCanceledException>(ex);
    }

    private sealed class ThrowingWriteStream : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("simulated half-closed socket");
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new IOException("simulated half-closed socket");
    }

    [Fact]
    public async Task AwaitWithPingsAsync_OnPingWriteFault_AwaitsThePendingNextTask_BeforePropagating()
    {
        // Reproduces todos/222: a ping write can fault (e.g. IOException on a half-closed socket) before
        // `ct` ever fires. Before the fix, only OperationCanceledException triggered observing the pending
        // `next` task, so a ping-write fault re-created the todos/171 disposal race: the caller's
        // `await using` would dispose an iterator whose MoveNextAsync is still in flight.
        var stream = new ThrowingWriteStream();
        var next = new TaskCompletionSource<int>();

        var pending = SseWriter.AwaitWithPingsAsync(stream, next.Task, TimeSpan.FromMilliseconds(10), CancellationToken.None);

        await Task.Delay(50);
        Assert.False(pending.IsCompleted, "must keep awaiting the pending next task before propagating the ping fault");

        // `next` finally settles normally — proving the helper actually observed it (swallowing this
        // result) rather than abandoning it, and still reports the original IOException, not this.
        next.TrySetResult(7);
        var ex = await Record.ExceptionAsync(() => pending);

        Assert.IsType<IOException>(ex);
    }
}
