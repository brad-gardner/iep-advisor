using System.Text.Json.Nodes;

namespace IepAssistant.Services.Models;

/// <summary>
/// A multi-turn, tool-enabled request for <c>IClaudeClient.StreamWithToolsAsync</c>. The system
/// prompt and tool definitions are cached automatically (<c>AutomaticToolsAndSystem</c>), so keep
/// both stable across calls within a conversation.
/// </summary>
public sealed class ClaudeToolRequest
{
    public required string SystemPrompt { get; init; }

    /// <summary>Prior turns plus the current user turn, oldest first. Roles are "user" or "assistant".</summary>
    public required IReadOnlyList<ClaudeTurn> Messages { get; init; }

    public required IReadOnlyList<ClaudeToolDefinition> Tools { get; init; }

    public int MaxTokens { get; init; } = 8192;

    /// <summary>
    /// How many rounds of tool execution are allowed before the loop gives up. A turn that requests
    /// tools after this many rounds have already run is not executed; the stream completes with
    /// <c>Truncated = true</c> instead.
    /// </summary>
    public int MaxToolRounds { get; init; } = 6;
}

/// <summary>One conversation turn. <paramref name="Role"/> is "user" or "assistant".</summary>
public sealed record ClaudeTurn(string Role, string Text);

/// <summary>A client tool the model may call. <paramref name="InputSchema"/> is a JSON Schema object.</summary>
public sealed record ClaudeToolDefinition(string Name, string Description, JsonNode InputSchema);
