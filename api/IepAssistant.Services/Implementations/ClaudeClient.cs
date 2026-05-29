using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class ClaudeClient : IClaudeClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeClient> _logger;

    public ClaudeClient(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ClaudeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogError("Anthropic API key not configured");
            return null;
        }

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
            Model = request.Model,
            MaxTokens = request.MaxTokens,
            System = [new SystemMessage(request.SystemPrompt)],
        };

        var response = await client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

        var responseText = (response.Content?.FirstOrDefault() as TextContent)?.Text;
        return string.IsNullOrEmpty(responseText) ? null : responseText;
    }
}
