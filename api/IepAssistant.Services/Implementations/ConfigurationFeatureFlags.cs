using Microsoft.Extensions.Configuration;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

public class ConfigurationFeatureFlags : IFeatureFlags
{
    private readonly IConfiguration _configuration;

    public ConfigurationFeatureFlags(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsEnabled(string flag) => _configuration.GetValue<bool>($"Feature:{flag}");
}
