using Microsoft.Extensions.DependencyInjection;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IChildProfileService, ChildProfileService>();
        services.AddScoped<IIepDocumentService, IepDocumentService>();
        services.AddScoped<IIepProcessingService, IepProcessingService>();
        services.AddScoped<IIepAnalysisService, IepAnalysisService>();
        services.AddScoped<IParentAdvocacyGoalService, ParentAdvocacyGoalService>();

        return services;
    }
}
