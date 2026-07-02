namespace IepAssistant.Services.Models;

/// <summary>
/// The resolved org identity for an authenticated staff user, derived per-request from an ACTIVE
/// <c>StaffProfile</c> (authorization is DB-backed, not claim-backed). Returned by
/// <c>IOrgAccessService.GetStaffContextAsync</c>; <c>null</c> means the user has no active staff
/// profile (not staff, or deactivated). <see cref="SchoolId"/> is <c>null</c> for a DistrictAdmin
/// not bound to a single school.
/// </summary>
public sealed record StaffContext(
    int StaffProfileId,
    int UserId,
    int DistrictId,
    int? SchoolId,
    int OrgRoleId);
