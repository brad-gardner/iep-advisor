namespace IepAssistant.Services.Models;

public class DistrictOverviewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
    public int ActiveSchoolCount { get; set; }
    public int ActiveStaffCount { get; set; }
}

public class DistrictSchoolModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
    public int ActiveStudentCount { get; set; }
    public int ActiveStaffCount { get; set; }
}

public class CreateSchoolModel
{
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
}

public class UpdateSchoolModel
{
    public string Name { get; set; } = string.Empty;
    public string? StateCode { get; set; }
}
