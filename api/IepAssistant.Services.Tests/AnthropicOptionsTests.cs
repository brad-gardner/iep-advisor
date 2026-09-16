using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Guards the fail-fast boot contract for Anthropic:Model/Effort. A blank model or an effort level the
/// SDK cannot send is exactly the class of defect that took analysis down in production, so it must
/// surface as a startup failure rather than as a 4xx discovered by the first user to run an analysis.
///
/// Effort is bound directly as <see cref="ThinkingEffort"/> (todos/P3-01 #4): <c>ConfigurationBinder</c>
/// parses enum names case-insensitively and throws on anything that isn't one of its members, so a typo
/// is caught by the type system at bind time instead of a regex duplicating the SDK's member list. This
/// file is accordingly much shorter than before — most of what it used to test was
/// <c>ValidateDataAnnotations</c>'s regex, not project logic (todos/P3-01 #5).
/// </summary>
public class AnthropicOptionsTests
{
    private static AnthropicOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddOptions<AnthropicOptions>()
            .Bind(configuration.GetSection(AnthropicOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    }

    private static Dictionary<string, string?> Settings(
        string? apiKey = "sk-test", string? model = "claude-opus-5", string? effort = "medium") =>
        new()
        {
            ["Anthropic:ApiKey"] = apiKey,
            ["Anthropic:Model"] = model,
            ["Anthropic:Effort"] = effort,
        };

    [Fact]
    public void ValidConfiguration_Binds()
    {
        var options = Resolve(Settings());

        Assert.Equal("claude-opus-5", options.Model);
        Assert.Equal(ThinkingEffort.medium, options.Effort);
    }

    [Theory]
    [InlineData("low", ThinkingEffort.low)]
    [InlineData("medium", ThinkingEffort.medium)]
    [InlineData("high", ThinkingEffort.high)]
    [InlineData("max", ThinkingEffort.max)]
    [InlineData("HIGH", ThinkingEffort.high)] // ConfigurationBinder's enum parsing is case-insensitive
    public void AcceptsEveryEffortLevelTheSdkCanSend(string effort, ThinkingEffort expected)
    {
        var options = Resolve(Settings(effort: effort));
        Assert.Equal(expected, options.Effort);
    }

    [Theory]
    [InlineData("")]           // blank
    [InlineData("xhigh")]      // real Anthropic level, but Anthropic.SDK 5.10.0 cannot express it
    [InlineData("aggressive")] // typo
    public void RejectsUnsupportedEffort(string effort)
    {
        // ConfigurationBinder throws while binding an invalid/blank enum value — a boot failure
        // regardless of the exact exception type it happens to throw.
        Assert.ThrowsAny<Exception>(() => Resolve(Settings(effort: effort)));
    }

    [Fact]
    public void RejectsBlankModel()
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(Settings(model: "")));
    }

    [Fact]
    public void AcceptsBlankApiKey_SoAMissingSecretCannotFailBoot()
    {
        // Deliberate: a missing Anthropic:ApiKey must NOT take down login, billing, and uploads.
        // ClaudeClient's blank-key guard scopes that failure to the AI features instead — see
        // ClaudeClientTests.CompleteAsync_Throws_Configuration_WhenApiKeyMissing.
        var options = Resolve(Settings(apiKey: ""));
        Assert.Equal(string.Empty, options.ApiKey);
    }
}
