namespace IepAssistant.Domain.Entities;

/// <summary>
/// A scheduled IEP-team meeting for a student (plan 4, decision 1). <see cref="Sequence"/> is the RFC 5545
/// SEQUENCE counter: it increments whenever start time, duration, location or video link changes (also the
/// trigger for participant change-notifications), and again on cancel. Participants are
/// <see cref="MeetingParticipant"/> rows (staff, family, the student's own account, or an external invitee).
/// </summary>
public class Meeting : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public MeetingType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = "America/New_York";
    public int DurationMinutes { get; set; } = 60;
    public string? Location { get; set; }
    public string? VideoUrl { get; set; }
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    public int? DocumentInstanceId { get; set; }
    public string? Notes { get; set; }

    /// <summary>RFC 5545 SEQUENCE. Bumped on any change to start/duration/location/video and on cancel.</summary>
    public int Sequence { get; set; }

    public int CreatedByUserId { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public User? CreatedByUser { get; set; }
    public ICollection<MeetingParticipant> Participants { get; set; } = new List<MeetingParticipant>();
}
