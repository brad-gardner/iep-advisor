namespace IepAssistant.Services.Interfaces;

public interface IFeatureFlags
{
    bool IsEnabled(string flag);
}
