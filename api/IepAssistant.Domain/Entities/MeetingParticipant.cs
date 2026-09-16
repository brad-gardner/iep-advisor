namespace IepAssistant.Domain.Entities;

/// <summary>
/// One invitee on a <see cref="Meeting"/>: a staff/family/student <see cref="User"/>, or an external
/// (non-user) invitee identified by name/email only. <see cref="RsvpToken"/> is a 32-hex-char token
/// (unique) backing the no-login email RSVP link; it stays usable up until the meeting starts.
/// <see cref="IsFamily"/>/<see cref="IsStudent"/> are resolved at write time from the participant's
/// relationship to the student (an accepted <see cref="ChildLink"/> / the student's own
/// <see cref="StudentProfile"/>) rather than caller-supplied, so the badge is correct regardless of which
/// <see cref="TeamRole"/> they were invited under.
/// </summary>
public class MeetingParticipant : BaseEntity, IAuditableEntity
{
    public int MeetingId { get; set; }
    public int? UserId { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalEmail { get; set; }
    public TeamRole TeamRole { get; set; } = TeamRole.Other;
    public bool IsRequired { get; set; } = true;
    public InviteStatus InviteStatus { get; set; } = InviteStatus.Pending;
    public bool? Attended { get; set; }
    public DateTime? ExcusedAt { get; set; }
    public string? ExcusalNote { get; set; }
    public bool IsFamily { get; set; }
    public bool IsStudent { get; set; }

    /// <summary>32 hex chars, unique. The no-login RSVP link token.</summary>
    public string RsvpToken { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Meeting Meeting { get; set; } = null!;
    public User? User { get; set; }
}
