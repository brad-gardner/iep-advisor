namespace IepAssistant.Domain.Entities;

/// <summary>Frozen copy of an <see cref="IepDraftTransitionItem"/>; LineageId carried verbatim. Immutable.</summary>
public class IepVersionTransitionItem : BaseEntity
{
    public int IepVersionId { get; set; }
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }

    public IepVersion IepVersion { get; set; } = null!;
}
