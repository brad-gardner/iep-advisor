using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Centralizes org authorization, resolved per-request from the DB (the JWT keeps only role=Educator +
/// SecurityStamp; org identity is never trusted from claims). Replaces the five duplicated SchoolId
/// permission checks across the educator/draft/version/invite/workspace services. Admins are a
/// player-coach superset of Teacher within their scope; Teachers still require an explicit
/// <c>SchoolStudentAccess</c> row.
/// </summary>
public interface IOrgAccessService
{
    /// <summary>
    /// Returns the caller's org context, or <c>null</c> if no ACTIVE <c>StaffProfile</c> exists
    /// (not staff, or deactivated).
    /// </summary>
    Task<StaffContext?> GetStaffContextAsync(int userId, CancellationToken ct = default);

    /// <summary>
    /// True if the caller may act on the given school. DistrictAdmin: the school must belong to their
    /// district. SchoolAdmin/Teacher: the school must be their own <c>SchoolId</c>.
    /// </summary>
    Task<bool> CanActOnSchoolAsync(int userId, int schoolId, CancellationToken ct = default);

    /// <summary>
    /// True if the caller may act on the given student at or above <paramref name="minRole"/>.
    /// District/School admins pass within their scope regardless of <c>SchoolStudentAccess</c>
    /// (player-coach superset); Teachers require an active <c>SchoolStudentAccess</c> row with
    /// Role &gt;= <paramref name="minRole"/>.
    /// </summary>
    Task<bool> CanActOnStudentAsync(int userId, int schoolStudentId, AccessRole minRole, CancellationToken ct = default);
}
