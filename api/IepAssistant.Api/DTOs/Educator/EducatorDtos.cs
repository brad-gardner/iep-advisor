using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Educator;

public class CreateSchoolStudentRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(2)]
    public string? StateCode { get; set; }

    [MaxLength(50)]
    public string? GradeLevel { get; set; }

    [MaxLength(100)]
    public string? DisabilityCategory { get; set; }

    /// <summary>Target school. REQUIRED for a DistrictAdmin; ignored/validated for SchoolAdmin/Teacher
    /// (must be absent or equal their own school).</summary>
    public int? SchoolId { get; set; }
}

public class EducatorProfileDto
{
    public int StaffProfileId { get; set; }
    public int UserId { get; set; }
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int DistrictId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public bool IsActive { get; set; }
    public string? StateCode { get; set; }
    public string? Title { get; set; }
    public string? Credentials { get; set; }
}

public class SchoolStudentDto
{
    public int Id { get; set; }
    public int SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? StateCode { get; set; }
    public string? GradeLevel { get; set; }
    public string? DisabilityCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>An active staff↔student access grant for the "Assigned staff" panel.</summary>
public class StudentStaffAccessDto
{
    public int AccessId { get; set; }
    public int StaffProfileId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrgRoleName { get; set; } = string.Empty;

    /// <summary>Per-student access role (Viewer/Collaborator/Owner), serialized as its string name.</summary>
    public string AccessRole { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
}

public class GrantStudentStaffAccessRequest
{
    [Required]
    public int StaffProfileId { get; set; }

    /// <summary>Optional access role ("Viewer"/"Collaborator"/"Owner"); defaults to Collaborator.</summary>
    public string? AccessRole { get; set; }
}
