namespace IepAssistant.Services.Models;

public sealed class ClaudeCompletionRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserText { get; init; }
    public byte[]? PdfDocument { get; init; }   // optional PDF attachment (base64-encoded as DocumentContent when present)
    /// <summary>
    /// Per-call model override. Null (the default) means "use the configured Anthropic:Model",
    /// which is how every call site should leave it unless it deliberately needs a different model.
    /// </summary>
    public string? Model { get; init; }
    public int MaxTokens { get; init; } = 16384;
}
