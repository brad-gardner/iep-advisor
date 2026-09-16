namespace IepAssistant.Services.Models;

/// <summary>
/// District-wide (or single-school) obligation-bucket counts — the SAME numbers a roster search with the
/// matching <see cref="StudentAttention"/> filter returns for the same scope (see
/// <see cref="Implementations.StudentAttentionRules"/>). Counts by STUDENT only — never by staff member
/// (no ranking, no per-staff scores; plan 5 constraint).
/// </summary>
public class ComplianceSummaryModel
{
    public int OverdueAnnual { get; set; }
    public int OverdueReeval { get; set; }
    public int Due30 { get; set; }
    public int Due60 { get; set; }

    /// <summary>Due within the board's caller-chosen [From, To] window (default today..today+60) — the
    /// only bucket the optional date range affects; <see cref="Due30"/>/<see cref="Due60"/> are always
    /// anchored on today regardless of the requested range (review-fix contract addition 1).</summary>
    public int DueInRange { get; set; }
    public int UnknownDates { get; set; }
    public int NoLead { get; set; }
    public int ActiveStudents { get; set; }
}

/// <summary>One school's row of the compliance board — same fields as <see cref="ComplianceSummaryModel"/> minus the district roll-up.</summary>
public class ComplianceSchoolRowModel
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int ActiveStudents { get; set; }
    public int OverdueAnnual { get; set; }
    public int OverdueReeval { get; set; }
    public int Due30 { get; set; }
    public int Due60 { get; set; }
    public int DueInRange { get; set; }
    public int UnknownDates { get; set; }
    public int NoLead { get; set; }
}

/// <summary>
/// DistrictAdmin: whole district, optionally narrowed to one school. SchoolAdmin: forced to their own
/// school regardless of a caller-supplied <c>schoolId</c>. <see cref="From"/>/<see cref="To"/> (default:
/// today .. today+60 days) bound ONLY the <see cref="ComplianceSummaryModel.DueInRange"/> bucket; Due30/
/// Due60/overdue counts are always anchored on today regardless of the requested range, so every OTHER
/// drill key's roster rows equal the tile count no matter what range is selected (review-fix contract
/// addition 1). <see cref="Drill"/> maps each count key to the roster query string that reproduces its
/// rows (e.g. "overdueAnnual" -> "attention=OverdueAnnual"; "dueInRange" carries the current from/to).
/// </summary>
public class ComplianceBoardModel
{
    public DateTime GeneratedAt { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public ComplianceSummaryModel Summary { get; set; } = new();
    public List<ComplianceSchoolRowModel> BySchool { get; set; } = new();
    public Dictionary<string, string> Drill { get; set; } = new();
}

public class AdoptionSchoolModel
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int StaffActive { get; set; }
    public int StaffTotal { get; set; }
    public int DraftsStarted { get; set; }
    public int DraftsFinalized { get; set; }
}

/// <summary>
/// Staff/draft activity in the last <see cref="Days"/> days (default 30). "Active" = at least one FERPA
/// access-audit entry (view/edit/share/finalize/export) for that staff member's user id in the window —
/// see <see cref="ActiveRule"/>, which states the exact rule for the UI to display verbatim.
/// </summary>
public class AdoptionModel
{
    public int Days { get; set; }

    /// <summary>Named for contract-shape compatibility; represents activity within <see cref="Days"/> (not necessarily 14).</summary>
    public int StaffActiveLast14 { get; set; }
    public int StaffTotal { get; set; }
    public List<AdoptionSchoolModel> BySchool { get; set; } = new();
    public int DraftsStarted { get; set; }
    public int DraftsFinalized { get; set; }
    public string ActiveRule { get; set; } = string.Empty;
}

public class EngagementSchoolModel
{
    public int SchoolId { get; set; }
    public string SchoolName { get; set; } = string.Empty;
    public int StudentsWithFamilyLink { get; set; }
    public int ActiveStudents { get; set; }
}

/// <summary>Family-engagement evidence tile. DraftsShared/ResponsesReceived are plan-6 placeholders (always 0).</summary>
public class EngagementModel
{
    public int StudentsWithFamilyLink { get; set; }
    public int ActiveStudents { get; set; }
    public int DraftsShared { get; set; }
    public int ResponsesReceived { get; set; }
    public List<EngagementSchoolModel> BySchool { get; set; } = new();
}
