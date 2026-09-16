using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AccessAuditLogConfiguration : IEntityTypeConfiguration<AccessAuditLog>
{
    public void Configure(EntityTypeBuilder<AccessAuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        // The immutability trigger (migration AddPilotGatesPhase12) must be declared here: EF Core 7+
        // otherwise saves with an OUTPUT clause, which SQL Server rejects on a table with any trigger
        // ("cannot have any enabled triggers … OUTPUT clause without INTO"). SQLite ignores it.
        builder.ToTable("AccessAuditLogs", t => t.HasTrigger("TR_AccessAuditLogs_Immutable"));

        // The hash backfill asks "any unhashed rows left?" on every restart; a filtered index turns that
        // into a probe instead of a scan of an ever-growing table (SQL Server filter syntax; SQLite ignores).
        builder.HasIndex(a => a.Id)
            .HasDatabaseName("IX_AccessAuditLogs_Unhashed")
            .HasFilter("[Hash] IS NULL");

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ResourceType)
            .HasMaxLength(50)
            .IsRequired();

        // Hash-chain columns (pilot-gates plan, phase 1). Nullable: a freshly-migrated historical row
        // has neither until AccessAuditLogWorker's startup backfill pass computes them, in Id order.
        builder.Property(a => a.PrevHash)
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(a => a.Hash)
            .HasMaxLength(64)
            .IsFixedLength();

        // Primary access-history lookup: "everything that touched this resource, in order."
        builder.HasIndex(a => new { a.ResourceType, a.ResourceId, a.CreatedAt });

        // Secondary: "everything this user did."
        builder.HasIndex(a => a.ActorUserId);

        // Audit-viewer read path (P2): the actor-scoped, Id-ordered query the district audit-log viewer
        // runs. Keyset pagination orders by Id DESC with an `Id < cursor` seek, and the actor set is a
        // semi-join on ActorUserId; the trailing Id column makes the single-actor path an index-only range
        // seek and serves the semi-join order. This is declared explicitly so the shape is provider-
        // independent — on SQL Server the clustered PK (Id) is silently appended to the bare (ActorUserId)
        // index above, but we don't want the read path's correctness to depend on that implicit behavior.
        builder.HasIndex(a => new { a.ActorUserId, a.Id });

        // Plan 5 adoption "staff active in window" check (DistrictService.GetAdoptionAsync): a
        // per-staff-member correlated EXISTS filtering ActorUserId AND range-restricting CreatedAt. The
        // bare (ActorUserId) index above can only seek to the actor, then must scan that actor's full
        // audit history to test CreatedAt; this composite index lets it seek directly into the recent
        // slice instead (review-fix contract, todos/084).
        builder.HasIndex(a => new { a.ActorUserId, a.CreatedAt });
    }
}
