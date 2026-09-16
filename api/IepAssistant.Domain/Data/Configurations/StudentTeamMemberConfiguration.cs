using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class StudentTeamMemberConfiguration : IEntityTypeConfiguration<StudentTeamMember>
{
    public void Configure(EntityTypeBuilder<StudentTeamMember> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.TeamRole)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();
        builder.Property(m => m.Note).HasMaxLength(300);

        builder.HasOne(m => m.SchoolStudent)
            .WithMany()
            .HasForeignKey(m => m.SchoolStudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // One membership row per (student, user); reactivated rather than duplicated.
        builder.HasIndex(m => new { m.SchoolStudentId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);

        // Single active lead per student — DB backstop for the service's lead-swap logic. Filter is
        // bracket-quoted so SQL Server (filtered index) and SQLite (partial index, EnsureCreated) both accept it.
        builder.HasIndex(m => m.SchoolStudentId)
            .IsUnique()
            .HasFilter("[IsLead] = 1 AND [IsActive] = 1")
            .HasDatabaseName("IX_StudentTeamMembers_SchoolStudentId_ActiveLead");
    }
}
