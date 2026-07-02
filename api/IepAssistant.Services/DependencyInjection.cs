using Microsoft.Extensions.DependencyInjection;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Security;

namespace IepAssistant.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IClaudeClient, ClaudeClient>();
        services.AddSingleton<MfaSecretProtector>();
        services.AddScoped<ITotpService, TotpService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IChildProfileService, ChildProfileService>();
        services.AddScoped<IIepDocumentService, IepDocumentService>();
        services.AddScoped<IEtrDocumentService, EtrDocumentService>();
        services.AddScoped<IIepProcessingService, IepProcessingService>();
        services.AddScoped<IEtrProcessingService, EtrProcessingService>();
        services.AddScoped<IIepAnalysisService, IepAnalysisService>();
        services.AddScoped<IEtrAnalysisService, EtrAnalysisService>();
        services.AddScoped<IParentAdvocacyGoalService, ParentAdvocacyGoalService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IMeetingPrepService, MeetingPrepService>();
        services.AddScoped<IIepComparisonService, IepComparisonService>();
        services.AddScoped<IAccessService, AccessService>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
        services.AddScoped<IProgressReportService, ProgressReportService>();
        services.AddScoped<IProgressReportAnalysisService, ProgressReportAnalysisService>();
        services.AddScoped<IAnalysisRunService, AnalysisRunService>();
        services.AddScoped<IAnalysisRunBackfillService, AnalysisRunBackfillService>();
        services.AddScoped<IOrgAccessService, OrgAccessService>();
        services.AddScoped<IDistrictService, DistrictService>();
        services.AddScoped<IEducatorService, EducatorService>();
        services.AddScoped<IChildLinkService, ChildLinkService>();
        services.AddScoped<IIepDraftService, IepDraftService>();
        services.AddScoped<IIepVersionService, IepVersionService>();
        services.AddScoped<IIepVersionPdfService, IepVersionPdfService>();
        services.AddScoped<IIepAssistService, IepAssistService>();
        services.AddScoped<IStudentInviteService, StudentInviteService>();
        services.AddScoped<IStudentWorkspaceService, StudentWorkspaceService>();
        services.AddScoped<IStaffInviteService, StaffInviteService>();
        services.AddScoped<IStaffInviteExpiryService, StaffInviteExpiryService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();

        // Stateless JWT minting reused by create-and-sign-in flows (staff invite accept).
        services.AddScoped<JwtTokenFactory>();

        return services;
    }
}
