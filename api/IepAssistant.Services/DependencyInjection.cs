using Microsoft.Extensions.DependencyInjection;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<MfaSecretProtector>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IChildProfileService, ChildProfileService>();
        services.AddScoped<IIepDocumentService, IepDocumentService>();
        services.AddScoped<IIepProcessingService, IepProcessingService>();
        services.AddScoped<IIepAnalysisService, IepAnalysisService>();
        services.AddScoped<IParentAdvocacyGoalService, ParentAdvocacyGoalService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IMeetingPrepService, MeetingPrepService>();
        services.AddScoped<IIepComparisonService, IepComparisonService>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IShareService, ShareService>();

        return services;
    }
}
