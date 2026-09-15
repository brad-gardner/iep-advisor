namespace IepAssistant.Domain.Entities;

/// <summary>
/// A parent-authored "about my child at home" note (strength, concern, what works, priority). Private
/// to the family by default; when <see cref="IsShared"/> is true it becomes visible to the linked
/// school team and enters the student evidence bundle that grounds AI assistance. Parent prep notes,
/// analyses and advocacy goals are deliberately NOT this — those never reach the school.
/// </summary>
public class ParentContribution : BaseEntity, IAuditableEntity
{
    public int ChildProfileId { get; set; }
    public ParentContributionKind Kind { get; set; } = ParentContributionKind.Other;
    public string Text { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ChildProfile ChildProfile { get; set; } = null!;
}
