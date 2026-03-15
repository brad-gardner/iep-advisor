namespace IepAssistant.Api.DTOs.Children;

public class ChildProfileDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? GradeLevel { get; set; }
    public string? DisabilityCategory { get; set; }
    public string? SchoolDistrict { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Role { get; set; }
}
