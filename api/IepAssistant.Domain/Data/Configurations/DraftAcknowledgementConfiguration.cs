using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class DraftAcknowledgementConfiguration : IEntityTypeConfiguration<DraftAcknowledgement>
{
    public void Configure(EntityTypeBuilder<DraftAcknowledgement> builder)
    {
        builder.HasKey(a => a.Id);

        // Restrict, not Cascade (pilot-gates plan, phase 1): SQL Server refuses to create an INSTEAD
        // OF UPDATE/DELETE trigger on a table that has an incoming cascading FK, and
        // SharedDraftRevisions needs exactly such a trigger for its own immutability. A revision is
        // never actually deleted by application code (it is superseded/withdrawn, not removed), so
        // this changes no real runtime behavior.
        builder.HasOne(a => a.SharedDraftRevision)
            .WithMany()
            .HasForeignKey(a => a.SharedDraftRevisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotent upsert key: one acknowledgement per (revision, user).
        builder.HasIndex(a => new { a.SharedDraftRevisionId, a.UserId }).IsUnique();
    }
}
