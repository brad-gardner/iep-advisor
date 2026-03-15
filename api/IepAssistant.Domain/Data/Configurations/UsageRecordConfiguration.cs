using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class UsageRecordConfiguration : IEntityTypeConfiguration<UsageRecord>
{
    public void Configure(EntityTypeBuilder<UsageRecord> builder)
    {
        builder.HasKey(ur => ur.Id);

        builder.Property(ur => ur.OperationType)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(ur => ur.User)
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ur => ur.ChildProfile)
            .WithMany()
            .HasForeignKey(ur => ur.ChildProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ur => ur.UserId);
        builder.HasIndex(ur => ur.ChildProfileId);
    }
}
