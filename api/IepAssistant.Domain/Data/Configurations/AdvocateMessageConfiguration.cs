using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Domain.Data.Configurations;

public class AdvocateMessageConfiguration : IEntityTypeConfiguration<AdvocateMessage>
{
    public const int UserContentMaxLength = 2000;
    public const int AssistantContentMaxLength = 32000;

    public void Configure(EntityTypeBuilder<AdvocateMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.ContentMarkdown).HasMaxLength(AssistantContentMaxLength).IsRequired();

        builder.HasIndex(m => new { m.AdvocateThreadId, m.CreatedAt });
    }
}
