using System.Net;
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
                Effort = ResolveEffort(_options.Effort),
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
        catch (AuthenticationException ex)
        {
            // Anthropic.SDK throws this — not HttpRequestException — for a 401, and embeds the whole
            // API response body in the message. Without this arm a bad key would escape unclassified
            // to the caller's broad catch, and that body is exactly what must never be surfaced.
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

    // Anthropic.SDK 5.10.0 declares ThinkingEffort with lowercase members and has no "xhigh" level,
    // so AnthropicOptions.Effort is constrained to the four the SDK can actually send. Unrecognized
    // input cannot reach here in the API host (ValidateOnStart rejects it at boot); the medium
    // fallback covers hosts that bind the options without validation.
    private static ThinkingEffort ResolveEffort(string? effort) => effort?.Trim().ToLowerInvariant() switch
    {
        "low" => ThinkingEffort.low,
        "high" => ThinkingEffort.high,
        "max" => ThinkingEffort.max,
        _ => ThinkingEffort.medium,
    };

    /// <summary>
    /// Classifies an SDK HTTP failure using every available signal, most reliable first: the HTTP
    /// status code when the SDK surfaced one, then the transport-level error for failures that
    /// never reached the API at all, then the <c>error</c> object inside the raw JSON body the SDK
    /// puts in the exception message. Never throws.
    /// </summary>
    private static ClaudeFailureKind Classify(HttpRequestException ex)
    {
        if (ex.StatusCode is { } status && MapStatusCode(status) is { } byStatus)
            return byStatus;

        // No response at all — DNS, TLS, proxy, or a refused connection. The request never reached
        // Anthropic, so there is no body to parse and retrying is exactly the right advice.
        if (ex.HttpRequestError is HttpRequestError.ConnectionError
            or HttpRequestError.NameResolutionError
            or HttpRequestError.SecureConnectionError
            or HttpRequestError.ProxyTunnelError
            or HttpRequestError.ResponseEnded)
            return ClaudeFailureKind.Transient;

        var (errorType, errorMessage) = TryParseError(ex.Message);
        if (MapErrorType(errorType, errorMessage) is { } byErrorType)
            return byErrorType;

        return ClaudeFailureKind.Unknown;
    }

    private static ClaudeFailureKind? MapStatusCode(HttpStatusCode status) => (int)status switch
    {
        401 or 403 or 404 => ClaudeFailureKind.Configuration,
        413 => ClaudeFailureKind.RequestTooLarge,
        429 => ClaudeFailureKind.RateLimited,
        >= 500 and <= 599 => ClaudeFailureKind.Transient,
        _ => null,
    };

    private static ClaudeFailureKind? MapErrorType(string? errorType, string? errorMessage) => errorType switch
    {
        "not_found_error" or "authentication_error" or "permission_error"
            => ClaudeFailureKind.Configuration,
        "rate_limit_error" => ClaudeFailureKind.RateLimited,
        "overloaded_error" or "api_error" => ClaudeFailureKind.Transient,
        "request_too_large" => ClaudeFailureKind.RequestTooLarge,
        // Context-window overflow arrives as a 400 invalid_request_error, not a 413. It is the most
        // likely user-triggered failure on a large multi-document run, and it must not land in
        // Configuration: that kind suppresses retry and offers no "select fewer documents" guidance,
        // leaving the user at a dead end for something one click would fix. It would also page an
        // oversized document set as a service-configuration incident.
        "invalid_request_error" => IsContextOverflow(errorMessage)
            ? ClaudeFailureKind.RequestTooLarge
            : ClaudeFailureKind.Configuration,
        _ => null,
    };

    private static bool IsContextOverflow(string? errorMessage) =>
        errorMessage is not null
        && (errorMessage.Contains("prompt is too long", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("too many tokens", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("exceed context limit", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pulls <c>error.type</c> and <c>error.message</c> out of an Anthropic error body such as
    /// <c>{"type":"error","error":{"type":"not_found_error","message":"..."},"request_id":"..."}</c>.
    /// Returns nulls for anything that is not that shape; never throws. The message is used only to
    /// sub-classify — it is never surfaced to a user.
    /// </summary>
    private static (string? Type, string? Message) TryParseError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);

        var start = body.IndexOf('{');
        var end = body.LastIndexOf('}');
        if (start < 0 || end <= start)
            return (null, null);

        try
        {
            using var document = JsonDocument.Parse(body[start..(end + 1)]);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String)
            {
                var message = error.TryGetProperty("message", out var messageElement)
                    && messageElement.ValueKind == JsonValueKind.String
                        ? messageElement.GetString()
                        : null;

                return (type.GetString(), message);
            }
        }
        catch (JsonException)
        {
            // Malformed body: fall through to the Unknown classification rather than throwing
            // out of the mapper and masking the original failure.
        }

        return (null, null);
    }
}
