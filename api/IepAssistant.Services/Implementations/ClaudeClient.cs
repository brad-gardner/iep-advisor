using System.Security.Authentication;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ClaudeClient : IClaudeClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AnthropicOptions _options;
    private readonly ILogger<ClaudeClient> _logger;

    public ClaudeClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AnthropicOptions> options,
        ILogger<ClaudeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Anthropic API key not configured");
            throw new ClaudeApiException(ClaudeFailureKind.Configuration);
        }

        var model = _options.Model;

        var httpClient = _httpClientFactory.CreateClient("Claude");
        var client = new AnthropicClient(apiKey, httpClient);

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
            // Adaptive thinking is on by default on current models; declare it explicitly so the
            // intent is visible and BudgetTokens (rejected outright by current models) stays null.
            Thinking = new ThinkingParameters
            {
                Type = ThinkingType.adaptive,
            },
            // Effort must be set HERE, not on ThinkingParameters.Effort: in Anthropic.SDK 5.10.0
            // that property is [JsonIgnore] and is only translated to output_config.effort on the
            // Microsoft.Extensions.AI ChatOptions path, so setting it here is what actually reaches
            // the wire. Effort bounds how much of MaxTokens is spent thinking before answer text.
            OutputConfig = new OutputConfig
            {
                Effort = _options.Effort,
            },
        };

        MessageResponse response;
        try
        {
            response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller did not cancel, so this is the HttpClient timeout rather than host
            // shutdown. A cancelled token falls through this filter and propagates untouched —
            // labelling a graceful deploy restart "took too long" would be a lie written onto
            // every in-flight run.
            _logger.LogError(ex, "Claude call timed out for model {Model}", model);
            throw new ClaudeApiException(ClaudeFailureKind.Timeout, ex);
        }
        catch (AuthenticationException ex) when (ex.InnerException is null)
        {
            // Anthropic.SDK throws this — not HttpRequestException — for a 401, and embeds the whole
            // API response body in the message. Without this arm a bad key would escape unclassified
            // to the caller's broad catch, and that body is never surfaced (only a canned message is).
            //
            // Guarded by `ex.InnerException is null` (todos/P3-01 #1): System.Security.Authentication.
            // AuthenticationException is ALSO the type .NET's TLS stack throws for a handshake
            // failure. HttpClient normally wraps that as HttpRequestException (→ Classify →
            // SecureConnectionError → Transient, correct), but if it were ever to escape unwrapped, a
            // momentary TLS blip would otherwise be misclassified Configuration — suppressing retry
            // and paging as a service-configuration incident for what is actually transient. The
            // SDK's own auth-failure exception is always freshly constructed with no InnerException;
            // a wrapped TLS exception always has one, so this filter tells them apart without relying
            // on the SDK's exact message text. An exception that fails the filter falls through to
            // the broad catch below, which still classifies it (as InvalidResponse) rather than
            // dropping it.
            _logger.LogError(ex, "Claude rejected the configured API key for model {Model}", model);
            throw new ClaudeApiException(ClaudeFailureKind.Configuration, ex);
        }
        catch (HttpRequestException ex)
        {
            var kind = Classify(ex);
            // Log the raw exception (which carries the API's error body) but never surface it:
            // the response payload contains the model id, a request id, and on an auth failure
            // potentially key material, and UserMessage is persisted where parents can read it.
            _logger.LogError(ex, "Claude call failed for model {Model} with kind {Kind}", model, kind);
            throw new ClaudeApiException(kind, ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Claude returned a body that could not be deserialized for model {Model}", model);
            throw new ClaudeApiException(ClaudeFailureKind.InvalidResponse, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // todos/P2-04: two more SDK-internal exception types are reachable from
            // GetClaudeMessageAsync and were escaping this typed contract entirely — a
            // NullReferenceException (the SDK foreach-es messageResponse.Content with no null guard,
            // so a 200 with a literal-null or content-less body throws this instead of JsonException),
            // and FormatException/OverflowException (unguarded long.Parse/DateTime.Parse over
            // anthropic-ratelimit-* response headers on an otherwise-successful call). Neither leaks
            // anything (both would otherwise land in a caller's own unclassified catch with no
            // {Kind}), but without this arm they defeat the Kibana-query triage this typed contract
            // exists to enable. Kept last so every more specific arm above still wins.
            //
            // Guarded by `ex is not OperationCanceledException` so a cancellation that fell through
            // the (cancellationToken.IsCancellationRequested) filter on the first catch above — i.e.
            // a genuine host shutdown — still propagates as OperationCanceledException instead of
            // being relabelled a Claude failure here.
            _logger.LogError(ex, "Claude call raised an unclassified exception for model {Model}", model);
            throw new ClaudeApiException(ClaudeFailureKind.InvalidResponse, ex);
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
