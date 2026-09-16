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
        services.AddScoped<IOutboundEmailQueue, OutboundEmailQueue>();
        services.AddScoped<IEmailTransport, AcsEmailTransport>();
        services.AddScoped<IAuditIntegrityService, AuditIntegrityService>();
        services.AddScoped<IAccountPurgeService, AccountPurgeService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMagicLinkService, MagicLinkService>();
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
        services.AddScoped<IStudentTeamService, StudentTeamService>();
        services.AddScoped<IRosterImportService, RosterImportService>();
        services.AddScoped<IStaffImportService, StaffImportService>();
        services.AddScoped<IChildLinkService, ChildLinkService>();
        services.AddScoped<IIepDraftService, IepDraftService>();
        services.AddScoped<IDocumentTemplateService, DocumentTemplateService>();
        services.AddScoped<ITemplateAuthoringService, TemplateAuthoringService>();
        services.AddScoped<ITemplateResolutionService, TemplateResolutionService>();
        services.AddScoped<IDocumentInstanceService, DocumentInstanceService>();
        services.AddScoped<IDefaultIepTemplateSeeder, DefaultIepTemplateSeeder>();
        services.AddScoped<ITemplateCatalogSeeder, TemplateCatalogSeeder>();
        services.AddScoped<IDocumentAssistService, DocumentAssistService>();
        services.AddScoped<IParentContributionService, ParentContributionService>();
        services.AddScoped<IStudentEvidenceService, StudentEvidenceService>();
        services.AddScoped<IDocumentPrefillService, DocumentPrefillService>();
        services.AddScoped<IIepVersionService, IepVersionService>();
        services.AddScoped<IIepVersionPdfService, IepVersionPdfService>();
        services.AddScoped<IAuthoredDocumentVersionService, AuthoredDocumentVersionService>();
        services.AddScoped<IAuthoredDocumentPdfService, AuthoredDocumentPdfService>();
        services.AddScoped<IIepAssistService, IepAssistService>();
        services.AddScoped<IStudentInviteService, StudentInviteService>();
        services.AddScoped<IStudentWorkspaceService, StudentWorkspaceService>();
        services.AddScoped<IStaffInviteService, StaffInviteService>();
        services.AddScoped<IStaffInviteExpiryService, StaffInviteExpiryService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();

        // Plan 4: meetings, deadlines, notifications, calendar.
        services.AddSingleton<IIcsBuilder, IcsBuilder>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEmailService, NotificationEmailService>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IObligationService, ObligationService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IMeetingReminderService, MeetingReminderService>();
        services.AddScoped<IDigestService, DigestService>();

        // Plan 5: role homes and district compliance/adoption/engagement.
        services.AddScoped<IDocumentCompletenessService, DocumentCompletenessService>();
        services.AddScoped<IHomeService, HomeService>();

        // Plan 6: family draft sharing, review, AI explanations/questions, responses, converge, meeting summaries.
        services.AddScoped<IDraftResponseService, DraftResponseService>();
        services.AddScoped<IDraftSharingService, DraftSharingService>();
        services.AddScoped<IDraftExplanationService, DraftExplanationService>();
        services.AddScoped<IDraftQuestionService, DraftQuestionService>();
        services.AddScoped<IMeetingSummaryService, MeetingSummaryService>();

        // Plan 7 phases 1-2: goal records + provider observations, evaluation case lifecycle.
        services.AddScoped<IGoalRecordService, GoalRecordService>();
        services.AddScoped<IEvaluationCaseService, EvaluationCaseService>();

        // Plan 7 phase 3: meeting brief, decisions, offline family participation.
        services.AddScoped<IMeetingBriefService, MeetingBriefService>();
        services.AddScoped<IMeetingDecisionService, MeetingDecisionService>();
        services.AddScoped<IFamilyContactService, FamilyContactService>();

        // Plan 7 phase 4: signed artifacts, amendments (on IAuthoredDocumentVersionService), district export.
        services.AddScoped<ISignedArtifactService, SignedArtifactService>();
        services.AddScoped<IExportService, ExportService>();

        // Stateless JWT minting reused by create-and-sign-in flows (staff invite accept).
        services.AddScoped<JwtTokenFactory>();

        return services;
    }
}
