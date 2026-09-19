using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Drives <see cref="ClaudeClient.StreamWithToolsAsync"/> through its internal stream seam with
/// scripted SSE-shaped <see cref="MessageResponse"/> events, exactly as Anthropic.SDK 5.10.0's
/// <c>StreamClaudeMessageAsync</c> forwards them: a <c>message_start</c> (prompt usage), then per
/// content block a <c>content_block_start</c> / <c>content_block_delta</c>s / <c>content_block_stop</c>,
/// then a <c>message_delta</c> carrying the stop reason and output usage. The SDK's
/// <c>Message(List&lt;MessageResponse&gt;)</c> constructor consumes that same shape to rebuild the
/// assistant turn, so these fakes exercise the real assembler.
/// </summary>
public class ClaudeClientToolLoopTests
{
    // --- Stream event builders ---

    private static MessageResponse Start(int inputTokens = 10) => new()
    {
        Type = "message_start",
        StreamStartMessage = new StreamMessage { Usage = new Usage { InputTokens = inputTokens } },
    };

    private static IEnumerable<MessageResponse> TextBlock(params string[] deltas)
    {
        yield return new MessageResponse { Type = "content_block_start", ContentBlock = new ContentBlock { Type = "text", Text = "" } };
        foreach (var delta in deltas)
        {
            yield return new MessageResponse { Type = "content_block_delta", Delta = new Delta { Type = "text_delta", Text = delta } };
        }

        yield return new MessageResponse { Type = "content_block_stop" };
    }

    private static IEnumerable<MessageResponse> ToolUseBlock(string id, string name, params string[] partialJson)
    {
        yield return new MessageResponse
        {
            Type = "content_block_start",
            ContentBlock = new ContentBlock { Type = "tool_use", Id = id, Name = name },
        };
        foreach (var chunk in partialJson)
        {
            yield return new MessageResponse { Type = "content_block_delta", Delta = new Delta { Type = "input_json_delta", PartialJson = chunk } };
        }

        yield return new MessageResponse { Type = "content_block_stop" };
    }

    private static MessageResponse Stop(string stopReason, int outputTokens = 5) => new()
    {
        Type = "message_delta",
        Delta = new Delta { StopReason = stopReason },
        Usage = new Usage { OutputTokens = outputTokens },
    };

    private static List<MessageResponse> Turn(string stopReason, params IEnumerable<MessageResponse>[] blocks)
    {
        var events = new List<MessageResponse> { Start() };
        foreach (var block in blocks)
        {
            events.AddRange(block);
        }

        events.Add(Stop(stopReason));
        return events;
    }

    private static List<MessageResponse> TextTurn(params string[] deltas) => Turn("end_turn", TextBlock(deltas));

    // --- Scripted stream seam ---

    /// <summary>
    /// Hands back one scripted turn per call and snapshots the request's message list at call time
    /// (the client mutates the same list between rounds, so a live reference would show later turns).
    /// </summary>
    private sealed class ScriptedStreams
    {
        private readonly Queue<Func<IAsyncEnumerable<MessageResponse>>> _turns = new();
        public List<MessageParameters> Parameters { get; } = [];
        public List<List<Message>> MessagesPerCall { get; } = [];

        public ScriptedStreams Then(List<MessageResponse> events)
        {
            _turns.Enqueue(() => Replay(events));
            return this;
        }

        public ScriptedStreams ThenThrow(Exception exception)
        {
            _turns.Enqueue(() => Throw(exception));
            return this;
        }

        public IAsyncEnumerable<MessageResponse> Open(MessageParameters parameters, CancellationToken cancellationToken)
        {
            Parameters.Add(parameters);
            MessagesPerCall.Add(parameters.Messages.ToList());
            if (_turns.Count == 0)
            {
                throw new InvalidOperationException("Stream requested more turns than were scripted");
            }

            return _turns.Dequeue()();
        }

        private static async IAsyncEnumerable<MessageResponse> Replay(
            List<MessageResponse> events, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var e in events)
            {
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                yield return e;
            }
        }

