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

    /// <summary>
    /// Deliberately NOT [Required], and therefore not boot-validated. A missing Azure App Service
    /// setting would otherwise fail startup for the WHOLE application — login, billing, uploads,
    /// PDF generation — over a key only the AI features need, and deploys go straight to Production
    /// with no staging slot to catch it. ClaudeClient's blank-key guard is the enforcement instead:
    /// it scopes the failure to AI features, returns a canned message, and logs a Configuration
    /// failure kind. Do not add [Required] here.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model id sent on every Claude call. There is deliberately no per-call override: the only
    /// thing that ever set one was the retired model id, on eight call sites that thereby bypassed
    /// this setting. Changing the model is a settings change, not a code change — that is the
    /// point of this option existing.
    /// </summary>
    /// <remarks>
    /// [Required] would be unreachable here — both this class and appsettings.json supply a
    /// non-empty default — but an operator CAN blank it out in App Service, and an empty model
    /// would 400 on every call. The length bound catches exactly that case.
    /// </remarks>
    [MinLength(1, ErrorMessage = "Anthropic:Model must not be blank.")]
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// Adaptive-thinking effort level, mapped by the SDK onto <c>output_config.effort</c>.
    /// Bounds how many tokens the model spends thinking before it answers.
    /// </summary>
    // Anthropic.SDK 5.10.0's ThinkingEffort enum exposes exactly these four levels — it has no
    // "xhigh" — so anything outside this set could not be transmitted and fails fast at startup
    // rather than silently degrading to a different effort than the one configured.
    // MinLength is not redundant with the regex: RegularExpressionAttribute passes on an empty
    // string by design, so without this an operator who blanked the setting would sail past
    // validation and silently land on the ResolveEffort fallback.
    [MinLength(1, ErrorMessage = "Anthropic:Effort must not be blank.")]
    [RegularExpression("^(low|medium|high|max)$",
        ErrorMessage = "Anthropic:Effort must be one of: low, medium, high, max.")]
    public string Effort { get; set; } = "medium";
}
