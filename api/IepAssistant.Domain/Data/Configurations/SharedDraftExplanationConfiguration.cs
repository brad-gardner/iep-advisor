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

        // Restrict, not Cascade (pilot-gates plan, phase 1): SQL Server refuses to create an INSTEAD
        // OF UPDATE/DELETE trigger on a table that has an incoming cascading FK, and
        // SharedDraftRevisions needs exactly such a trigger for its own immutability. A revision is
        // never actually deleted by application code (it is superseded/withdrawn, not removed), so
        // this changes no real runtime behavior.
        builder.HasOne(e => e.SharedDraftRevision)
            .WithMany()
            .HasForeignKey(e => e.SharedDraftRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:1 per revision — generated once, never regenerated.
        builder.HasIndex(e => e.SharedDraftRevisionId).IsUnique();
    }
}
