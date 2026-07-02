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

/// <summary>
/// Oversight dashboard aggregate (Phase 1, district-admin pilot readiness). DistrictAdmin sees the
/// whole district; SchoolAdmin sees only their own school's slice. Inactive schools and inactive
/// students are excluded from every count and list. An empty district yields a valid all-zero payload.
/// </summary>
public class DistrictDashboardModel
{
    public List<DashboardSchoolModel> Schools { get; set; } = new();
    public DashboardStaffSummaryModel StaffSummary { get; set; } = new();

    /// <summary>Pending + expired invites (revoked/accepted excluded), expired-first triage order.</summary>
    public List<DashboardInviteModel> InvitesNeedingAttention { get; set; } = new();

    /// <summary>Active students with zero active access rows whose grantee still has an ACTIVE
    /// StaffProfile — a student whose only grantee was deactivated appears here.</summary>
    public List<DashboardStudentModel> StudentsWithoutStaff { get; set; } = new();

    /// <summary>Active students with no accepted, active <c>ChildLink</c> bound to a
    /// <c>ChildProfile</c>; rows distinguish "invite pending" from "never invited".</summary>
    public List<DashboardNoParentStudentModel> StudentsWithoutParent { get; set; } = new();
}

/// <summary>Per-school active student count (active schools only).</summary>
public class DashboardSchoolModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ActiveStudentCount { get; set; }
}

public class DashboardStaffSummaryModel
{
    /// <summary>Active StaffProfiles in scope.</summary>
    public int ActiveCount { get; set; }

    /// <summary>Deactivated (IsActive=false) StaffProfiles in scope.</summary>
    public int DeactivatedCount { get; set; }

    /// <summary>Pending invite ROWS (unexpired, un-accepted, not revoked) — multiple pending invites
    /// count individually; expired invites are NOT counted here (they appear flagged in the list).</summary>
    public int InvitedCount { get; set; }
}

public class DashboardInviteModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public DateTime InviteExpiresAt { get; set; }

    /// <summary>"pending" while unexpired, "expired" once the window passes (matches
    /// <c>StaffPendingInviteModel</c>; revoked/accepted invites are excluded entirely).</summary>
    public string Status { get; set; } = "pending";
}

public class DashboardStudentModel
{
    public int SchoolStudentId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string SchoolName { get; set; } = string.Empty;
}

public class DashboardNoParentStudentModel : DashboardStudentModel
{
    /// <summary>True when an active, un-accepted <c>ChildLink</c> (parent invite) exists for the
    /// student; false means the parent was never invited.</summary>
    public bool ParentInvitePending { get; set; }
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
