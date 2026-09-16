using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Scope).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.BlobPath).HasMaxLength(500);
        builder.Property(j => j.Error).HasMaxLength(2000);

        // Restrict: a district lookup row referenced by many export jobs; never cascade-destroy it.
        builder.HasOne(j => j.District)
            .WithMany()
            .HasForeignKey(j => j.DistrictId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(j => j.SchoolStudent)
            .WithMany()
            .HasForeignKey(j => j.SchoolStudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.DistrictId, j.Status });
        builder.HasIndex(j => j.SchoolStudentId);
    }
}
