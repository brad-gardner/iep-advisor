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

        // Audit-viewer read path (P2): the actor-scoped, CreatedAt-ordered query the district audit-log
        // viewer runs. The bare (ActorUserId) index above doesn't cover the CreatedAt ordering. Keyset
        // pagination orders by Id, but this index still serves the actor + date-range filter selection.
        builder.HasIndex(a => new { a.ActorUserId, a.CreatedAt });
    }
}
