namespace IepAssistant.Api.DTOs.District;

/// <summary>
/// District-wide (or single-school) obligation-bucket counts — same numbers a roster search with the
/// matching <c>attention</c> filter returns for the same scope. Counts by student only — never by staff
/// (no ranking, no per-staff scores).
/// </summary>
public class ComplianceSummaryDto
{
    public int OverdueAnnual { get; set; }
    public int OverdueReeval { get; set; }
    public int Due30 { get; set; }
    public int Due60 { get; set; }
    public int UnknownDates { get; set; }
    public int NoLead { get; set; }
    public int ActiveStudents { get; set; }
}

public class ComplianceSchoolRowDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int ActiveStudents { get; set; }
    public int OverdueAnnual { get; set; }
    public int OverdueReeval { get; set; }
    public int Due30 { get; set; }
    public int Due60 { get; set; }
    public int UnknownDates { get; set; }
    public int NoLead { get; set; }
}

public class ComplianceBoardDto
{
    public DateTime GeneratedAt { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public ComplianceSummaryDto Summary { get; set; } = new();
    public List<ComplianceSchoolRowDto> BySchool { get; set; } = new();

    /// <summary>Each count key mapped to the roster query string that reproduces its rows (e.g. "overdueAnnual" -> "attention=OverdueAnnual").</summary>
    public Dictionary<string, string> Drill { get; set; } = new();
}

public class AdoptionSchoolDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int StaffActive { get; set; }
    public int StaffTotal { get; set; }
    public int DraftsStarted { get; set; }
    public int DraftsFinalized { get; set; }
}

public class AdoptionDto
{
    public int Days { get; set; }
    public int StaffActiveLast14 { get; set; }
    public int StaffTotal { get; set; }
    public List<AdoptionSchoolDto> BySchool { get; set; } = new();
    public int DraftsStarted { get; set; }
    public int DraftsFinalized { get; set; }

    /// <summary>States the exact "active" rule for the UI to display verbatim.</summary>
    public string ActiveRule { get; set; } = string.Empty;
}

public class EngagementSchoolDto
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int StudentsWithFamilyLink { get; set; }
    public int ActiveStudents { get; set; }
}

public class EngagementDto
{
    public int StudentsWithFamilyLink { get; set; }
    public int ActiveStudents { get; set; }
    public int DraftsShared { get; set; }
    public int ResponsesReceived { get; set; }
    public List<EngagementSchoolDto> BySchool { get; set; } = new();
}
