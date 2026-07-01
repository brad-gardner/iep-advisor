using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.District;

/// <summary>District overview for the caller's district (any active staff may read).</summary>
public class DistrictOverviewDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
    public int ActiveSchoolCount { get; set; }
    public int ActiveStaffCount { get; set; }
}

/// <summary>A school in the caller's district, with active student/staff counts (directory + picker).</summary>
public class DistrictSchoolDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
    public int ActiveStudentCount { get; set; }
    public int ActiveStaffCount { get; set; }
}

public class CreateSchoolRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2)]
    [MinLength(2)]
    public string? StateCode { get; set; }
}

public class UpdateSchoolRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2)]
    [MinLength(2)]
    public string? StateCode { get; set; }
}
