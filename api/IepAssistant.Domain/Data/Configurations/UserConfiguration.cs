using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.State).HasMaxLength(2);
        // Tolerant string conversion: any legacy/un-migrated "User" row (or any other
        // unexpected stored value) reads back as Parent rather than throwing — so the app
        // fails safe (toward least privilege) during a rolling deploy before the data
        // migration lands, instead of crashing list/login reads.
        var roleConverter = new ValueConverter<UserRole, string>(
            r => r.ToString(),
            s => ParseRole(s));

        builder.Property(u => u.Role)
            .HasConversion(roleConverter)
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(u => u.StripeCustomerId).HasMaxLength(256);
        builder.Property(u => u.StripeSubscriptionId).HasMaxLength(256);
        builder.Property(u => u.SubscriptionStatus).HasMaxLength(20).HasDefaultValue("none");

        // Plan 4: revocable ICS calendar subscription token. Filtered unique index (bracket-quoted so both
        // SQL Server and SQLite/EnsureCreated accept it) — null until a user first requests a feed URL.
        builder.Property(u => u.CalendarFeedToken).HasMaxLength(32);
        builder.HasIndex(u => u.CalendarFeedToken)
            .IsUnique()
            .HasFilter("[CalendarFeedToken] IS NOT NULL");

        // Ignore computed property
        builder.Ignore(u => u.FullName);
    }

    private static UserRole ParseRole(string stored) =>
        Enum.TryParse<UserRole>(stored, out var role) ? role : UserRole.Parent;
}
