using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class OfflineFamilyInputConfiguration : IEntityTypeConfiguration<OfflineFamilyInput>
{
    public void Configure(EntityTypeBuilder<OfflineFamilyInput> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Summary).IsRequired().HasMaxLength(4000);

        builder.HasOne(i => i.SchoolStudent)
            .WithMany()
            .HasForeignKey(i => i.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a Draft instance an offline input references must not vanish underneath it via cascade.
        builder.HasOne(i => i.DocumentInstance)
            .WithMany()
            .HasForeignKey(i => i.DocumentInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.SchoolStudentId, i.ReceivedAt });
    }
}
