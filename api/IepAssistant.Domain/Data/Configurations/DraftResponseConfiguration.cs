using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DraftResponseConfiguration : IEntityTypeConfiguration<DraftResponse>
{
    public void Configure(EntityTypeBuilder<DraftResponse> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Text).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.StaffReply).HasMaxLength(2000);
        builder.Property(r => r.TargetRowId).HasMaxLength(64);

        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(r => r.SharedDraftRevision)
            .WithMany()
            .HasForeignKey(r => r.SharedDraftRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ParentUser)
            .WithMany()
            .HasForeignKey(r => r.ParentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The parent's own responses list, and the staff converge/list-by-instance reads (via a join on
        // SharedDraftRevisionId), both filter on (revision, status).
        builder.HasIndex(r => new { r.SharedDraftRevisionId, r.Status });
        builder.HasIndex(r => new { r.SharedDraftRevisionId, r.ParentUserId });
    }
}
