namespace IepAssistant.Domain.Entities;

/// <summary>
/// P8a. The single self-advocacy workspace for a Student-role user (one per student, unique
/// <see cref="UserId"/>). Auto-created on first access. Holds the student's strengths, interests,
/// accommodation requests, meeting statements, and AI-interview answers as
/// <see cref="StudentWorkspaceEntry"/> rows. Entries are private until the student marks them shareable.
/// </summary>
public class StudentWorkspace : BaseEntity, IAuditableEntity
{
    public int UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public User User { get; set; } = null!;
    public ICollection<StudentWorkspaceEntry> Entries { get; set; } = new List<StudentWorkspaceEntry>();
}
