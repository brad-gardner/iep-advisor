using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

public class ClaudeClientTests
{
    private const string ConfiguredModel = "claude-opus-5";

    // A canned, standards-compliant Anthropic Messages API response body. The Anthropic.SDK
    // deserializes this into its MessageResponse model, and ClaudeClient concatenates the text
    // content blocks out of it. We include the full set of fields
    // (id/type/role/model/content/stop_reason/usage) so the SDK's deserializer has everything it
    // expects for a real round-trip.
    private const string CannedResponse = """
    {
      "id": "msg_test_123",
      "type": "message",
      "role": "assistant",
      "model": "claude-opus-5",
      "content": [
        { "type": "text", "text": "HELLO" }
      ],
      "stop_reason": "end_turn",
      "stop_sequence": null,
      "usage": { "input_tokens": 10, "output_tokens": 1 }
    }
    """;

    // Models with adaptive thinking return a thinking block FIRST. This is the exact shape that
    // made `Content.FirstOrDefault() as TextContent` silently yield null.
    private const string ThinkingThenTextResponse = """
    {
      "id": "msg_test_124",
      "type": "message",
      "role": "assistant",
      "model": "claude-opus-5",
      "content": [
        { "type": "thinking", "thinking": "Let me reason about this.", "signature": "sig_abc" },
        { "type": "text", "text": "HELLO" }
      ],
      "stop_reason": "end_turn",
      "stop_sequence": null,
      "usage": { "input_tokens": 10, "output_tokens": 1 }
    }
    """;

    private const string ThinkingOnlyResponse = """
    {
      "id": "msg_test_125",
      "type": "message",
      "role": "assistant",
      "model": "claude-opus-5",
      "content": [
        { "type": "thinking", "thinking": "Thought but never answered.", "signature": "sig_abc" }
      ],
      "stop_reason": "end_turn",
      "stop_sequence": null,
      "usage": { "input_tokens": 10, "output_tokens": 0 }
    }
    """;

    private static string ErrorBody(string errorType, string message = "boom") =>
        $$"""
        {"type":"error","error":{"type":"{{errorType}}","message":"{{message}}"},"request_id":"req_011CeJg96PRbL75TvhoRJKA8"}
        """;

    // Stub handler that records the outgoing request and returns a canned response.
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public bool WasCalled { get; private set; }

        public StubHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    // Stub handler that throws instead of responding — used for the timeout/cancellation paths,
    // where HttpClient surfaces a TaskCanceledException rather than an HTTP status.
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Func<Exception> _exceptionFactory;
        public ThrowingHandler(Func<Exception> exceptionFactory) => _exceptionFactory = exceptionFactory;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _exceptionFactory();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static IOptions<AnthropicOptions> BuildOptions(
        string apiKey = "test-key", string model = ConfiguredModel, string effort = "medium") =>
        Options.Create(new AnthropicOptions { ApiKey = apiKey, Model = model, Effort = effort });

    private static ClaudeClient BuildClient(HttpMessageHandler handler, IOptions<AnthropicOptions>? options = null) =>
        new(new StubHttpClientFactory(handler), options ?? BuildOptions(), NullLogger<ClaudeClient>.Instance);

    private static ClaudeCompletionRequest Request(int maxTokens = 1024) =>
        new() { SystemPrompt = "system", UserText = "hello", MaxTokens = maxTokens };

    // --- Happy path and request building ---

