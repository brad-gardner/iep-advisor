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
    public string? ExternalStudentId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public DisabilityCategory? DisabilityCategory { get; set; }
    public string? HomeLanguage { get; set; }
    public DateTime? IepDate { get; set; }
    public DateTime? AnnualReviewDueDate { get; set; }
    public DateTime? EtrDate { get; set; }
    public DateTime? ReevaluationDueDate { get; set; }

    /// <summary>The school the student belongs to. REQUIRED for a DistrictAdmin (no implicit school);
    /// optional for SchoolAdmin/Teacher (must be absent or equal their own school).</summary>
    public int? SchoolId { get; set; }
}

/// <summary>Full-replacement edit of a student's record (PUT semantics: a null clears an optional field).</summary>
public class UpdateSchoolStudentModel
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? StateCode { get; set; }
    public string? ExternalStudentId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public DisabilityCategory? DisabilityCategory { get; set; }
    public string? HomeLanguage { get; set; }
    public DateTime? IepDate { get; set; }
    public DateTime? AnnualReviewDueDate { get; set; }
    public DateTime? EtrDate { get; set; }
    public DateTime? ReevaluationDueDate { get; set; }
}

public class ExitStudentModel
{
    public ExitReason ExitReason { get; set; } = ExitReason.Other;
    public DateTime? ExitedAt { get; set; }
}

/// <summary>
/// Dashboard "needs attention" narrowing for the roster (same predicates as the district dashboard
/// tiles): <see cref="NoCaseManager"/> = no active lead whose staff profile is still active in the
/// district; <see cref="NoLinkedParent"/> = no accepted, active parent link.
/// </summary>
public enum StudentAttention
{
    NoCaseManager,
    NoLinkedParent
}

/// <summary>Roster search/filter/paging input. <see cref="Status"/> null means every status ("All").</summary>
public class StudentSearchQuery
{
    public string? Query { get; set; }
    public int? SchoolId { get; set; }
    public StudentStatus? Status { get; set; } = StudentStatus.Active;
    public GradeLevel? Grade { get; set; }
    public StudentAttention? Attention { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class BulkAssignCaseManagerModel
{
    public List<int> StudentIds { get; set; } = new();
    public int UserId { get; set; }
}

public class BulkAssignResultModel
{
    public int Updated { get; set; }
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

    /// <summary>Equals <c>Status == Active</c>; kept for existing callers.</summary>
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
