using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepDraftConfiguration : IEntityTypeConfiguration<IepDraft>
{
    public void Configure(EntityTypeBuilder<IepDraft> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(d => d.Title).HasMaxLength(200);

        builder.HasOne(d => d.SchoolStudent)
            .WithMany()
            .HasForeignKey(d => d.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.SchoolStudentId);
    }
}
