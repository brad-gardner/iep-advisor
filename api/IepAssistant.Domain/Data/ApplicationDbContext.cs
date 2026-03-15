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
    public DbSet<ParentAdvocacyGoal> ParentAdvocacyGoals => Set<ParentAdvocacyGoal>();
    public DbSet<UserRecoveryCode> UserRecoveryCodes => Set<UserRecoveryCode>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MeetingPrepChecklist> MeetingPrepChecklists => Set<MeetingPrepChecklist>();
    public DbSet<ChildAccess> ChildAccesses => Set<ChildAccess>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<BetaInviteCode> BetaInviteCodes => Set<BetaInviteCode>();

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
