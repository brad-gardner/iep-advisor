namespace IepAssistant.Services.Models;

/// <summary>
/// A StudentInvite returned from invite/accept flows.
/// </summary>
public class StudentInviteModel
{
    public int Id { get; set; }
    public string InviteEmail { get; set; } = string.Empty;
    public int? ChildProfileId { get; set; }
    public int? SchoolStudentId { get; set; }
    public bool IsActive { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Context shown on the consent screen before the invited student accepts: which side they're being
/// linked to (parent-side child first name, or school-side student first name + school name).
/// </summary>
public class StudentInvitePreviewModel
{
    /// <summary>"Parent" or "Educator" — who initiated this invite.</summary>
    public string InviteSource { get; set; } = string.Empty;

    /// <summary>The first name they're being linked under (child first name, or school-student first name).</summary>
    public string LinkedToFirstName { get; set; } = string.Empty;

    /// <summary>Set only for educator-initiated (school-side) invites.</summary>
    public string? SchoolName { get; set; }

    public DateTime? InviteExpiresAt { get; set; }
}

/// <summary>
/// The result of accepting a student invite — reflects the converged single StudentProfile.
/// </summary>
public class AcceptStudentInviteModel
{
    public int StudentProfileId { get; set; }
    public int? ChildProfileId { get; set; }
    public int? SchoolStudentId { get; set; }
    public DateTime? ConsentAcceptedAt { get; set; }
}
