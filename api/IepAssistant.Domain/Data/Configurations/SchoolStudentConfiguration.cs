using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SchoolStudentConfiguration : IEntityTypeConfiguration<SchoolStudent>
{
    public void Configure(EntityTypeBuilder<SchoolStudent> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.LastName).HasMaxLength(100);
        builder.Property(s => s.StateCode).HasMaxLength(2);
        builder.Property(s => s.GradeLevel).HasMaxLength(50);
        builder.Property(s => s.DisabilityCategory).HasMaxLength(100);

        builder.HasOne(s => s.School)
            .WithMany(sc => sc.Students)
            .HasForeignKey(s => s.SchoolId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.SchoolId);
    }
}
