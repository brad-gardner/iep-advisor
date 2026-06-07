using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// DB-backed org authorization (see <see cref="IOrgAccessService"/>). Every check first resolves an
/// ACTIVE <c>StaffProfile</c> — a deactivated or missing profile denies everything (returns null /
/// false), which together with the JWT SecurityStamp check (Program.cs) gives immediate deactivation.
/// </summary>
public class OrgAccessService : IOrgAccessService
{
    private readonly ApplicationDbContext _context;

    public OrgAccessService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StaffContext?> GetStaffContextAsync(int userId, CancellationToken ct = default)
    {
        return await _context.StaffProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive)
            .Select(p => new StaffContext(p.Id, p.UserId, p.DistrictId, p.SchoolId, p.OrgRoleId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> CanActOnSchoolAsync(int userId, int schoolId, CancellationToken ct = default)
    {
        var ctx = await GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return false;

        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            // DistrictAdmin may act on any school within their district.
            return await _context.Schools
                .AsNoTracking()
                .AnyAsync(s => s.Id == schoolId && s.DistrictId == ctx.DistrictId, ct);
        }

        // SchoolAdmin / Teacher: only their own school.
        return ctx.SchoolId != null && ctx.SchoolId.Value == schoolId;
    }

    public async Task<bool> CanActOnStudentAsync(int userId, int schoolStudentId, AccessRole minRole, CancellationToken ct = default)
    {
        var ctx = await GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return false;

        // Resolve the student's school once (also rules out non-existent students).
        var studentSchoolId = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => (int?)s.SchoolId)
            .FirstOrDefaultAsync(ct);
        if (studentSchoolId == null)
            return false;

        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            // Player-coach superset: any active student-bearing school in the district, no
            // SchoolStudentAccess row required.
            return await _context.Schools
                .AsNoTracking()
                .AnyAsync(s => s.Id == studentSchoolId.Value && s.DistrictId == ctx.DistrictId, ct);
        }

        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            // Player-coach superset within the admin's own school, no SchoolStudentAccess required.
            return ctx.SchoolId != null && ctx.SchoolId.Value == studentSchoolId.Value;
        }

        // Teacher: must be in their own school AND hold an active SchoolStudentAccess >= minRole.
        if (ctx.SchoolId == null || ctx.SchoolId.Value != studentSchoolId.Value)
            return false;

        // AccessRole is persisted as a string (HasConversion<string>); a SQL-side `>= minRole` would
        // compare alphabetically ("Collaborator" < "Viewer"), not by enum rank. Materialize the role
        // and compare in memory (matches the documented fix in IepVersionService.CheckStudentAccessAsync).
        var role = await _context.SchoolStudentAccesses
            .AsNoTracking()
            .Where(a => a.SchoolStudentId == schoolStudentId && a.UserId == userId && a.IsActive)
            .Select(a => (AccessRole?)a.Role)
            .FirstOrDefaultAsync(ct);

        return role != null && role.Value >= minRole;
    }
}
