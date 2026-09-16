using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class FamilyContactAttemptConfiguration : IEntityTypeConfiguration<FamilyContactAttempt>
{
    public void Configure(EntityTypeBuilder<FamilyContactAttempt> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Outcome).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Note).HasMaxLength(1000);

        builder.HasOne(a => a.SchoolStudent)
            .WithMany()
            .HasForeignKey(a => a.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.SchoolStudentId, a.AttemptedAt });
    }
}
