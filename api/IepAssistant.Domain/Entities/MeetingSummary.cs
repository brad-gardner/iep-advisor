namespace IepAssistant.Domain.Entities;

/// <summary>
/// A plain-language, family-facing summary of a Held/Continued <see cref="Meeting"/> (plan 6, decision
/// 6): AI-drafted, human-editable, and sent only on an explicit staff action. One row per meeting (1:1).
/// </summary>
public class MeetingSummary : BaseEntity, IAuditableEntity
{
    public int MeetingId { get; set; }

    public string Body { get; set; } = string.Empty;
    public MeetingSummaryStatus Status { get; set; } = MeetingSummaryStatus.Draft;

    public DateTime? GeneratedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int? SentByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Meeting Meeting { get; set; } = null!;
}
