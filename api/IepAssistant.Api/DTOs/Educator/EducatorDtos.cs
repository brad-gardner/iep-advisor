using System.ComponentModel.DataAnnotations;
using IepAssistant.Domain.Entities;

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

    [MaxLength(64)]
    public string? ExternalStudentId { get; set; }

    public GradeLevel? GradeLevel { get; set; }
    public DisabilityCategory? DisabilityCategory { get; set; }

    [MaxLength(32)]
    public string? HomeLanguage { get; set; }

    public DateTime? IepDate { get; set; }
    public DateTime? AnnualReviewDueDate { get; set; }
    public DateTime? EtrDate { get; set; }
    public DateTime? ReevaluationDueDate { get; set; }

    /// <summary>Target school. REQUIRED for a DistrictAdmin; ignored/validated for SchoolAdmin/Teacher
    /// (must be absent or equal their own school).</summary>
    public int? SchoolId { get; set; }
}

/// <summary>Full-replacement edit (PUT): omitted/null optional fields are cleared.</summary>
public class UpdateSchoolStudentRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(2)]
    public string? StateCode { get; set; }

    [MaxLength(64)]
    public string? ExternalStudentId { get; set; }

    public GradeLevel? GradeLevel { get; set; }
    public DisabilityCategory? DisabilityCategory { get; set; }

    [MaxLength(32)]
    public string? HomeLanguage { get; set; }

    public DateTime? IepDate { get; set; }
    public DateTime? AnnualReviewDueDate { get; set; }
    public DateTime? EtrDate { get; set; }
    public DateTime? ReevaluationDueDate { get; set; }
}

public class ExitStudentRequest
{
    /// <summary>Nullable so [Required] actually rejects an omitted value (a non-nullable enum defaults silently).</summary>
    [Required]
    public ExitReason? ExitReason { get; set; }
    public DateTime? ExitedAt { get; set; }
}

public class TransferStudentRequest
{
    [Required]
    public int NewSchoolId { get; set; }
}

public class BulkAssignCaseManagerRequest
{
    /// <summary>1–500 ids (the service enforces the same cap).</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(500)]
    public List<int> StudentIds { get; set; } = new();

    [Required]
    public int UserId { get; set; }
}

public class BulkAssignResultDto
{
    public int Updated { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
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
    public string? ExternalStudentId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public DisabilityCategory? DisabilityCategory { get; set; }
    public string? LegacyDisabilityText { get; set; }
    public string? HomeLanguage { get; set; }
    public StudentStatus Status { get; set; }
    public DateTime? ExitedAt { get; set; }
    public ExitReason? ExitReason { get; set; }
    public int? CaseManagerUserId { get; set; }
    public string? CaseManagerName { get; set; }
    public DateTime? IepDate { get; set; }
    public DateTime? AnnualReviewDueDate { get; set; }
    public DateTime? EtrDate { get; set; }
    public DateTime? ReevaluationDueDate { get; set; }

    /// <summary>Equals <c>status == Active</c>; kept for compatibility.</summary>
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>An IEP team member: functional role + effective permission tier.</summary>
public class StudentTeamMemberDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int StaffProfileId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrgRoleName { get; set; } = string.Empty;
    public TeamRole TeamRole { get; set; }
    public bool IsLead { get; set; }
    public AccessRole AccessRole { get; set; }
    public bool IsActive { get; set; }
    public DateTime AddedAt { get; set; }
}

/// <summary>A staff member eligible to join a student's team (GET .../team/eligible).</summary>
public class EligibleStaffDto
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
}

public class AddTeamMemberRequest
{
    [Required]
    public int StaffProfileId { get; set; }

    /// <summary>Nullable so [Required] actually rejects an omitted value (a non-nullable enum defaults silently).</summary>
    [Required]
    public TeamRole? TeamRole { get; set; }

    public bool? IsLead { get; set; }
    public AccessRole? AccessRole { get; set; }
}

public class UpdateTeamMemberRequest
{
    public TeamRole? TeamRole { get; set; }
    public AccessRole? AccessRole { get; set; }
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
