using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).HasMaxLength(200).IsRequired();
        builder.Property(m => m.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(m => m.Location).HasMaxLength(300);
        builder.Property(m => m.VideoUrl).HasMaxLength(500);
        builder.Property(m => m.Notes).HasMaxLength(4000);
        builder.Property(m => m.CancelReason).HasMaxLength(500);

        // Enums stored as their names, mirroring the rest of the model (JsonStringEnumConverter on the API side).
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(m => m.SchoolStudent)
            .WithMany()
            .HasForeignKey(m => m.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: User already has other cascade-eligible relationships (e.g. SchoolStudent.CaseManager);
        // a second cascading path from User would be rejected by SQL Server, and a creator's account is
        // deactivated rather than deleted in this system anyway.
        builder.HasOne(m => m.CreatedByUser)
            .WithMany()
            .HasForeignKey(m => m.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.SchoolStudentId);
        // Calendar/list queries filter by status and range-scan on start time.
        builder.HasIndex(m => new { m.Status, m.StartsAtUtc });
        // Some reads range-scan StartsAtUtc with no equality filter on Status at all (e.g.
        // MeetingService.ListMineAsync's "mine, in this date range" query, and the plan-5 home's parent
        // next-meeting lookup once a caller has several linked children) — the leading (Status,
        // StartsAtUtc) index above can't be used as a seek for that shape, so StartsAtUtc gets its own.
        builder.HasIndex(m => m.StartsAtUtc);
    }
}
