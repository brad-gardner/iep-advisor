using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ChildProfileConfiguration : IEntityTypeConfiguration<ChildProfile>
{
    public void Configure(EntityTypeBuilder<ChildProfile> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.GradeLevel).HasMaxLength(20);
        builder.Property(c => c.DisabilityCategory).HasMaxLength(100);
        builder.Property(c => c.SchoolDistrict).HasMaxLength(200);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction to avoid SQL Server cascade-cycle (IepDocument cascades from ChildProfile).
        // IepDocumentService.DeleteAsync clears CurrentIepDocumentId before deleting the referenced IEP.
        builder.HasOne(c => c.CurrentIepDocument)
            .WithMany()
            .HasForeignKey(c => c.CurrentIepDocumentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(c => c.UserId);
    }
}
