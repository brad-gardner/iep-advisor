using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class EvaluatorAssignmentConfiguration : IEntityTypeConfiguration<EvaluatorAssignment>
{
    public void Configure(EntityTypeBuilder<EvaluatorAssignment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Domain).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasOne(a => a.EvaluationCase)
            .WithMany(c => c.Assignments)
            .HasForeignKey(a => a.EvaluationCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a staff user with an existing evaluator assignment must not be deletable out from
        // under it (mirrors other User FK's in the roster/team model).
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.EvaluationCaseId);
        builder.HasIndex(a => new { a.UserId, a.SubmittedAt, a.DueDate });
    }
}
