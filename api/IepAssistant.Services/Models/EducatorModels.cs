using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class EducatorProfileModel
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

public class CreateSchoolStudentModel
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? StateCode { get; set; }
    public string? GradeLevel { get; set; }
    public string? DisabilityCategory { get; set; }

    /// <summary>The school the student belongs to. REQUIRED for a DistrictAdmin (no implicit school);
    /// optional for SchoolAdmin/Teacher (must be absent or equal their own school).</summary>
    public int? SchoolId { get; set; }
}

public class SchoolStudentModel
{
    public int Id { get; set; }
    public int SchoolId { get; set; }

    /// <summary>The student's school name — populated so the UI (esp. DistrictAdmin's district-wide
    /// roster) can group/filter by school.</summary>
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

/// <summary>An active staff↔student access grant, for the "Assigned staff" panel.</summary>
public class StudentStaffAccessModel
{
    public int AccessId { get; set; }
    public int StaffProfileId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrgRoleName { get; set; } = string.Empty;
    public AccessRole AccessRole { get; set; }
    public DateTime GrantedAt { get; set; }
}

/// <summary>Input for granting (or updating) a staff member's access to a student. Defaults to Collaborator.</summary>
public class GrantStudentStaffAccessModel
{
    public int StaffProfileId { get; set; }
    public AccessRole AccessRole { get; set; } = AccessRole.Collaborator;
}
