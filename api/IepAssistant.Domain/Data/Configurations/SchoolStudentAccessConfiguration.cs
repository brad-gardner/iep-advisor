using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SchoolStudentAccessConfiguration : IEntityTypeConfiguration<SchoolStudentAccess>
{
    public void Configure(EntityTypeBuilder<SchoolStudentAccess> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne(a => a.SchoolStudent)
            .WithMany()
            .HasForeignKey(a => a.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.SchoolStudentId, a.UserId }).IsUnique();
    }
}
