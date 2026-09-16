using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
    public DbSet<IepDocument> IepDocuments => Set<IepDocument>();
    public DbSet<IepSection> IepSections => Set<IepSection>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<IepAnalysis> IepAnalyses => Set<IepAnalysis>();
    public DbSet<EtrDocument> EtrDocuments => Set<EtrDocument>();
    public DbSet<EtrSection> EtrSections => Set<EtrSection>();
    public DbSet<EtrAnalysis> EtrAnalyses => Set<EtrAnalysis>();
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<AnalysisRunSource> AnalysisRunSources => Set<AnalysisRunSource>();
    public DbSet<AnalysisRunSection> AnalysisRunSections => Set<AnalysisRunSection>();
    public DbSet<ParentAdvocacyGoal> ParentAdvocacyGoals => Set<ParentAdvocacyGoal>();
    public DbSet<ParentContribution> ParentContributions => Set<ParentContribution>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MeetingPrepChecklist> MeetingPrepChecklists => Set<MeetingPrepChecklist>();
    public DbSet<ChildAccess> ChildAccesses => Set<ChildAccess>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<BetaInviteCode> BetaInviteCodes => Set<BetaInviteCode>();
    public DbSet<KnowledgeBaseEntry> KnowledgeBaseEntries => Set<KnowledgeBaseEntry>();
    public DbSet<ProgressReport> ProgressReports => Set<ProgressReport>();
    public DbSet<ProgressReportAnalysis> ProgressReportAnalyses => Set<ProgressReportAnalysis>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<OrgRole> OrgRoles => Set<OrgRole>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffInvite> StaffInvites => Set<StaffInvite>();
    public DbSet<SchoolStudent> SchoolStudents => Set<SchoolStudent>();
    public DbSet<SchoolStudentAccess> SchoolStudentAccesses => Set<SchoolStudentAccess>();
    public DbSet<StudentTeamMember> StudentTeamMembers => Set<StudentTeamMember>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ChildLink> ChildLinks => Set<ChildLink>();
    public DbSet<IepDraft> IepDrafts => Set<IepDraft>();
    public DbSet<IepDraftSection> IepDraftSections => Set<IepDraftSection>();
    public DbSet<IepDraftGoal> IepDraftGoals => Set<IepDraftGoal>();
    public DbSet<IepDraftServiceLine> IepDraftServiceLines => Set<IepDraftServiceLine>();
    public DbSet<IepDraftAccommodation> IepDraftAccommodations => Set<IepDraftAccommodation>();
    public DbSet<IepDraftTransitionItem> IepDraftTransitionItems => Set<IepDraftTransitionItem>();
    public DbSet<IepVersion> IepVersions => Set<IepVersion>();
    public DbSet<IepVersionSection> IepVersionSections => Set<IepVersionSection>();
    public DbSet<IepVersionGoal> IepVersionGoals => Set<IepVersionGoal>();
    public DbSet<IepVersionServiceLine> IepVersionServiceLines => Set<IepVersionServiceLine>();
    public DbSet<IepVersionAccommodation> IepVersionAccommodations => Set<IepVersionAccommodation>();
    public DbSet<IepVersionTransitionItem> IepVersionTransitionItems => Set<IepVersionTransitionItem>();
    public DbSet<IepVersionPdf> IepVersionPdfs => Set<IepVersionPdf>();
    public DbSet<AccessAuditLog> AccessAuditLogs => Set<AccessAuditLog>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<StudentInvite> StudentInvites => Set<StudentInvite>();
    public DbSet<StudentWorkspace> StudentWorkspaces => Set<StudentWorkspace>();
    public DbSet<StudentWorkspaceEntry> StudentWorkspaceEntries => Set<StudentWorkspaceEntry>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<DocumentTemplateVersion> DocumentTemplateVersions => Set<DocumentTemplateVersion>();
    public DbSet<TemplateSection> TemplateSections => Set<TemplateSection>();
    public DbSet<TemplateField> TemplateFields => Set<TemplateField>();
    public DbSet<DocumentInstance> DocumentInstances => Set<DocumentInstance>();
    public DbSet<AuthoredDocumentVersion> AuthoredDocumentVersions => Set<AuthoredDocumentVersion>();
    public DbSet<AuthoredDocumentPdf> AuthoredDocumentPdfs => Set<AuthoredDocumentPdf>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingParticipant> MeetingParticipants => Set<MeetingParticipant>();
    public DbSet<MeetingReminder> MeetingReminders => Set<MeetingReminder>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Plan 6: family draft sharing, review, AI explanations/questions, responses, converge, meeting summaries.
    public DbSet<SharedDraftRevision> SharedDraftRevisions => Set<SharedDraftRevision>();
    public DbSet<SharedDraftExplanation> SharedDraftExplanations => Set<SharedDraftExplanation>();
    public DbSet<ParentDraftNote> ParentDraftNotes => Set<ParentDraftNote>();
    public DbSet<DraftResponse> DraftResponses => Set<DraftResponse>();
    public DbSet<DraftAcknowledgement> DraftAcknowledgements => Set<DraftAcknowledgement>();
    public DbSet<MeetingSummary> MeetingSummaries => Set<MeetingSummary>();

    // Plan 7 phase 1: goals as entities + provider observations.
    public DbSet<GoalRecord> GoalRecords => Set<GoalRecord>();
    public DbSet<GoalObservation> GoalObservations => Set<GoalObservation>();
    public DbSet<GoalRetirement> GoalRetirements => Set<GoalRetirement>();

    // Plan 7 phase 2: evaluation case + clock + evaluator assignments.
    public DbSet<EvaluationCase> EvaluationCases => Set<EvaluationCase>();
    public DbSet<EvaluatorAssignment> EvaluatorAssignments => Set<EvaluatorAssignment>();

    // Plan 7 phase 3: meeting brief, attendance decisions, offline family participation.
    public DbSet<MeetingBrief> MeetingBriefs => Set<MeetingBrief>();
    public DbSet<MeetingDecision> MeetingDecisions => Set<MeetingDecision>();
    public DbSet<FamilyContactAttempt> FamilyContactAttempts => Set<FamilyContactAttempt>();
    public DbSet<OfflineFamilyInput> OfflineFamilyInputs => Set<OfflineFamilyInput>();

    // Plan 7 phase 4: signed artifacts, amendments, district export.
    public DbSet<SignedArtifact> SignedArtifacts => Set<SignedArtifact>();
    public DbSet<SignatureEvent> SignatureEvents => Set<SignatureEvent>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    // Pilot-gates plan, phase 1: durable audit + outbound email queue.
    public DbSet<PendingAuditEvent> PendingAuditEvents => Set<PendingAuditEvent>();
    public DbSet<AuditIntegrityRun> AuditIntegrityRuns => Set<AuditIntegrityRun>();
    public DbSet<OutboundEmail> OutboundEmails => Set<OutboundEmail>();

    // Pilot-gates plan, phase 3: magic-link sign-in tokens.
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Suppress warning about pending model changes
        // This is safe because we control the migration generation process
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
