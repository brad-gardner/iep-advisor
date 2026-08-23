using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Guards the fail-fast boot contract. A blank model or a typo'd effort is exactly the class of
/// defect that took analysis down in production, so it must surface as a startup failure rather
/// than as a 4xx discovered by the first user to run an analysis.
/// </summary>
public class AnthropicOptionsTests
{
    private static AnthropicOptions? Resolve(Dictionary<string, string?> settings)
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

        Assert.NotNull(options);
        Assert.Equal("claude-opus-5", options!.Model);
        Assert.Equal("medium", options.Effort);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    [InlineData("max")]
    public void AcceptsEveryEffortLevelTheSdkCanSend(string effort)
    {
        var options = Resolve(Settings(effort: effort));

        Assert.Equal(effort, options!.Effort);
    }

    [Theory]
    [InlineData("")]           // blank
    [InlineData("Medium")]     // wrong case
    [InlineData("xhigh")]      // real Anthropic level, but Anthropic.SDK 5.10.0 cannot express it
    [InlineData("aggressive")] // typo
    public void RejectsUnsupportedEffort(string effort)
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(Settings(effort: effort)));
    }

    [Fact]
    public void RejectsBlankModel()
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(Settings(model: "")));
    }

    [Fact]
    public void RejectsBlankApiKey()
    {
        Assert.Throws<OptionsValidationException>(() => Resolve(Settings(apiKey: "")));
    }
}
