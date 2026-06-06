using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.StudentInvites;

public class InviteStudentRequest
{
    [Required]
    [EmailAddress]
    public string StudentEmail { get; set; } = string.Empty;
}

public class AcceptStudentInviteRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>Must be true to activate the account — the consent gate.</summary>
    public bool ConsentAccepted { get; set; }
}

public class StudentInviteDto
{
    public int Id { get; set; }
    public string InviteEmail { get; set; } = string.Empty;
    public int? ChildProfileId { get; set; }
    public int? SchoolStudentId { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
}

public class StudentInvitePreviewDto
{
    public string InviteSource { get; set; } = string.Empty;
    public string LinkedToFirstName { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
}

public class AcceptedStudentInviteDto
{
    public int StudentProfileId { get; set; }
    public int? ChildProfileId { get; set; }
    public int? SchoolStudentId { get; set; }
    public DateTime? ConsentAcceptedAt { get; set; }
}
