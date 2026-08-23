using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Services.Models;

/// <summary>
/// Strongly-typed binding for the <c>Anthropic</c> configuration section. Validated with
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> so a blank model or a typo'd effort is a
/// boot failure rather than a per-request 4xx discovered by the first user who runs an analysis.
/// </summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    [Required(AllowEmptyStrings = false)]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model id sent on every Claude call unless a call site overrides it via
    /// <see cref="ClaudeCompletionRequest.Model"/>. Changing the model is a settings change,
    /// not a code change — that is the point of this option existing.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// Adaptive-thinking effort level, mapped by the SDK onto <c>output_config.effort</c>.
    /// Bounds how many tokens the model spends thinking before it answers.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    // Anthropic.SDK 5.10.0's ThinkingEffort enum exposes exactly these four levels — it has no
    // "xhigh" — so anything outside this set could not be transmitted and fails fast at startup
    // rather than silently degrading to a different effort than the one configured.
    [RegularExpression("^(low|medium|high|max)$",
        ErrorMessage = "Anthropic:Effort must be one of: low, medium, high, max.")]
    public string Effort { get; set; } = "medium";
}
