using IepAssistant.Api.DTOs.District;
using IepAssistant.Api.DTOs.Obligations;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Home;

public class HomeMeetingDto
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

public class HomeDraftDto
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

/// <summary>Plan-6 shape — always [] until plan 6 lands.</summary>
public class HomeSharedDraftDto
{
    public int InstanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

/// <summary>Plan-7 shape — always [] until plan 7 lands.</summary>
public class HomeProviderRequestDto
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
}

/// <summary>Plan-7 shape — always [] until plan 7 lands.</summary>
public class HomeUnsignedDto
{
    public int VersionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public DateTime FinalizedAt { get; set; }
}

public class RosterAttentionDto
{
    public int NoLead { get; set; }
    public int NoFamily { get; set; }
    public int UnknownDates { get; set; }
    public int OverdueAnnual { get; set; }
    public int OverdueReeval { get; set; }
    public int Due30 { get; set; }
}

/// <summary>Sorted by student name, NEVER by staff.</summary>
public class CaseManagerRowDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? CaseManagerName { get; set; }
    public ObligationKind Kind { get; set; }
    public DateTime? DueDate { get; set; }
    public ObligationStatus Status { get; set; }
}

public class StaffHomeDto
{
    public StaffHomeVariant Variant { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string ScopeLabel { get; set; } = string.Empty;
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public List<HomeMeetingDto> MeetingsThisWeek { get; set; } = new();
    public List<ObligationDto> Obligations { get; set; } = new();
    public List<HomeDraftDto> Drafts { get; set; } = new();
    public List<HomeSharedDraftDto> SharedDraftsAwaitingFamily { get; set; } = new();
    public List<HomeSharedDraftDto> FamilyResponsesToReview { get; set; } = new();
    public List<HomeProviderRequestDto> ProviderRequestsIOwe { get; set; } = new();
    public RosterAttentionDto? RosterAttention { get; set; }

    /// <summary>Capped at 50 rows, sorted by student; <see cref="OverdueByCaseManagerTotal"/> carries the true count.</summary>
    public List<CaseManagerRowDto>? OverdueByCaseManager { get; set; }
    public int? OverdueByCaseManagerTotal { get; set; }
    public List<HomeUnsignedDto>? UnsignedFinalized { get; set; }
    public ComplianceSummaryDto? ComplianceSummary { get; set; }
}

public class ParentChildDto
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public bool HasSchoolLink { get; set; }
    public int? StudentId { get; set; }
}

public class ParentNextMeetingDto : HomeMeetingDto
{
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public int DaysUntil { get; set; }
}

public class ParentDocumentDto
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

public class ParentProgressReportDto
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public string ChildName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ParentHomeDto
{
    public string DisplayName { get; set; } = string.Empty;
    public ParentNextMeetingDto? NextMeeting { get; set; }
    public List<ParentDocumentDto> DocumentsToReview { get; set; } = new();
    public List<ParentChildDto> Children { get; set; } = new();
    public List<ParentProgressReportDto> RecentProgressReports { get; set; } = new();
    public List<string> SetupNotices { get; set; } = new();
}

public class StudentHomeDto
{
    public string DisplayName { get; set; } = string.Empty;
    public HomeMeetingDto? NextMeeting { get; set; }
    public string? WorkspaceNudge { get; set; }
    public int? LinkedStudentId { get; set; }
}

/// <summary><c>GET /api/home</c> — discriminated on <see cref="Kind"/> ('Staff' | 'Parent' | 'Student').</summary>
public class HomeDto
{
    public HomeKind Kind { get; set; }
    public DateTime GeneratedAt { get; set; }
    public StaffHomeDto? Staff { get; set; }
    public ParentHomeDto? Parent { get; set; }
    public StudentHomeDto? Student { get; set; }
}
