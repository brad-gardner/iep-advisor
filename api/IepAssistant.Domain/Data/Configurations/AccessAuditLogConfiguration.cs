using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AccessAuditLogConfiguration : IEntityTypeConfiguration<AccessAuditLog>
{
    public void Configure(EntityTypeBuilder<AccessAuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ResourceType)
            .HasMaxLength(50)
            .IsRequired();

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
    }
}
