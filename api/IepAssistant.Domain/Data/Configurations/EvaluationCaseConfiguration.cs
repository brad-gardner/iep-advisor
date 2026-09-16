using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class EvaluationCaseConfiguration : IEntityTypeConfiguration<EvaluationCase>
{
    public void Configure(EntityTypeBuilder<EvaluationCase> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ReferralSource).HasMaxLength(200);
        builder.Property(c => c.ConsentBlobPath).HasMaxLength(500);
        builder.Property(c => c.ConsentFileName).HasMaxLength(260);
        builder.Property(c => c.DueDateOverrideReason).HasMaxLength(1000);
        builder.Property(c => c.DeterminationRationale).HasMaxLength(4000);

        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.EligibilityOutcome).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(c => c.SchoolStudent)
            .WithMany()
            .HasForeignKey(c => c.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Informational cross-references to the ETR document/version produced from this case. Restrict
        // (not SetNull) — SQL Server refuses a SetNull path here alongside the direct Cascade FK to
        // SchoolStudent below (both DocumentInstance and AuthoredDocumentVersion are themselves reachable
        // from SchoolStudent, so a second cascading path to EvaluationCases would create the "multiple
        // cascade paths" DDL error). Restrict also matches AuthoredDocumentVersionConfiguration's own
        // choice for its SchoolStudent FK: never silently sever a provenance pointer.
        builder.HasOne<DocumentInstance>()
            .WithMany()
            .HasForeignKey(c => c.EtrDocumentInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AuthoredDocumentVersion>()
            .WithMany()
            .HasForeignKey(c => c.EtrAuthoredVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.SchoolStudentId, c.Status });

        // At most one non-Closed case per student (plan 7, decision 1) — a filtered unique index mirrors
        // StudentTeamMemberConfiguration's single-active-lead pattern.
        builder.HasIndex(c => c.SchoolStudentId)
            .IsUnique()
            .HasFilter("[Status] <> 'Closed'")
            .HasDatabaseName("IX_EvaluationCases_OneOpenPerStudent");
    }
}