    [Fact]
    public async Task CompleteAsync_ReturnsText_FromCannedResponse()
    {
        var handler = new StubHandler(CannedResponse);
        var client = BuildClient(handler);

        var result = await client.CompleteAsync(Request());

        Assert.Equal("HELLO", result);
        Assert.True(handler.WasCalled);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("1024", handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteAsync_SendsPdfDocument_WhenPdfProvided()
    {
        var handler = new StubHandler(CannedResponse);
        var client = BuildClient(handler);

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake pdf bytes");
        var expectedBase64 = Convert.ToBase64String(pdfBytes);

        var result = await client.CompleteAsync(new ClaudeCompletionRequest
        {
            SystemPrompt = "system",
            UserText = "parse this",
            PdfDocument = pdfBytes,
            MaxTokens = 16384,
        });

        Assert.Equal("HELLO", result);
        Assert.NotNull(handler.LastRequestBody);
        // The document-attachment branch must serialize a base64 PDF DocumentContent.
        Assert.Contains("application/pdf", handler.LastRequestBody);
        Assert.Contains(expectedBase64, handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteAsync_AlwaysUsesConfiguredModel()
    {
        // There is no per-call override any more: the configured model is the only source, which is
        // what puts all nine Claude-calling services on one config-driven model.
        var handler = new StubHandler(CannedResponse);
        var client = BuildClient(handler, BuildOptions(model: "claude-configured-default"));

        await client.CompleteAsync(Request());

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("claude-configured-default", handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteAsync_SendsAdaptiveThinkingWithConfiguredEffort()
    {
        var handler = new StubHandler(CannedResponse);
        var client = BuildClient(handler, BuildOptions(effort: "medium"));

        await client.CompleteAsync(Request());

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"adaptive\"", handler.LastRequestBody);
        Assert.Contains("\"effort\":\"medium\"", handler.LastRequestBody);
        // budget_tokens is rejected outright by current models; adaptive thinking must not carry one.
        Assert.DoesNotContain("budget_tokens", handler.LastRequestBody);
    }

    /// <summary>
    /// Regression guard, not a style check: current models reject temperature/top_p/top_k with a
    /// 400. If the SDK ever starts emitting a default for any of them, every Claude call in the
    /// product fails — and this test is the only thing standing between that change and production.
    /// </summary>
    [Fact]
    public async Task CompleteAsync_SendsNoSamplingParameters()
    {
        var handler = new StubHandler(CannedResponse);
        var client = BuildClient(handler);

        await client.CompleteAsync(Request());

        Assert.NotNull(handler.LastRequestBody);
        Assert.DoesNotContain("temperature", handler.LastRequestBody);
        Assert.DoesNotContain("top_p", handler.LastRequestBody);
        Assert.DoesNotContain("top_k", handler.LastRequestBody);
    }

    // --- Response extraction ---

    [Fact]
    public async Task CompleteAsync_ReturnsText_WhenResponseStartsWithThinkingBlock()
    {
        var handler = new StubHandler(ThinkingThenTextResponse);
        var client = BuildClient(handler);

        var result = await client.CompleteAsync(Request());

        Assert.Equal("HELLO", result);
    }

    [Fact]
    public async Task CompleteAsync_Throws_InvalidResponse_WhenOnlyThinkingBlockReturned()
    {
        var handler = new StubHandler(ThinkingOnlyResponse);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.InvalidResponse, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.InvalidResponse, ex.UserMessage);
    }

    // --- Failure classification ---

    [Fact]
    public async Task CompleteAsync_Throws_Configuration_OnNotFoundError()
    {
        // Mirrors the 2026-08-22 production failure, where the configured model had been retired.
        // The model id here is a deliberate stand-in for the real retired one, so that grepping
        // the tree for that retired id stays a clean, CI-enforceable invariant. The assertion below
        // only needs SOME model id present in the error body to prove it does not leak.
        var handler = new StubHandler(
            ErrorBody("not_found_error", "model: retired-model-id-from-error-body"), HttpStatusCode.NotFound);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.Configuration, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.Configuration, ex.UserMessage);
        // The error body names the model and a request id; neither may reach a parent-visible field.
        Assert.DoesNotContain("retired-model-id-from-error-body", ex.UserMessage);
        Assert.DoesNotContain("req_", ex.UserMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error", ClaudeFailureKind.Configuration)]
    [InlineData(HttpStatusCode.Forbidden, "permission_error", ClaudeFailureKind.Configuration)]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", ClaudeFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "overloaded_error", ClaudeFailureKind.Transient)]
    [InlineData(HttpStatusCode.InternalServerError, "api_error", ClaudeFailureKind.Transient)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "request_too_large", ClaudeFailureKind.RequestTooLarge)]
    public async Task CompleteAsync_ClassifiesApiErrors(
        HttpStatusCode status, string errorType, ClaudeFailureKind expected)
    {
        var handler = new StubHandler(ErrorBody(errorType), status);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(expected, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.For(expected), ex.UserMessage);
    }

    [Fact]
    public async Task CompleteAsync_ClassifiesFromErrorBody_WhenStatusCarriesNoMapping()
    {
        // 400 is not in the status-code map, so this can only be classified from error.type.
        var handler = new StubHandler(ErrorBody("invalid_request_error"), HttpStatusCode.BadRequest);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.Configuration, ex.Kind);
    }

    [Fact]
    public async Task CompleteAsync_ClassifiesContextOverflow_AsRequestTooLarge()
    {
        // Anthropic reports a context-window overflow as a 400 invalid_request_error, NOT a 413.
        // Classifying it Configuration would suppress retry and withhold the "select fewer
        // documents" guidance, dead-ending a user whose problem is one click away from fixed.
        var handler = new StubHandler(
            ErrorBody("invalid_request_error", "prompt is too long: 250000 tokens > 200000 maximum"),
            HttpStatusCode.BadRequest);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.RequestTooLarge, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.RequestTooLarge, ex.UserMessage);
    }

    [Fact]
    public async Task CompleteAsync_ClassifiesOtherInvalidRequests_AsConfiguration()
    {
        var handler = new StubHandler(
            ErrorBody("invalid_request_error", "max_tokens: must be greater than 0"),
            HttpStatusCode.BadRequest);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.Configuration, ex.Kind);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError)]
    [InlineData(HttpRequestError.ConnectionError)]
    [InlineData(HttpRequestError.SecureConnectionError)]
    public async Task CompleteAsync_ClassifiesTransportFailures_AsTransient(HttpRequestError error)
    {
        // The request never reached Anthropic: no status, no body to parse. Retrying is the right
        // advice, so this must not degrade to Unknown.
        var handler = new ThrowingHandler(() => new HttpRequestException(error, "no route to host"));
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.Transient, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.Transient, ex.UserMessage);
    }

    [Fact]
    public async Task CompleteAsync_ClassifiesUnknown_OnMalformedErrorBody()
    {
        // A body the mapper cannot parse must degrade to Unknown, never throw out of the mapper.
        var handler = new StubHandler("<html>gateway exploded</html>", HttpStatusCode.BadRequest);
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.Unknown, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.Unknown, ex.UserMessage);
    }

    // --- Cancellation vs. timeout ---

    [Fact]
    public async Task CompleteAsync_Throws_Timeout_WhenCancelledWithoutCallerCancellation()
    {
        // TaskCanceledException with a token the caller never cancelled is the HttpClient timeout.
        var handler = new ThrowingHandler(() => new TaskCanceledException("timeout"));
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(
            () => client.CompleteAsync(Request(), CancellationToken.None));

        Assert.Equal(ClaudeFailureKind.Timeout, ex.Kind);
        Assert.Equal(ClaudeFailureMessages.Timeout, ex.UserMessage);
    }

    [Fact]
    public async Task CompleteAsync_PropagatesCancellation_WhenCallerCancelled()
    {
        // Host shutdown must NOT be relabelled "took too long" — every deploy restart would
        // otherwise write a lie onto in-flight runs.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var handler = new ThrowingHandler(() => new TaskCanceledException("shutdown"));
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteAsync(Request(), cts.Token));

        Assert.IsNotType<ClaudeApiException>(ex);
    }

    // --- Configuration guard ---

    [Fact]
    public async Task CompleteAsync_Throws_Configuration_WhenApiKeyMissing()
    {
        var handler = new StubHandler(CannedResponse);
        var client = BuildClient(handler, BuildOptions(apiKey: "   "));

        var ex = await Assert.ThrowsAsync<ClaudeApiException>(() => client.CompleteAsync(Request()));

        Assert.Equal(ClaudeFailureKind.Configuration, ex.Kind);
        Assert.False(handler.WasCalled); // HTTP must not be attempted without a key
    }
}
