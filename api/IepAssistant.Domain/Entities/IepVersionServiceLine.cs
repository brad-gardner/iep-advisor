namespace IepAssistant.Domain.Entities;

/// <summary>Frozen copy of an <see cref="IepDraftServiceLine"/>; LineageId carried verbatim. Immutable.</summary>
public class IepVersionServiceLine : BaseEntity
{
    public int IepVersionId { get; set; }
    public string? ServiceType { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Location { get; set; }
    public string? ProviderRole { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }

    public IepVersion IepVersion { get; set; } = null!;
}
