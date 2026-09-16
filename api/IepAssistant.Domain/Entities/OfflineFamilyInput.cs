namespace IepAssistant.Domain.Entities;

/// <summary>
/// Family input received through an offline channel (plan 7, decision 7) — e.g. a phone call or a paper
/// note — recorded by staff so a school-only student's family participation is captured without a family
/// account. Optionally tied to a specific <see cref="DocumentInstance"/>; counts toward the meeting
/// brief's "family input received" checklist item and appears in the student evidence bundle.
/// </summary>
public class OfflineFamilyInput : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public int? DocumentInstanceId { get; set; }

    public DateTime ReceivedAt { get; set; }
    public FamilyContactMethod Method { get; set; }
    public string Summary { get; set; } = string.Empty;

    public int RecordedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public DocumentInstance? DocumentInstance { get; set; }
}
