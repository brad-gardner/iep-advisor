using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class MeetingPrepChecklistConfiguration : IEntityTypeConfiguration<MeetingPrepChecklist>
{
    public void Configure(EntityTypeBuilder<MeetingPrepChecklist> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Status).HasMaxLength(20).IsRequired();
        builder.Property(m => m.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(m => m.ChildProfile)
            .WithMany()
            .HasForeignKey(m => m.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.IepDocument)
            .WithMany()
            .HasForeignKey(m => m.IepDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.ChildProfileId);
        builder.HasIndex(m => m.IepDocumentId);
    }
}
