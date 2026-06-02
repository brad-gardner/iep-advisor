namespace IepAssistant.Domain.Entities;

/// <summary>
/// P8a. A single self-advocacy entry the student authored. <see cref="IsShareable"/> defaults to
/// false — entries are PRIVATE until the student explicitly shares them; only shareable entries are
/// ever exposed to educators/parents. There is intentionally NO foreign key from a pulled IEP/meeting-prep
/// copy back to this row: pull-into actions copy <see cref="Content"/> by value (P8b frontend), so the
/// snapshot is independent and survives the student later editing/deleting the entry.
/// </summary>
public class StudentWorkspaceEntry : BaseEntity, IAuditableEntity
{
    public int StudentWorkspaceId { get; set; }
    public StudentEntryKind EntryKind { get; set; }
    public string Content { get; set; } = string.Empty;

    /// <summary>Private until the student shares: only true entries are readable by educators/parents.</summary>
    public bool IsShareable { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public StudentWorkspace StudentWorkspace { get; set; } = null!;
}
