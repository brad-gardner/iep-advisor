namespace IepAssistant.Domain.Entities;

/// <summary>
/// A question the parent plans to ask at the next IEP meeting — their own list, kept beside (not inside)
/// the AI-generated <see cref="MeetingPrepChecklist"/>. Plain text (markup is stripped before persisting).
/// Private to the family: every reader must hold <see cref="AccessRole.Viewer"/>+ on the child, writers
/// <see cref="AccessRole.Collaborator"/>+; nothing here is surfaced to a linked school team.
/// <see cref="Source"/> records where the text came from ("parent" typed it, or accepted an "advocate"
/// suggestion) so the record stays honest about what was AI-suggested.
/// </summary>
public class ParentPrepQuestion : BaseEntity, IAuditableEntity
{
    public const string SourceParent = "parent";
    public const string SourceAdvocate = "advocate";

    public int ChildProfileId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public int DisplayOrder { get; set; }
    public string Source { get; set; } = SourceParent;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
}
