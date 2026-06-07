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
    public DbSet<SchoolStudent> SchoolStudents => Set<SchoolStudent>();
    public DbSet<SchoolStudentAccess> SchoolStudentAccesses => Set<SchoolStudentAccess>();
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
