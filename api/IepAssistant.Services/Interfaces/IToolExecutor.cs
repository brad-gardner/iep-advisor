using System.Text.Json;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Executes client tools on behalf of <see cref="IClaudeClient.StreamWithToolsAsync"/>.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Runs <paramref name="toolName"/> with the model-supplied <paramref name="input"/> and returns
    /// the result text (normally JSON) to send back as the <c>tool_result</c> content.
    /// </summary>
    /// <remarks>
    /// Throw <see cref="ToolExecutionException"/> to send the model an <c>is_error</c> result with the
    /// exception's message (unknown tool, invalid input, "not found"). Never throw anything else: the
    /// client deliberately does not catch other exception types, so they abort the whole stream.
    /// <paramref name="input"/> is untrusted model output — validate it against the tool's schema.
    /// </remarks>
    Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken);
}
