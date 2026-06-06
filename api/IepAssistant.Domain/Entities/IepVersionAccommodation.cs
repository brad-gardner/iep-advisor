namespace IepAssistant.Domain.Entities;

/// <summary>Frozen copy of an <see cref="IepDraftAccommodation"/>; LineageId carried verbatim. Immutable.</summary>
public class IepVersionAccommodation : BaseEntity
{
    public int IepVersionId { get; set; }
    public string? Category { get; set; }
    public string? Text { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }

    public IepVersion IepVersion { get; set; } = null!;
}
