using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class GoalRecordConfiguration : IEntityTypeConfiguration<GoalRecord>
{
    public void Configure(EntityTypeBuilder<GoalRecord> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.GoalText).IsRequired().HasMaxLength(4000);
        builder.Property(g => g.Domain).HasMaxLength(200);
        builder.Property(g => g.Baseline).HasMaxLength(4000);
        builder.Property(g => g.TargetCriteria).HasMaxLength(4000);
        builder.Property(g => g.MeasurementMethod).HasMaxLength(2000);
        builder.Property(g => g.Timeframe).HasMaxLength(500);
        builder.Property(g => g.StatusReason).HasMaxLength(1000);
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(g => g.SchoolStudent)
            .WithMany()
            .HasForeignKey(g => g.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: an immutable AuthoredDocumentVersion/DocumentInstance must never be deletable while a
        // GoalRecord snapshot references it.
        builder.HasOne(g => g.AuthoredDocumentVersion)
            .WithMany()
            .HasForeignKey(g => g.AuthoredDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.DocumentInstance)
            .WithMany()
            .HasForeignKey(g => g.DocumentInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        // One GoalRecord per (version, lineage) — the projection's core invariant.
        builder.HasIndex(g => new { g.AuthoredDocumentVersionId, g.LineageId }).IsUnique();
        builder.HasIndex(g => new { g.SchoolStudentId, g.Status });
        builder.HasIndex(g => new { g.SchoolStudentId, g.LineageId });
    }
}