        // Thrown from inside the iterator, i.e. on the first MoveNextAsync, which is where the real
        // SDK raises HTTP failures.
        private static async IAsyncEnumerable<MessageResponse> Throw(Exception exception)
        {
            await Task.Yield();
            throw exception;
#pragma warning disable CS0162 // unreachable — needed so the method is an iterator
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class ScriptedExecutor : IToolExecutor
    {
        private readonly Func<string, JsonElement, string> _handler;
        public List<(string Name, string Input)> Calls { get; } = [];

        public ScriptedExecutor(Func<string, JsonElement, string> handler) => _handler = handler;

        public static ScriptedExecutor Returning(string result) => new((_, _) => result);

        public Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken)
        {
            Calls.Add((toolName, input.GetRawText()));
            return Task.FromResult(_handler(toolName, input));
        }
    }

    private sealed class UnusedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("The SDK must not be reached through the test seam");
    }

    private static ClaudeClient BuildClient(ScriptedStreams streams, string apiKey = "test-key") =>
        new(
            new UnusedHttpClientFactory(),
            Options.Create(new AnthropicOptions { ApiKey = apiKey, Model = "claude-opus-5", Effort = ThinkingEffort.high }),
            NullLogger<ClaudeClient>.Instance,
            streams.Open);

    private static ClaudeToolRequest Request(int maxToolRounds = 6, int maxTokens = 2048) => new()
    {
        SystemPrompt = "You are a test.",
        Messages =
        [
            new ClaudeTurn("user", "earlier question"),
            new ClaudeTurn("assistant", "earlier answer"),
            new ClaudeTurn("user", "What is PWN?"),
        ],
        Tools =
        [
            new ClaudeToolDefinition("search_kb", "Search the knowledge base", JsonNode.Parse("""{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""")!),
            new ClaudeToolDefinition("get_child_summary", "Summarise the child", JsonNode.Parse("""{"type":"object","properties":{}}""")!),
        ],
        MaxTokens = maxTokens,
        MaxToolRounds = maxToolRounds,
    };

    private static async Task<List<ClaudeStreamEvent>> Collect(ClaudeClient client, ClaudeToolRequest request, IToolExecutor tools)
    {
        var events = new List<ClaudeStreamEvent>();
        await foreach (var e in client.StreamWithToolsAsync(request, tools))
        {
            events.Add(e);
        }

        return events;
    }

    private static ClaudeStreamEvent CompletedOf(List<ClaudeStreamEvent> events)
    {
        var last = events[^1];
        Assert.Equal(ClaudeStreamEventKind.Completed, last.Kind);
        Assert.Single(events, e => e.Kind == ClaudeStreamEventKind.Completed);
        return last;
    }

    // --- (a) text-only turn ---

    [Fact]
    public async Task TextOnlyTurn_EmitsDeltasThenCompleted()
    {
        var streams = new ScriptedStreams().Then(TextTurn("Prior ", "written ", "notice."));
        var executor = ScriptedExecutor.Returning("unused");

        var events = await Collect(BuildClient(streams), Request(), executor);

        var deltas = events.TakeWhile(e => e.Kind == ClaudeStreamEventKind.TextDelta).Select(e => e.Text).ToList();
        Assert.Equal(["Prior ", "written ", "notice."], deltas);
        var completed = CompletedOf(events);
        Assert.Equal(4, events.Count);
        Assert.Equal("Prior written notice.", completed.FullText);
        Assert.False(completed.Truncated);
        Assert.Equal(10, completed.InputTokens);
        Assert.Equal(5, completed.OutputTokens);
        Assert.NotNull(completed.Trace);
        Assert.Empty(completed.Trace.Calls);
        Assert.Equal(1, completed.Trace.Rounds);
        Assert.Empty(executor.Calls);
    }

    [Fact]
    public async Task BuildsParameters_WithToolsCachingThinkingAndStreamFlag()
    {
        var streams = new ScriptedStreams().Then(TextTurn("ok"));

        await Collect(BuildClient(streams), Request(maxTokens: 4321), ScriptedExecutor.Returning("unused"));

        var p = Assert.Single(streams.Parameters);
        Assert.Equal("claude-opus-5", p.Model);
        Assert.Equal(4321, p.MaxTokens);
        Assert.True(p.Stream);
        Assert.Equal(ToolChoiceType.Auto, p.ToolChoice.Type);
        Assert.Equal(PromptCacheType.AutomaticToolsAndSystem, p.PromptCaching);
        Assert.Equal(ThinkingType.adaptive, p.Thinking.Type);
        Assert.Equal(ThinkingEffort.high, p.OutputConfig.Effort);
        Assert.Equal(["search_kb", "get_child_summary"], p.Tools.Select(t => t.Function.Name));
        Assert.Equal("You are a test.", Assert.Single(p.System).Text);

        var messages = streams.MessagesPerCall[0];
        Assert.Equal([RoleType.User, RoleType.Assistant, RoleType.User], messages.Select(m => m.Role));
        Assert.Equal("What is PWN?", Assert.IsType<TextContent>(Assert.Single(messages[2].Content)).Text);
    }

    // --- (b) tool_use → result → end_turn ---

    [Fact]
    public async Task ToolUseTurn_ExecutesTool_AndSendsAssistantTurnPlusOneResultMessage()
    {
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use", TextBlock("Let me check. "), ToolUseBlock("toolu_1", "search_kb", """{"query":""", """ "prior written notice"}""")))
            .Then(TextTurn("PWN is a notice."));
        var executor = ScriptedExecutor.Returning("""{"results":[{"title":"PWN"}]}""");

        var events = await Collect(BuildClient(streams), Request(), executor);

        // Executor saw the parsed JSON input, not a string fragment.
        var call = Assert.Single(executor.Calls);
        Assert.Equal("search_kb", call.Name);
        Assert.Equal("prior written notice", JsonDocument.Parse(call.Input).RootElement.GetProperty("query").GetString());

        // Event order: delta, ToolStarted, ToolFinished, delta, Completed.
        Assert.Equal(
            [ClaudeStreamEventKind.TextDelta, ClaudeStreamEventKind.ToolStarted, ClaudeStreamEventKind.ToolFinished, ClaudeStreamEventKind.TextDelta, ClaudeStreamEventKind.Completed],
            events.Select(e => e.Kind));
        Assert.Equal("toolu_1", events[1].ToolUseId);
        Assert.Equal("search_kb", events[1].ToolName);
        Assert.False(events[2].ToolIsError);

        // Second request: original 3 turns + assistant tool_use turn + ONE user tool_result message.
        Assert.Equal(2, streams.MessagesPerCall.Count);
        var second = streams.MessagesPerCall[1];
        Assert.Equal(5, second.Count);
        var assistantTurn = second[3];
        Assert.Equal(RoleType.Assistant, assistantTurn.Role);
        Assert.Equal("Let me check. ", Assert.IsType<TextContent>(assistantTurn.Content[0]).Text);
        var use = Assert.IsType<ToolUseContent>(assistantTurn.Content[1]);
        Assert.Equal("toolu_1", use.Id);
        Assert.Equal("prior written notice", use.Input!["query"]!.GetValue<string>());
        var resultMessage = second[4];
        Assert.Equal(RoleType.User, resultMessage.Role);
        var result = Assert.IsType<ToolResultContent>(Assert.Single(resultMessage.Content));
        Assert.Equal("toolu_1", result.ToolUseId);
        Assert.NotEqual(true, result.IsError);
        Assert.Equal("""{"results":[{"title":"PWN"}]}""", Assert.IsType<TextContent>(Assert.Single(result.Content)).Text);

        var completed = CompletedOf(events);
        Assert.Equal("PWN is a notice.", completed.FullText);
        Assert.False(completed.Truncated);
        Assert.Equal(20, completed.InputTokens);
        Assert.Equal(10, completed.OutputTokens);
        Assert.Equal(2, completed.Trace!.Rounds);
        var traced = Assert.Single(completed.Trace.Calls);
        Assert.Equal(("search_kb", "toolu_1", false), (traced.Name, traced.ToolUseId, traced.IsError));
        Assert.Equal("""{"results":[{"title":"PWN"}]}""".Length, traced.ResultChars);
        Assert.True(traced.InputChars > 0);
    }

    // --- (c) two tool_use blocks in one turn ---

    [Fact]
    public async Task TwoToolUsesInOneTurn_BothExecutedInOrder_ResultsInOneUserMessage()
    {
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use",
                ToolUseBlock("toolu_a", "search_kb", """{"query":"a"}"""),
                ToolUseBlock("toolu_b", "get_child_summary", """{"detail":"full"}""")))
            .Then(TextTurn("Done."));
        var executor = new ScriptedExecutor((name, _) => $"result-for-{name}");

        var events = await Collect(BuildClient(streams), Request(), executor);

        Assert.Equal(["search_kb", "get_child_summary"], executor.Calls.Select(c => c.Name));
        Assert.Equal(
            ["toolu_a", "toolu_a", "toolu_b", "toolu_b"],
            events.Where(e => e.Kind is ClaudeStreamEventKind.ToolStarted or ClaudeStreamEventKind.ToolFinished).Select(e => e.ToolUseId));

        var second = streams.MessagesPerCall[1];
        Assert.Equal(5, second.Count);
        var results = second[4].Content.Cast<ToolResultContent>().ToList();
        Assert.Equal(["toolu_a", "toolu_b"], results.Select(r => r.ToolUseId));
        Assert.Equal("result-for-search_kb", Assert.IsType<TextContent>(results[0].Content[0]).Text);
        Assert.Equal("result-for-get_child_summary", Assert.IsType<TextContent>(results[1].Content[0]).Text);
        Assert.Equal(2, CompletedOf(events).Trace!.Calls.Count);
    }

    [Fact]
    public async Task ToolUseWithEmptyInput_StillExecuted_WithEmptyObject()
    {
        // The API streams a no-argument tool call as a tool_use block whose only input_json_delta is
        // "" — which the SDK's assembler drops entirely. The client must still run it and echo the
        // tool_use back, or the next request is rejected for an unanswered tool call.
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use", ToolUseBlock("toolu_empty", "get_child_summary", "")))
            .Then(TextTurn("Summary."));
        var executor = ScriptedExecutor.Returning("{}");

        var events = await Collect(BuildClient(streams), Request(), executor);

        var call = Assert.Single(executor.Calls);
        Assert.Equal("get_child_summary", call.Name);
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(call.Input).RootElement.ValueKind);
        var assistantTurn = streams.MessagesPerCall[1][3];
        var use = Assert.IsType<ToolUseContent>(Assert.Single(assistantTurn.Content));
        Assert.Equal("toolu_empty", use.Id);
        Assert.Equal("toolu_empty", Assert.IsType<ToolResultContent>(Assert.Single(streams.MessagesPerCall[1][4].Content)).ToolUseId);
        Assert.False(CompletedOf(events).Truncated);
    }

    // --- (d) ToolExecutionException → is_error, loop continues ---

    [Fact]
    public async Task ToolExecutionException_BecomesIsErrorResult_AndLoopContinues()
    {
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use", ToolUseBlock("toolu_1", "search_kb", """{"query":"x"}""")))
            .Then(TextTurn("I could not check that."));
        var executor = new ScriptedExecutor((_, _) => throw new ToolExecutionException("Not found"));

        var events = await Collect(BuildClient(streams), Request(), executor);

        var finished = Assert.Single(events, e => e.Kind == ClaudeStreamEventKind.ToolFinished);
        Assert.True(finished.ToolIsError);
        var result = Assert.IsType<ToolResultContent>(Assert.Single(streams.MessagesPerCall[1][4].Content));
        Assert.True(result.IsError);
        Assert.Equal("Not found", Assert.IsType<TextContent>(Assert.Single(result.Content)).Text);

        var completed = CompletedOf(events);
        Assert.Equal("I could not check that.", completed.FullText);
        Assert.False(completed.Truncated);
        Assert.True(Assert.Single(completed.Trace!.Calls).IsError);
    }

    // --- (e) MaxToolRounds reached ---

    [Fact]
    public async Task MaxToolRoundsReached_CompletesTruncated_WithoutExecutingPendingTools()
    {
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use", ToolUseBlock("toolu_1", "search_kb", """{"query":"first"}""")))
            .Then(Turn("tool_use", TextBlock("One more look. "), ToolUseBlock("toolu_2", "search_kb", """{"query":"second"}""")));
        var executor = ScriptedExecutor.Returning("{}");

        var events = await Collect(BuildClient(streams), Request(maxToolRounds: 1), executor);

        // Only the first round's tool ran; the second turn's tool was never executed.
        Assert.Equal(["first"], executor.Calls.Select(c => JsonDocument.Parse(c.Input).RootElement.GetProperty("query").GetString()));
        Assert.DoesNotContain(events, e => e.ToolUseId == "toolu_2");
        Assert.Equal(2, streams.Parameters.Count);

        var completed = CompletedOf(events);
        Assert.True(completed.Truncated);
        Assert.Equal("One more look. ", completed.FullText);
        Assert.Equal(2, completed.Trace!.Rounds);
        Assert.Single(completed.Trace.Calls);
    }

    [Fact]
    public async Task MaxTokensStopReason_CompletesTruncated()
    {
        var streams = new ScriptedStreams().Then(Turn("max_tokens", TextBlock("This answer was cut o")));

        var events = await Collect(BuildClient(streams), Request(), ScriptedExecutor.Returning("unused"));

        var completed = CompletedOf(events);
        Assert.True(completed.Truncated);
        Assert.Equal("This answer was cut o", completed.FullText);
    }

    [Fact]
    public async Task MaxTokensMidToolInput_CompletesTruncated_InsteadOfThrowing()
    {
        // partial_json is incomplete when max_tokens lands inside a tool_use block; the SDK's
        // assembler would throw JsonException on it.
        var streams = new ScriptedStreams()
            .Then(Turn("max_tokens", TextBlock("Checking. "), ToolUseBlock("toolu_cut", "search_kb", """{"query":"unfini""")));
        var executor = ScriptedExecutor.Returning("unused");

        var events = await Collect(BuildClient(streams), Request(), executor);

        Assert.Empty(executor.Calls);
        var completed = CompletedOf(events);
        Assert.True(completed.Truncated);
        Assert.Equal("Checking. ", completed.FullText);
    }

    // --- (f) API failures classified like CompleteAsync ---

    [Fact]
    public async Task StreamThrows429_SurfacesAsRateLimited()
    {
        var streams = new ScriptedStreams()
            .ThenThrow(new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => Collect(BuildClient(streams), Request(), ScriptedExecutor.Returning("unused")));

        Assert.Equal(ClaudeFailureKind.RateLimited, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.RateLimited, ex.UserMessage);
    }

    [Fact]
    public async Task StreamThrows_OnSecondRound_AfterDeltasWereDelivered_SurfacesAsTransient()
    {
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use", TextBlock("Looking. "), ToolUseBlock("toolu_1", "search_kb", """{"query":"x"}""")))
            .ThenThrow(new HttpRequestException("overloaded", null, HttpStatusCode.ServiceUnavailable));
        var delivered = new List<ClaudeStreamEvent>();

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(async () =>
        {
            await foreach (var e in BuildClient(streams).StreamWithToolsAsync(Request(), ScriptedExecutor.Returning("{}")))
            {
                delivered.Add(e);
            }
        });

        Assert.Equal(ClaudeFailureKind.Transient, ex.Kind);
        Assert.Equal("Looking. ", Assert.Single(delivered, e => e.Kind == ClaudeStreamEventKind.TextDelta).Text);
        Assert.DoesNotContain(delivered, e => e.Kind == ClaudeStreamEventKind.Completed);
    }

    [Fact]
    public async Task MissingApiKey_ThrowsConfiguration_BeforeOpeningAStream()
    {
        var streams = new ScriptedStreams().Then(TextTurn("never"));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => Collect(BuildClient(streams, apiKey: ""), Request(), ScriptedExecutor.Returning("unused")));

        Assert.Equal(ClaudeFailureKind.Configuration, ex.Kind);
        Assert.Empty(streams.Parameters);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesAsOperationCanceled_NotTimeout()
    {
        var streams = new ScriptedStreams().Then(TextTurn("a", "b", "c"));
        using var cts = new CancellationTokenSource();
        var client = BuildClient(streams);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var e in client.StreamWithToolsAsync(Request(), ScriptedExecutor.Returning("unused"), cts.Token))
            {
                if (e.Kind == ClaudeStreamEventKind.TextDelta)
                {
                    cts.Cancel();
                }
            }
        });
    }

    // --- (g) non-ToolExecutionException from the executor propagates ---

    [Fact]
    public async Task ExecutorThrowsOtherException_PropagatesUnwrapped()
    {
        // Contract: the toolset owns catching everything except ToolExecutionException. A bug in a
        // tool must surface as that bug, not be relabelled a Claude failure or fed back to the model.
        var streams = new ScriptedStreams()
            .Then(Turn("tool_use", ToolUseBlock("toolu_1", "search_kb", """{"query":"x"}""")))
            .Then(TextTurn("never reached"));
        var executor = new ScriptedExecutor((_, _) => throw new InvalidOperationException("toolset bug"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Collect(BuildClient(streams), Request(), executor));

        Assert.Equal("toolset bug", ex.Message);
        Assert.Single(streams.Parameters);
    }

    [Fact]
    public async Task UnknownTurnRole_ThrowsArgumentException()
    {
        var request = new ClaudeToolRequest
        {
            SystemPrompt = "s",
            Messages = [new ClaudeTurn("system", "nope")],
            Tools = [],
        };

        await Assert.ThrowsAsync<ArgumentException>(() => Collect(BuildClient(new ScriptedStreams()), request, ScriptedExecutor.Returning("unused")));
    }
}
