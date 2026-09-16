using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class IepVersionConfiguration : IEntityTypeConfiguration<IepVersion>
{
    public void Configure(EntityTypeBuilder<IepVersion> builder)
    {
        builder.HasKey(v => v.Id);

        // The immutability trigger (migration AddPilotGatesPhase12) must be declared here: EF Core 7+
        // otherwise saves with an OUTPUT clause, which SQL Server rejects on a table with any trigger
        // ("cannot have any enabled triggers … OUTPUT clause without INTO"). SQLite ignores it.
        builder.ToTable("IepVersions", t => t.HasTrigger("TR_IepVersions_Immutable"));

        builder.Property(v => v.DocumentType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(v => v.Title).HasMaxLength(200);

        // Restrict (not Cascade): a finalized IepVersion is an immutable legal record and must
        // not be silently destroyed by deleting its SchoolStudent (or a cascade from School/District).
        builder.HasOne(v => v.SchoolStudent)
            .WithMany()
            .HasForeignKey(v => v.SchoolStudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // UNIQUE: the DB-enforced backstop for monotonic VersionNumber per student. FinalizeAsync's
        // serializable transaction prevents the read-then-insert race on SQL Server, but the unique
        // index guarantees the legal-record invariant even if two finalizes slip through (the loser
        // fails with a unique violation and rolls back). Also backs per-student listing + next-number.
        builder.HasIndex(v => new { v.SchoolStudentId, v.VersionNumber }).IsUnique();
    }
}
