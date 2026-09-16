using System.ComponentModel.DataAnnotations;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Meetings;

/// <summary>A suggested participant for a new meeting (team, accepted family, student account).</summary>
public class DefaultParticipantDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public TeamRole TeamRole { get; set; }
    public bool IsFamily { get; set; }
    public bool IsStudent { get; set; }
}

public class MeetingParticipantDto
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

public class MeetingDto
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
    public List<MeetingParticipantDto> Participants { get; set; } = new();
    public InviteStatus? MyInviteStatus { get; set; }
    public bool CanManage { get; set; }
}

public class ParticipantInputRequest
{
    public int? UserId { get; set; }

    [MaxLength(200)]
    public string? ExternalName { get; set; }

    [MaxLength(256)]
    [EmailAddress]
    public string? ExternalEmail { get; set; }

    public TeamRole TeamRole { get; set; } = TeamRole.Other;
    public bool? IsRequired { get; set; }
}

public class CreateMeetingRequest
{
    public MeetingType Type { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    [Required]
    public DateTime? StartsAtUtc { get; set; }

    [MaxLength(64)]
    public string? TimeZoneId { get; set; }

    [Range(15, 480)]
    public int? DurationMinutes { get; set; }

    [MaxLength(300)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    public int? DocumentInstanceId { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    /// <summary>Omitted ⇒ defaults from the student's team + accepted family links + student account.</summary>
    public List<ParticipantInputRequest>? Participants { get; set; }
}

/// <summary>All fields optional; a null/omitted field leaves the current value untouched. Participants,
/// when present, fully replaces the roster.</summary>
public class UpdateMeetingRequest
{
    public MeetingType? Type { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    public DateTime? StartsAtUtc { get; set; }

    [MaxLength(64)]
    public string? TimeZoneId { get; set; }

    [Range(15, 480)]
    public int? DurationMinutes { get; set; }

    [MaxLength(300)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    public int? DocumentInstanceId { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }

    public List<ParticipantInputRequest>? Participants { get; set; }
}

public class CancelMeetingRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public class SetMeetingStatusRequest
{
    [Required]
    public MeetingStatus? Status { get; set; }
}

public class RsvpRequest
{
    [Required]
    public InviteStatus? Status { get; set; }
}

public class TokenRsvpRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    public InviteStatus? Status { get; set; }
}

public class AttendanceItemRequest
{
    [Required]
    public int ParticipantId { get; set; }

    public bool Attended { get; set; }

    [MaxLength(500)]
    public string? ExcusalNote { get; set; }
}

public class AttendanceRequest
{
    [Required]
    public List<AttendanceItemRequest> Attendance { get; set; } = new();
}

/// <summary>Minimal, non-participant view of a meeting returned by the anonymous token RSVP endpoints
/// (review-fix contract addition 1) — no participants, notes, or video URL.</summary>
public class MeetingSummaryDto
{
    public int Id { get; set; }
    public string StudentFirstName { get; set; } = string.Empty;
    public MeetingType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Location { get; set; }
    public MeetingStatus Status { get; set; }
}

public class MeetingRsvpPreviewDto
{
    public MeetingSummaryDto Meeting { get; set; } = null!;
    public InviteStatus Status { get; set; }
}
