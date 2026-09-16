using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class MeetingParticipantModel
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalEmail { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public TeamRole TeamRole { get; set; }
    public bool IsRequired { get; set; }
    public InviteStatus InviteStatus { get; set; }
    public bool? Attended { get; set; }
    public DateTime? ExcusedAt { get; set; }
    public string? ExcusalNote { get; set; }
    public bool IsFamily { get; set; }
    public bool IsStudent { get; set; }
}

public class MeetingModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public MeetingType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? VideoUrl { get; set; }
    public MeetingStatus Status { get; set; }
    public int? DocumentInstanceId { get; set; }
    public string? Notes { get; set; }
    public int Sequence { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<MeetingParticipantModel> Participants { get; set; } = new();
    public InviteStatus? MyInviteStatus { get; set; }
    public bool CanManage { get; set; }
}

/// <summary>One entry of a caller-supplied participant roster (explicit create, or a full-replacement update).</summary>
/// <summary>A suggested participant for a new meeting (the same set CreateAsync applies when the client
/// sends no participants), with display data so the schedule form can pre-check real users.</summary>
public class DefaultParticipantModel
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TeamRole TeamRole { get; set; }
    public bool IsFamily { get; set; }
    public bool IsStudent { get; set; }
}

public class ParticipantInputModel
{
    public int? UserId { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalEmail { get; set; }
    public TeamRole TeamRole { get; set; } = TeamRole.Other;
    public bool? IsRequired { get; set; }
}

public class CreateMeetingModel
{
    public MeetingType Type { get; set; }
    public string? Title { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public string? TimeZoneId { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? VideoUrl { get; set; }
    public int? DocumentInstanceId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Null ⇒ defaults from team + accepted family links + student account (+ creator).</summary>
    public List<ParticipantInputModel>? Participants { get; set; }
}

/// <summary>All fields optional: a null/omitted field leaves the current value untouched (Location/VideoUrl/Notes
/// can be explicitly cleared by sending an empty string, which IS distinguishable from omitted). Participants,
/// when present, is a full replacement of the roster.</summary>
public class UpdateMeetingModel
{
    public MeetingType? Type { get; set; }
    public string? Title { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public string? TimeZoneId { get; set; }
    public int? DurationMinutes { get; set; }
    public string? Location { get; set; }
    public string? VideoUrl { get; set; }
    public int? DocumentInstanceId { get; set; }
    public string? Notes { get; set; }
    public List<ParticipantInputModel>? Participants { get; set; }
}

public class AttendanceItemModel
{
    public int ParticipantId { get; set; }
    public bool Attended { get; set; }
    public string? ExcusalNote { get; set; }
}

/// <summary>The result of a token RSVP lookup: a minimal view of the meeting plus the caller's current status.</summary>
public class MeetingRsvpPreviewModel
{
    public MeetingModel Meeting { get; set; } = null!;
    public InviteStatus Status { get; set; }
}
