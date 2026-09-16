using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>Which branch of <see cref="HomeModel"/> is populated.</summary>
public enum HomeKind
{
    Staff,
    Parent,
    Student
}

/// <summary>The staff-tier home layout (plan 5, decisions 2-4). Maps from <see cref="OrgRoleIds"/>: Teacher
/// -&gt; CaseManager, RelatedServiceProvider -&gt; Provider, GeneralEducator -&gt; GeneralEducator,
/// SchoolAdmin -&gt; SchoolAdmin, DistrictAdmin -&gt; DistrictAdmin.</summary>
public enum StaffHomeVariant
{
    CaseManager,
    Provider,
    GeneralEducator,
    SchoolAdmin,
    DistrictAdmin
}

/// <summary>One line of a home's "this week" meeting list, or a parent/student's next meeting.</summary>
public class HomeMeetingModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public MeetingType Type { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public InviteStatus? MyInviteStatus { get; set; }
    public MeetingStatus Status { get; set; }
}

/// <summary>A Draft <see cref="DocumentInstance"/> the caller is on the team for, or last edited.</summary>
public class HomeDraftModel
{
    public int InstanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public DateTime? LastEditedAt { get; set; }
    public int CompletenessPercent { get; set; }
    public int RequiredMissing { get; set; }
}

/// <summary>Plan-6 shape (shared drafts / family responses) — always empty until plan 6 lands.</summary>
public class HomeSharedDraftModel
{
    public int InstanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

/// <summary>Plan-7 shape (provider requests) — always empty until plan 7 lands.</summary>
public class HomeProviderRequestModel
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
}

/// <summary>Plan-7 shape (unsigned finalized documents) — always empty until plan 7 lands.</summary>
public class HomeUnsignedModel
{
    public int VersionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime FinalizedAt { get; set; }
}

/// <summary>Admin-only roster snapshot on the staff home — same predicates/counts as the roster
/// "attention" filter and the district compliance board (<see cref="Implementations.StudentAttentionRules"/>).</summary>
public class RosterAttentionModel
{
    public int NoLead { get; set; }
    public int NoFamily { get; set; }
    public int UnknownDates { get; set; }
    public int OverdueAnnual { get; set; }
    public int OverdueReeval { get; set; }
    public int Due30 { get; set; }
}

/// <summary>One overdue/at-risk row for an admin's "by case manager" table — sorted by student name,
/// NEVER by staff (no ranking language anywhere in this model).</summary>
public class CaseManagerRowModel
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? CaseManagerName { get; set; }
    public ObligationKind Kind { get; set; }
    public DateTime? DueDate { get; set; }
    public ObligationStatus Status { get; set; }
}

/// <summary>The staff-tier home (plan 5, decisions 2-4).</summary>
public class StaffHomeModel
{
    public StaffHomeVariant Variant { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ScopeLabel { get; set; } = string.Empty;
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public List<HomeMeetingModel> MeetingsThisWeek { get; set; } = new();

    /// <summary>DueSoon + Overdue where the caller is lead. Empty for admin variants (the compliance board covers scope-wide obligations).</summary>
    public List<ObligationModel> Obligations { get; set; } = new();
    public List<HomeDraftModel> Drafts { get; set; } = new();
    public List<HomeSharedDraftModel> SharedDraftsAwaitingFamily { get; set; } = new();
    public List<HomeSharedDraftModel> FamilyResponsesToReview { get; set; } = new();
    public List<HomeProviderRequestModel> ProviderRequestsIOwe { get; set; } = new();

    /// <summary>Admin variants only.</summary>
    public RosterAttentionModel? RosterAttention { get; set; }

    /// <summary>Admin variants only.</summary>
    public List<CaseManagerRowModel>? OverdueByCaseManager { get; set; }

    /// <summary>Admin variants only (plan-7 shape, always empty until plan 7 lands).</summary>
    public List<HomeUnsignedModel>? UnsignedFinalized { get; set; }

    /// <summary>DistrictAdmin only — the same numbers as the compliance board with no filters.</summary>
    public ComplianceSummaryModel? ComplianceSummary { get; set; }
}

public class ParentChildModel
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public bool HasSchoolLink { get; set; }
    public int? StudentId { get; set; }
}

/// <summary>The parent home's next-meeting card: a <see cref="HomeMeetingModel"/> plus which child it is for.</summary>
public class ParentNextMeetingModel : HomeMeetingModel
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public int DaysUntil { get; set; }
}

public enum ParentDocumentKind
{
    Finalized,
    SharedDraft
}

public class ParentDocumentModel
{
    public ParentDocumentKind Kind { get; set; }
    public int Id { get; set; }
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int? VersionNumber { get; set; }
    public DateTime Date { get; set; }
    public string LinkPath { get; set; } = string.Empty;
}

public class ParentProgressReportModel
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>The parent home (plan 5, decision 5) — meeting-relative.</summary>
public class ParentHomeModel
{
    public string DisplayName { get; set; } = string.Empty;
    public ParentNextMeetingModel? NextMeeting { get; set; }

    /// <summary>Finalized versions now; shared drafts (plan 6) are always [] until plan 6 lands.</summary>
    public List<ParentDocumentModel> DocumentsToReview { get; set; } = new();
    public List<ParentChildModel> Children { get; set; } = new();
    public List<ParentProgressReportModel> RecentProgressReports { get; set; } = new();

    /// <summary>Demoted setup notices, e.g. "No school link yet".</summary>
    public List<string> SetupNotices { get; set; } = new();
}

/// <summary>The student home (plan 5): next meeting + a workspace nudge.</summary>
public class StudentHomeModel
{
    public string DisplayName { get; set; } = string.Empty;
    public HomeMeetingModel? NextMeeting { get; set; }
    public string? WorkspaceNudge { get; set; }
    public int? LinkedStudentId { get; set; }
}

/// <summary>Top-level <c>GET /api/home</c> payload, discriminated on <see cref="Kind"/>.</summary>
public class HomeModel
{
    public HomeKind Kind { get; set; }
    public DateTime GeneratedAt { get; set; }
    public StaffHomeModel? Staff { get; set; }
    public ParentHomeModel? Parent { get; set; }
    public StudentHomeModel? Student { get; set; }
}
