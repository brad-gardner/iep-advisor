namespace IepAssistant.Services.Models;

public class OnboardEducatorModel
{
    public string DistrictName { get; set; } = string.Empty;
    public string SchoolName { get; set; } = string.Empty;
    public string? StateCode { get; set; }
}

public class EducatorProfileModel
{
    public int TeacherProfileId { get; set; }
    public int UserId { get; set; }
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int DistrictId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
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
}

public class SchoolStudentModel
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
