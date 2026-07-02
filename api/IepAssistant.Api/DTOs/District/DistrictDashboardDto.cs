namespace IepAssistant.Api.DTOs.District;

/// <summary>
/// Oversight dashboard aggregate for the caller's district (Phase 1, district-admin pilot readiness).
/// DistrictAdmin sees the whole district; SchoolAdmin sees only their own school's slice; Teacher,
/// parents, and students are denied. Inactive schools and inactive students are excluded from every
/// count and list; an empty district returns a valid all-zero payload.
/// </summary>
public class DistrictDashboardDto
{
    public List<DashboardSchoolDto> Schools { get; set; } = new();
    public DashboardStaffSummaryDto StaffSummary { get; set; } = new();

    /// <summary>Pending + expired invites (revoked/accepted excluded), expired-first triage order.</summary>
    public List<DashboardInviteDto> InvitesNeedingAttention { get; set; } = new();

    /// <summary>Active students with no active staff access grant from an active staff member.</summary>
    public List<DashboardStudentDto> StudentsWithoutStaff { get; set; } = new();

    /// <summary>Active students with no accepted parent link; rows distinguish pending invites
    /// from never-invited.</summary>
    public List<DashboardNoParentStudentDto> StudentsWithoutParent { get; set; } = new();
}

/// <summary>Per-school active student count (active schools only).</summary>
public class DashboardSchoolDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ActiveStudentCount { get; set; }
}

public class DashboardStaffSummaryDto
{
    public int ActiveCount { get; set; }
    public int DeactivatedCount { get; set; }

    /// <summary>Pending invite rows (unexpired, un-accepted, not revoked).</summary>
    public int InvitedCount { get; set; }
}

public class DashboardInviteDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public DateTime InviteExpiresAt { get; set; }

    /// <summary>"pending" while unexpired, "expired" once the window passes.</summary>
    public string Status { get; set; } = "pending";
}

public class DashboardStudentDto
{
    public int SchoolStudentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string SchoolName { get; set; } = string.Empty;
}

public class DashboardNoParentStudentDto : DashboardStudentDto
{
    /// <summary>True when a parent invite is pending; false means the parent was never invited.</summary>
    public bool ParentInvitePending { get; set; }
}
