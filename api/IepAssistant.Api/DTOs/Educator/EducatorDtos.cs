using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Educator;

public class OnboardEducatorRequest
{
    [Required]
    [MaxLength(200)]
    public string DistrictName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string SchoolName { get; set; } = string.Empty;

    [MaxLength(2)]
    public string? StateCode { get; set; }
}

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
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? StateCode { get; set; }
    public string? GradeLevel { get; set; }
    public string? DisabilityCategory { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
