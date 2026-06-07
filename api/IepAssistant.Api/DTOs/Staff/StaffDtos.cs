using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Staff;

// ----------------------------------------------------------------- Requests (authenticated)

public class CreateStaffInviteRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int OrgRoleId { get; set; }

    public int? SchoolId { get; set; }
}

// ----------------------------------------------------------------- Requests (anonymous accept)

public class AcceptStaffInviteRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(200)]
    public string Password { get; set; } = string.Empty;
}

// ----------------------------------------------------------------- Responses

/// <summary>A created/resent staff invite. <see cref="InviteUrl"/> is only present under the gated
/// <c>Email:ExposeLinksForTesting</c> condition (Development + no ACS connection string).</summary>
public class StaffInviteDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public DateTime InviteExpiresAt { get; set; }
    public string? InviteUrl { get; set; }
}

public class StaffListDto
{
    public List<StaffMemberDto> Members { get; set; } = new();
    public List<StaffPendingInviteDto> PendingInvites { get; set; } = new();
}

public class StaffMemberDto
{
    public int StaffProfileId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public bool IsActive { get; set; }
}

public class StaffPendingInviteDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public DateTime InviteExpiresAt { get; set; }
    public string Status { get; set; } = "pending";
}

/// <summary>Response to a staff deactivation: carries the reassignment hint (students the deactivated
/// staff solely owned among non-admin staff).</summary>
public class DeactivateStaffResponseDto
{
    public int SolelyOwnedStudentCount { get; set; }
    public List<DeactivatedStaffStudentDto> SolelyOwnedStudents { get; set; } = new();
}

public class DeactivatedStaffStudentDto
{
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class StaffInvitePreviewDto
{
    public string DistrictName { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = "valid";
}
