using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class SharedDraftExplanationConfiguration : IEntityTypeConfiguration<SharedDraftExplanation>
{
    public void Configure(EntityTypeBuilder<SharedDraftExplanation> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExplanationJson).IsRequired();

        builder.HasOne(e => e.SharedDraftRevision)
            .WithMany()
            .HasForeignKey(e => e.SharedDraftRevisionId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:1 per revision — generated once, never regenerated.
        builder.HasIndex(e => e.SharedDraftRevisionId).IsUnique();
    }
}
