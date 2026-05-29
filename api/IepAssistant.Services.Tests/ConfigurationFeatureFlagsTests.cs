using Microsoft.Extensions.Configuration;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

public class ConfigurationFeatureFlagsTests
{
    private static ConfigurationFeatureFlags Build(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new ConfigurationFeatureFlags(configuration);
    }

    [Fact]
    public void IsEnabled_ReturnsTrue_WhenFlagConfiguredTrue()
    {
        var flags = Build(new Dictionary<string, string?>
        {
            ["Feature:AnalysisRun"] = "true",
        });

        Assert.True(flags.IsEnabled(FeatureFlags.AnalysisRun));
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenFlagConfiguredFalse()
    {
        var flags = Build(new Dictionary<string, string?>
        {
            ["Feature:AnalysisRun"] = "false",
        });

        Assert.False(flags.IsEnabled(FeatureFlags.AnalysisRun));
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenFlagMissing()
    {
        var flags = Build(new Dictionary<string, string?>());

        Assert.False(flags.IsEnabled(FeatureFlags.SchoolSide));
    }
}
