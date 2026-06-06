namespace IepAssistant.Domain.Entities;

/// <summary>Frozen copy of an <see cref="IepDraftSection"/>; LineageId carried verbatim. Immutable.</summary>
public class IepVersionSection : BaseEntity
{
    public int IepVersionId { get; set; }
    public IepSectionKind SectionKind { get; set; }
    public string? RichText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }

    public IepVersion IepVersion { get; set; } = null!;
}
