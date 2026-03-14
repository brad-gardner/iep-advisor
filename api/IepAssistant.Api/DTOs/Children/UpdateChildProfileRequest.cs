using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Children;

public class UpdateChildProfileRequest
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? GradeLevel { get; set; }

    [MaxLength(100)]
    public string? DisabilityCategory { get; set; }

    [MaxLength(200)]
    public string? SchoolDistrict { get; set; }
}
