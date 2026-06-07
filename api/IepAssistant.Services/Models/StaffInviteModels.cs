namespace IepAssistant.Services.Models;

/// <summary>Input for creating a staff invite. <see cref="SchoolId"/> is required for SchoolAdmin/Teacher
/// invites and must be null for a DistrictAdmin invite (validated against the caller's scope).</summary>
public class CreateStaffInviteModel
{
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public int? SchoolId { get; set; }
}

/// <summary>A created/resent staff invite. <see cref="InviteUrl"/> is only populated when the
/// <c>Email:ExposeLinksForTesting</c> gate is satisfied (Development + no ACS connection string).</summary>
public class StaffInviteModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public DateTime InviteExpiresAt { get; set; }

    /// <summary>Gated raw accept URL for e2e/testing; null in normal operation. Never log or persist.</summary>
    public string? InviteUrl { get; set; }
}

/// <summary>A staff directory list: active+inactive profiles plus pending/expired invites, scope-filtered.</summary>
public class StaffListModel
{
    public List<StaffMemberModel> Members { get; set; } = new();
    public List<StaffPendingInviteModel> PendingInvites { get; set; } = new();
}

public class StaffMemberModel
{
    public int StaffProfileId { get; set; }
    public int UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public bool IsActive { get; set; }
}

public class StaffPendingInviteModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public int OrgRoleId { get; set; }
    public string OrgRoleName { get; set; } = string.Empty;
    public int? SchoolId { get; set; }
    public string? SchoolName { get; set; }
    public DateTime InviteExpiresAt { get; set; }

    /// <summary>"pending" while unexpired, "expired" once the window passes (revoked/accepted are excluded).</summary>
    public string Status { get; set; } = "pending";
}

/// <summary>
/// Result of deactivating a staff member. Surfaces the students for which the deactivated staff held
/// the ONLY active non-admin access ("solely-owned") so the UI can prompt for reassignment. Admins keep
/// scope-wide visibility regardless (no orphaning by construction); this is purely an advisory hint.
/// </summary>
public class DeactivateStaffResult
{
    public int SolelyOwnedStudentCount { get; set; }
    public List<DeactivatedStaffStudentModel> SolelyOwnedStudents { get; set; } = new();
}

public class DeactivatedStaffStudentModel
{
    public int StudentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Preview of a staff invite for the anonymous accept page.</summary>
public class StaffInvitePreviewModel
{
    public string DistrictName { get; set; } = string.Empty;
    public string? SchoolName { get; set; }
    public string RoleName { get; set; } = string.Empty;

    /// <summary>The invited email — returned in full; the recipient needs to know which address to use.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>"valid" | "expired" | "invalid" (claimed/revoked/unknown all map to "invalid").</summary>
    public string Status { get; set; } = "valid";
}

/// <summary>Input for the anonymous accept endpoint.</summary>
public class AcceptStaffInviteModel
{
    public string Token { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Outcome of accept: carries an <see cref="AuthResult"/> (JWT + user) so the frontend
/// auto-logs-in exactly as after login/register-district.</summary>
public class AcceptStaffInviteResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public AuthResult? AuthResult { get; init; }

    public static AcceptStaffInviteResult Failure(string message) => new() { Success = false, Message = message };
    public static AcceptStaffInviteResult Ok(AuthResult auth) => new() { Success = true, AuthResult = auth };
}
