using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class ParentPrepQuestionConfiguration : IEntityTypeConfiguration<ParentPrepQuestion>
{
    public const int TextMaxLength = 500;
    public const int SourceMaxLength = 32;

    public void Configure(EntityTypeBuilder<ParentPrepQuestion> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Text).HasMaxLength(TextMaxLength).IsRequired();
        builder.Property(q => q.Source).HasMaxLength(SourceMaxLength).IsRequired();

        builder.HasOne(q => q.ChildProfile)
            .WithMany()
            .HasForeignKey(q => q.ChildProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.ChildProfileId, q.DisplayOrder });
    }
}
