using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SchoolStudentConfiguration : IEntityTypeConfiguration<SchoolStudent>
{
    public void Configure(EntityTypeBuilder<SchoolStudent> builder)
    {
        builder.HasKey(s => s.Id);
        // IsActive is a computed mirror of Status (the column was dropped by AddRosterLifecycleTeamsAndImports).
        builder.Ignore(s => s.IsActive);
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100);
        builder.Property(s => s.StateCode).HasMaxLength(2);
        builder.Property(s => s.ExternalStudentId).HasMaxLength(64);

        // Enums stored as their names (mirrors JsonStringEnumConverter). Legacy free text is converted
        // by the AddRosterLifecycleTeamsAndImports migration before these columns are narrowed.
        builder.Property(s => s.GradeLevel).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.DisabilityCategory).HasConversion<string>().HasMaxLength(64);
        builder.Property(s => s.LegacyDisabilityText).HasMaxLength(200);
        builder.Property(s => s.HomeLanguage).HasMaxLength(32).HasDefaultValue("en");
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(StudentStatus.Active);
        builder.Property(s => s.ExitReason).HasConversion<string>().HasMaxLength(32);

        builder.Property(s => s.IepDate).HasColumnType("date");
        builder.Property(s => s.AnnualReviewDueDate).HasColumnType("date");
        builder.Property(s => s.EtrDate).HasColumnType("date");
        builder.Property(s => s.ReevaluationDueDate).HasColumnType("date");

        builder.HasOne(s => s.School)
            .WithMany(sc => sc.Students)
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        // Denormalized district (maintained by the service on create/transfer). Restrict: the School FK
        // already cascades from District → School → SchoolStudent; a second cascading path would be
        // rejected by SQL Server.
        builder.HasOne(s => s.District)
            .WithMany()
            .HasForeignKey(s => s.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CaseManager)
            .WithMany()
            .HasForeignKey(s => s.CaseManagerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.SchoolId);
        builder.HasIndex(s => s.CaseManagerUserId);
        builder.HasIndex(s => new { s.DistrictId, s.Status });
        // Plan 5: the compliance board and the roster's Overdue/DueSoon/Unknown attention filters
        // (StudentAttentionRules) range-scan these due-date columns across every active student in scope.
        builder.HasIndex(s => s.AnnualReviewDueDate);
        builder.HasIndex(s => s.ReevaluationDueDate);

        // Student identity key = (DistrictId, ExternalStudentId); unique only where an id is present.
        // Bracket-quoted filter is accepted by both SQL Server and SQLite (EnsureCreated in tests).
        builder.HasIndex(s => new { s.DistrictId, s.ExternalStudentId })
            .IsUnique()
            .HasFilter("[ExternalStudentId] IS NOT NULL");
    }
}
