namespace IepAssistant.Services.Models;

public sealed class ClaudeCompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserText { get; init; }
    public byte[]? PdfDocument { get; init; }   // optional PDF attachment (base64-encoded as DocumentContent when present)
    public int MaxTokens { get; init; } = 16384;
}
