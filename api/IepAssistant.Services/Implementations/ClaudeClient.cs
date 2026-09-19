using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Function = Anthropic.SDK.Common.Function;
using Tool = Anthropic.SDK.Common.Tool;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ClaudeClient : IClaudeClient
{
    /// <summary>
    /// Opens one streaming Messages call. Production binds this to the Anthropic.SDK; tests script
    /// the raw SSE-shaped <see cref="MessageResponse"/> events directly.
    /// </summary>
    internal delegate IAsyncEnumerable<MessageResponse> StreamFactory(MessageParameters parameters, CancellationToken cancellationToken);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnthropicOptions _options;
    private readonly ILogger<ClaudeClient> _logger;
    private readonly StreamFactory _streamFactory;

    public ClaudeClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AnthropicOptions> options,
        ILogger<ClaudeClient> logger)
        : this(httpClientFactory, options, logger, streamFactory: null)
    {
    }

    /// <summary>
    /// Test seam: <paramref name="streamFactory"/> replaces the SDK's
    /// <c>Messages.StreamClaudeMessageAsync</c> so the tool loop can be driven with scripted
    /// stream events. <c>null</c> uses the real SDK.
    /// </summary>
    internal ClaudeClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AnthropicOptions> options,
        ILogger<ClaudeClient> logger,
        StreamFactory? streamFactory)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _streamFactory = streamFactory ?? StreamFromSdk;
    }

    public async Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureApiKeyConfigured();
        var model = _options.Model;
        var client = CreateSdkClient();

        var content = new List<ContentBase>();
        if (request.PdfDocument != null)
        {
            content.Add(new DocumentContent
            {
                Source = new DocumentSource
                {
                    MediaType = "application/pdf",
                    Data = Convert.ToBase64String(request.PdfDocument),
                },
            });
        }

        content.Add(new TextContent
        {
            Text = request.UserText,
        });

        var messages = new List<Message>
        {
            new Message { Role = RoleType.User, Content = content },
        };

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = model,
            MaxTokens = request.MaxTokens,
            System = [new SystemMessage(request.SystemPrompt)],
            Thinking = AdaptiveThinking(),
            OutputConfig = ConfiguredOutput(),
        };

        MessageResponse response;
        try
        {
            response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
        }
        catch (Exception ex) when (ShouldClassify(ex, cancellationToken))
        {
            throw ClassifyException(ex, model);
        }

        // Models with thinking enabled return a thinking block FIRST, so taking the first content
        // block and casting it to TextContent silently yields null. Concatenate every text block.
        var responseText = string.Concat(
            response.Content?.OfType<TextContent>().Select(c => c.Text) ?? []);

        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogError("Claude returned no text content for model {Model}", model);
            throw new ClaudeApiException(ClaudeFailureKind.InvalidResponse);
        }

        return responseText;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A manual tool loop over the SDK's raw streaming events. Each round: stream one model turn
    /// (forwarding text deltas as they arrive), reassemble the assistant message with the SDK's
    /// <c>Message(List&lt;MessageResponse&gt;)</c> constructor so thinking blocks and their
    /// signatures are echoed back exactly, then either finish or execute every requested tool and
    /// send all the results back in ONE user message (the API requires every <c>tool_use</c> in a
    /// turn to be answered together).
    ///
    /// The SDK call is enumerated by hand rather than with <c>await foreach</c> because a C#
    /// iterator cannot <c>yield</c> inside a <c>try</c> that has a <c>catch</c>: only
    /// <c>MoveNextAsync</c> sits in the classifying try/catch, and the deltas are yielded from
    /// outside it, so text still reaches the caller mid-turn instead of only after the turn ends.
    /// </remarks>
    public async IAsyncEnumerable<ClaudeStreamEvent> StreamWithToolsAsync(
        ClaudeToolRequest request,
        IToolExecutor tools,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureApiKeyConfigured();
        var model = _options.Model;

        var messages = request.Messages
            .Select(t => new Message { Role = ParseRole(t.Role), Content = [new TextContent { Text = t.Text }] })
            .ToList();

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = model,
            MaxTokens = request.MaxTokens,
            System = [new SystemMessage(request.SystemPrompt)],
            // Function → Tool is an implicit SDK conversion; the JsonNode overload sends the schema
            // verbatim as input_schema.
            Tools = request.Tools.Select(t => (Tool)new Function(t.Name, t.Description, t.InputSchema)).ToList(),
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Auto },
            // The SDK stamps cache_control on the last system block and the last tool definition,
            // so the frozen persona + tool table is paid for once per conversation, not per round.
            PromptCaching = PromptCacheType.AutomaticToolsAndSystem,
            Thinking = AdaptiveThinking(),
            OutputConfig = ConfiguredOutput(),
            Stream = true,
        };

        var trace = new List<ClaudeToolCallTrace>();
        var inputTokens = 0;
        var outputTokens = 0;

        for (var round = 0; ; round++)
        {
            var outputs = new List<MessageResponse>();
            var turnText = new StringBuilder();
            string? stopReason = null;
            // (id, name) of every tool_use block the stream opened, in order — see the empty-input
            // repair below.
            var openedToolUses = new List<(string Id, string Name)>();

            IAsyncEnumerator<MessageResponse> stream;
            try
            {
                stream = _streamFactory(parameters, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }
            catch (Exception ex) when (ShouldClassify(ex, cancellationToken))
            {
                throw ClassifyException(ex, model);
            }

            await using (stream)
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await stream.MoveNextAsync();
                    }
                    catch (Exception ex) when (ShouldClassify(ex, cancellationToken))
                    {
                        throw ClassifyException(ex, model);
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    var res = stream.Current;
                    outputs.Add(res);

                    // message_start carries the prompt-side usage for this turn; message_delta carries
                    // the stop reason and the output-side usage. Both are per model turn, so they are
                    // summed across rounds.
                    if (res.StreamStartMessage?.Usage is { } startUsage)
                    {
                        inputTokens += startUsage.InputTokens;
                    }

                    if (res.Type == "message_delta")
                    {
                        if (res.Delta?.StopReason is { Length: > 0 } reason)
                        {
                            stopReason = reason;
                        }

                        if (res.Usage is { } deltaUsage)
                        {
                            outputTokens += deltaUsage.OutputTokens;
                        }
                    }

                    if (res.Type == "content_block_start"
                        && res.ContentBlock is { Type: "tool_use", Id: { Length: > 0 } id, Name: { Length: > 0 } name })
                    {
                        openedToolUses.Add((id, name));
                    }

                    if (res.Delta?.Text is { Length: > 0 } text)
                    {
                        turnText.Append(text);
                        yield return new ClaudeStreamEvent(ClaudeStreamEventKind.TextDelta, Text: text);
                    }
                }
            }

            var hitMaxTokens = stopReason == "max_tokens";

            Message? turn;
            try
            {
                turn = new Message(outputs);
            }
            catch (JsonException ex) when (hitMaxTokens)
            {
                // max_tokens landed mid tool_use: the accumulated partial_json is not a complete
                // document, and the SDK's assembler parses it eagerly. There is nothing to execute
                // and nothing further to send, so treat it as a truncated answer rather than a
                // broken response.
                _logger.LogWarning(ex, "Claude hit max_tokens mid tool_use for model {Model}; completing as truncated", model);
                turn = null;
            }
            catch (Exception ex) when (ShouldClassify(ex, cancellationToken))
            {
                throw ClassifyException(ex, model);
            }

            if (turn is null)
            {
                yield return Completed(turnText, trace, round + 1, inputTokens, outputTokens, truncated: true);
                yield break;
            }

            RepairEmptyInputToolUses(turn, openedToolUses);
            messages.Add(turn);

            var toolUses = turn.Content.OfType<ToolUseContent>().ToList();
            if (toolUses.Count == 0)
            {
                if (hitMaxTokens)
                {
                    _logger.LogWarning("Claude hit max_tokens ({MaxTokens}) for model {Model} after {Rounds} round(s)", request.MaxTokens, model, round + 1);
                }

                yield return Completed(turnText, trace, round + 1, inputTokens, outputTokens, truncated: hitMaxTokens);
                yield break;
            }

            if (round >= request.MaxToolRounds)
            {
                // The model wants more tools than the budget allows. Do not run them: the turn's text
                // (if any) is the best answer available, and the caller is told it was cut short.
                _logger.LogWarning(
                    "Claude requested {PendingTools} tool(s) after {MaxToolRounds} round(s) for model {Model}; completing as truncated",
                    toolUses.Count, request.MaxToolRounds, model);
                yield return Completed(turnText, trace, round + 1, inputTokens, outputTokens, truncated: true);
                yield break;
            }

            var results = new List<ContentBase>(toolUses.Count);
            foreach (var use in toolUses)
            {
                yield return new ClaudeStreamEvent(ClaudeStreamEventKind.ToolStarted, ToolName: use.Name, ToolUseId: use.Id);

                // Parsed, never string-matched: the SDK has already turned the streamed partial_json
                // into a JsonNode, and the executor validates the JsonElement against its schema.
                var input = JsonSerializer.SerializeToElement(use.Input ?? new JsonObject());
                var inputChars = input.GetRawText().Length;

                var stopwatch = Stopwatch.StartNew();
                string resultText;
                var isError = false;
                try
                {
                    resultText = await tools.ExecuteAsync(use.Name, input, cancellationToken);
                }
                catch (ToolExecutionException ex)
                {
                    // The one sanctioned failure signal. Its message goes to the model as an is_error
                    // result so it can recover. Anything else the executor throws is a bug in the
                    // toolset (or a genuine cancellation) and propagates out of the stream untouched —
                    // the toolset, not this client, owns catching those.
                    resultText = ex.Message;
                    isError = true;
                }

                stopwatch.Stop();
                trace.Add(new ClaudeToolCallTrace(use.Name, use.Id, inputChars, resultText.Length, stopwatch.ElapsedMilliseconds, isError));
                _logger.LogDebug(
                    "Claude tool {ToolName} ({ToolUseId}) finished in {DurationMs} ms, {ResultChars} chars, error={IsError}",
                    use.Name, use.Id, stopwatch.ElapsedMilliseconds, resultText.Length, isError);

                results.Add(new ToolResultContent
                {
                    ToolUseId = use.Id,
                    IsError = isError ? true : null,
                    Content = [new TextContent { Text = resultText }],
                });

                yield return new ClaudeStreamEvent(ClaudeStreamEventKind.ToolFinished, ToolName: use.Name, ToolUseId: use.Id, ToolIsError: isError);
            }

            messages.Add(new Message { Role = RoleType.User, Content = results });
        }
    }

    private static ClaudeStreamEvent Completed(
        StringBuilder turnText, List<ClaudeToolCallTrace> trace, int rounds, int inputTokens, int outputTokens, bool truncated) =>
        new(
            ClaudeStreamEventKind.Completed,
            FullText: turnText.ToString(),
            Trace: new ClaudeToolTrace(trace.ToArray(), rounds),
            InputTokens: inputTokens,
            OutputTokens: outputTokens,
            Truncated: truncated);

    /// <summary>
    /// The SDK's stream assembler only emits a <see cref="ToolUseContent"/> when the block's
    /// accumulated <c>partial_json</c> is non-blank, so a tool the model calls with no arguments
    /// (its input is <c>{}</c>, streamed as an empty delta) silently vanishes from the assembled
    /// turn — and the next request would then omit the <c>tool_use</c> the API expects a result for.
    /// Re-add any opened tool_use the assembler dropped, with an empty object as its input.
    /// </summary>
    private static void RepairEmptyInputToolUses(Message turn, List<(string Id, string Name)> openedToolUses)
    {
        if (openedToolUses.Count == 0)
        {
            return;
        }

        var present = turn.Content.OfType<ToolUseContent>().Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (id, name) in openedToolUses)
        {
            if (present.Add(id))
            {
                turn.Content.Add(new ToolUseContent { Id = id, Name = name, Input = new JsonObject() });
            }
        }
    }

    private static RoleType ParseRole(string role) => role switch
    {
        "user" => RoleType.User,
        "assistant" => RoleType.Assistant,
        _ => throw new ArgumentException($"Unsupported Claude turn role '{role}'; expected \"user\" or \"assistant\".", nameof(role)),
    };

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogError("Anthropic API key not configured");
            throw new ClaudeApiException(ClaudeFailureKind.Configuration);
        }
    }

    private AnthropicClient CreateSdkClient()
    {
        var httpClient = _httpClientFactory.CreateClient("Claude");
        return new AnthropicClient(_options.ApiKey, httpClient);
    }

    private IAsyncEnumerable<MessageResponse> StreamFromSdk(MessageParameters parameters, CancellationToken cancellationToken) =>
        CreateSdkClient().Messages.StreamClaudeMessageAsync(parameters, cancellationToken);

    // Adaptive thinking is on by default on current models; declare it explicitly so the intent is
    // visible and BudgetTokens (rejected outright by current models) stays null.
    private static ThinkingParameters AdaptiveThinking() => new() { Type = ThinkingType.adaptive };

    // Effort must be set HERE, not on ThinkingParameters.Effort: in Anthropic.SDK 5.10.0 that
    // property is [JsonIgnore] and is only translated to output_config.effort on the
    // Microsoft.Extensions.AI ChatOptions path, so setting it here is what actually reaches the wire.
    // Effort bounds how much of MaxTokens is spent thinking before answer text.
    private OutputConfig ConfiguredOutput() => new() { Effort = _options.Effort };

    /// <summary>
    /// Exception filter shared by every SDK call site. A cancellation the caller asked for falls
    /// through untouched — labelling a graceful deploy restart "took too long" would be a lie
    /// written onto every in-flight run — and everything else is classified by
    /// <see cref="ClassifyException"/>.
    /// </summary>
    private static bool ShouldClassify(Exception ex, CancellationToken cancellationToken) =>
        ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    /// <summary>
    /// Maps an SDK failure onto a <see cref="ClaudeApiException"/>. One helper for both the
    /// non-streaming and streaming paths so the two classifications cannot drift. Only reached
    /// after <see cref="ShouldClassify"/> passed, so an <see cref="OperationCanceledException"/>
    /// here is never a caller-initiated cancellation.
    /// </summary>
    private ClaudeApiException ClassifyException(Exception ex, string model)
    {
        switch (ex)
        {
            case OperationCanceledException:
                // The caller did not cancel, so this is the HttpClient timeout rather than host
                // shutdown.
                _logger.LogError(ex, "Claude call timed out for model {Model}", model);
                return new ClaudeApiException(ClaudeFailureKind.Timeout, ex);

            case AuthenticationException { InnerException: null }:
                // Anthropic.SDK throws this — not HttpRequestException — for a 401, and embeds the whole
                // API response body in the message. Without this arm a bad key would escape unclassified
                // to the caller's broad catch, and that body is never surfaced (only a canned message is).
                //
                // Guarded by `InnerException is null` (todos/P3-01 #1): System.Security.Authentication.
                // AuthenticationException is ALSO the type .NET's TLS stack throws for a handshake
                // failure. HttpClient normally wraps that as HttpRequestException (→ Classify →
                // SecureConnectionError → Transient, correct), but if it were ever to escape unwrapped, a
                // momentary TLS blip would otherwise be misclassified Configuration — suppressing retry
                // and paging as a service-configuration incident for what is actually transient. The
                // SDK's own auth-failure exception is always freshly constructed with no InnerException;
                // a wrapped TLS exception always has one, so this filter tells them apart without relying
                // on the SDK's exact message text. An exception that fails the filter falls through to
                // the default arm below, which still classifies it (as InvalidResponse) rather than
                // dropping it.
                _logger.LogError(ex, "Claude rejected the configured API key for model {Model}", model);
                return new ClaudeApiException(ClaudeFailureKind.Configuration, ex);

            case HttpRequestException http:
            {
                var kind = Classify(http);
                // Log the raw exception (which carries the API's error body) but never surface it:
                // the response payload contains the model id, a request id, and on an auth failure
                // potentially key material, and UserMessage is persisted where parents can read it.
                _logger.LogError(ex, "Claude call failed for model {Model} with kind {Kind}", model, kind);
                return new ClaudeApiException(kind, ex);
            }

            case JsonException:
                _logger.LogError(ex, "Claude returned a body that could not be deserialized for model {Model}", model);
                return new ClaudeApiException(ClaudeFailureKind.InvalidResponse, ex);

            default:
                // todos/P2-04: two more SDK-internal exception types are reachable from
                // GetClaudeMessageAsync and were escaping this typed contract entirely — a
                // NullReferenceException (the SDK foreach-es messageResponse.Content with no null guard,
                // so a 200 with a literal-null or content-less body throws this instead of JsonException),
                // and FormatException/OverflowException (unguarded long.Parse/DateTime.Parse over
                // anthropic-ratelimit-* response headers on an otherwise-successful call). Neither leaks
                // anything (both would otherwise land in a caller's own unclassified catch with no
                // {Kind}), but without this arm they defeat the Kibana-query triage this typed contract
                // exists to enable. Kept last so every more specific arm above still wins. On the
                // streaming path this arm also catches the bare Exception the SDK raises for an SSE
                // `error` event (e.g. overloaded_error mid-stream).
                _logger.LogError(ex, "Claude call raised an unclassified exception for model {Model}", model);
                return new ClaudeApiException(ClaudeFailureKind.InvalidResponse, ex);
        }
    }

    /// <summary>
    /// Classifies an SDK HTTP failure by status code alone (todos/P2-05). The prior version also
    /// parsed the Anthropic error body's <c>error.type</c> into a second, near-fully-redundant
    /// classification layer — <c>ClaudeClientTests.CompleteAsync_ClassifiesApiErrors</c>, flipped so
    /// status and error type disagreed, showed the status code wins whenever the SDK surfaces one, so
    /// that layer was deleted. The one thing it genuinely decided — a 400's context-overflow split —
    /// is preserved below by scanning <paramref name="ex"/>'s own <c>Message</c> directly (which
    /// already carries the raw response body verbatim, per <c>Program.cs</c>'s comment on the "Claude"
    /// HttpClient) instead of re-parsing it as JSON first.
    /// </summary>
    private static ClaudeFailureKind Classify(HttpRequestException ex) => (int?)ex.StatusCode switch
    {
        401 or 403 or 404 => ClaudeFailureKind.Configuration,
        // Context-window overflow arrives as a 400 invalid_request_error, not a 413. It is the most
        // likely user-triggered failure on a large multi-document run, and it must not land in
        // Configuration: that kind suppresses retry and offers no "select fewer documents" guidance,
        // leaving the user at a dead end for something one click would fix. It would also page an
        // oversized document set as a service-configuration incident.
        400 => IsContextOverflow(ex.Message) ? ClaudeFailureKind.RequestTooLarge : ClaudeFailureKind.Configuration,
        413 => ClaudeFailureKind.RequestTooLarge,
        429 => ClaudeFailureKind.RateLimited,
        >= 500 and <= 599 => ClaudeFailureKind.Transient,
        // No status at all — DNS, TLS, proxy, or a refused connection; the request never reached
        // Anthropic, so retrying is exactly the right advice.
        null => ClaudeFailureKind.Transient,
        _ => ClaudeFailureKind.Unknown,
    };

    private static bool IsContextOverflow(string? errorMessage) =>
        errorMessage is not null
        && (errorMessage.Contains("prompt is too long", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("too many tokens", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("exceed context limit", StringComparison.OrdinalIgnoreCase));
}
