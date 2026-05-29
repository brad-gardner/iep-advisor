using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

public class ClaudeClientTests
{
    // A canned, standards-compliant Anthropic Messages API response body. The
    // Anthropic.SDK deserializes this into its MessageResponse model, and
    // ClaudeClient pulls the first TextContent's Text out of it. We include the
    // full set of fields (id/type/role/model/content/stop_reason/usage) so the
    // SDK's deserializer has everything it expects for a real round-trip.
    private const string CannedResponse = """
    {
      "id": "msg_test_123",
      "type": "message",
      "role": "assistant",
      "model": "claude-sonnet-4-20250514",
      "content": [
        { "type": "text", "text": "HELLO" }
      ],
      "stop_reason": "end_turn",
      "stop_sequence": null,
      "usage": {
        "input_tokens": 10,
        "output_tokens": 1
      }
    }
    """;

    // Stub handler that records the outgoing request and returns a canned response.
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public bool WasCalled { get; private set; }

        public StubHandler(string responseBody)
        {
            _responseBody = responseBody;
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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public async Task CompleteAsync_ReturnsText_FromCannedResponse()
    {
        var handler = new StubHandler(CannedResponse);
        var factory = new StubHttpClientFactory(handler);
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "test-key",
        });

        var client = new ClaudeClient(factory, config, NullLogger<ClaudeClient>.Instance);

        var result = await client.CompleteAsync(new ClaudeCompletionRequest
        {
            SystemPrompt = "system",
            UserText = "hello",
            Model = "claude-sonnet-4-20250514",
            MaxTokens = 1024,
        });

        Assert.Equal("HELLO", result);
        Assert.True(handler.WasCalled);

        // Observable request-building behavior: the configured model and max_tokens
        // are serialized into the outgoing request body.
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("claude-sonnet-4-20250514", handler.LastRequestBody);
        Assert.Contains("1024", handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteAsync_SendsPdfDocument_WhenPdfProvided()
    {
        var handler = new StubHandler(CannedResponse);
        var factory = new StubHttpClientFactory(handler);
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Anthropic:ApiKey"] = "test-key",
        });

        var client = new ClaudeClient(factory, config, NullLogger<ClaudeClient>.Instance);

        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake pdf bytes");
        var expectedBase64 = Convert.ToBase64String(pdfBytes);

        var result = await client.CompleteAsync(new ClaudeCompletionRequest
        {
            SystemPrompt = "system",
            UserText = "parse this",
            PdfDocument = pdfBytes,
            Model = "claude-sonnet-4-20250514",
            MaxTokens = 16384,
        });

        Assert.Equal("HELLO", result);
        Assert.NotNull(handler.LastRequestBody);
        // The document-attachment branch must serialize a base64 PDF DocumentContent.
        Assert.Contains("application/pdf", handler.LastRequestBody);
        Assert.Contains(expectedBase64, handler.LastRequestBody);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsNull_WhenApiKeyMissing()
    {
        var handler = new StubHandler(CannedResponse);
        var factory = new StubHttpClientFactory(handler);
        var config = BuildConfig(new Dictionary<string, string?>()); // no Anthropic:ApiKey

        var client = new ClaudeClient(factory, config, NullLogger<ClaudeClient>.Instance);

        var result = await client.CompleteAsync(new ClaudeCompletionRequest
        {
            SystemPrompt = "system",
            UserText = "hello",
        });

        Assert.Null(result);
        Assert.False(handler.WasCalled); // HTTP must not be attempted without a key
    }
}
