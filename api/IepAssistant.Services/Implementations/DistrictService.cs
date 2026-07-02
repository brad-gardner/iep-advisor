using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// District/school management. Org authorization is resolved per-request from the caller's active
/// <see cref="StaffContext"/> (DB-backed, never claim-backed). Reads are open to any active staff in
/// the district; mutations are DistrictAdmin-only and confined to the caller's own district.
/// </summary>
public class DistrictService : IDistrictService
{
    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly ILogger<DistrictService> _logger;

    public DistrictService(ApplicationDbContext context, IOrgAccessService orgAccess, ILogger<DistrictService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _logger = logger;
    }

    public async Task<ServiceResult<DistrictOverviewModel>> GetOverviewAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<DistrictOverviewModel>.FailureResult("Educator profile not found.");

        var district = await _context.Districts
            .AsNoTracking()
            .Where(d => d.Id == ctx.DistrictId)
            .Select(d => new { d.Id, d.Name, d.StateCode })
            .FirstOrDefaultAsync(ct);
        if (district == null)
            return ServiceResult<DistrictOverviewModel>.FailureResult("District not found.");

        var activeSchoolCount = await _context.Schools
            .AsNoTracking()
            .CountAsync(s => s.DistrictId == ctx.DistrictId && s.IsActive, ct);

        var activeStaffCount = await _context.StaffProfiles
            .AsNoTracking()
            .CountAsync(p => p.DistrictId == ctx.DistrictId && p.IsActive, ct);

        return ServiceResult<DistrictOverviewModel>.SuccessResult(new DistrictOverviewModel
        {
            Id = district.Id,
            Name = district.Name,
            StateCode = district.StateCode,
            ActiveSchoolCount = activeSchoolCount,
            ActiveStaffCount = activeStaffCount
        });
    }

    public async Task<ServiceResult<DistrictDashboardModel>> GetDashboardAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<DistrictDashboardModel>.FailureResult("Educator profile not found.");
        if (ctx.OrgRoleId == OrgRoleIds.Teacher)
            return ServiceResult<DistrictDashboardModel>.FailureResult("You do not have permission to view the district dashboard.");

        var isDistrictAdmin = ctx.OrgRoleId == OrgRoleIds.DistrictAdmin;

        // A SchoolAdmin without a school binding has nothing to oversee — return the valid empty payload
        // (mirrors EducatorService.GetStudentsAsync, which returns an empty roster for the same case).
        if (!isDistrictAdmin && ctx.SchoolId == null)
            return ServiceResult<DistrictDashboardModel>.SuccessResult(new DistrictDashboardModel());

        var now = DateTime.UtcNow;

        // ------- Per-school active student counts (active schools only) -------
        var schoolsQuery = _context.Schools
            .AsNoTracking()
            .Where(s => s.DistrictId == ctx.DistrictId && s.IsActive);
        if (!isDistrictAdmin)
            schoolsQuery = schoolsQuery.Where(s => s.Id == ctx.SchoolId!.Value);

        var schools = await schoolsQuery
            .OrderBy(s => s.Name)
            .Select(s => new DashboardSchoolModel
            {
                Id = s.Id,
                Name = s.Name,
                ActiveStudentCount = _context.SchoolStudents.Count(st => st.SchoolId == s.Id && st.IsActive)
            })
            .ToListAsync(ct);

        // ------- Staff status summary (SchoolAdmin: own school only, DistrictAdmin rows hidden —
        // mirrors StaffInviteService.ListAsync). Rows bound to a deactivated school are excluded
        // ("every count and list"); school-null rows are district-scoped admins and always count. -------
        var staffQuery = _context.StaffProfiles
            .AsNoTracking()
            .Where(p => p.DistrictId == ctx.DistrictId && (p.SchoolId == null || p.School!.IsActive));
        if (!isDistrictAdmin)
            staffQuery = staffQuery.Where(p => p.SchoolId == ctx.SchoolId && p.OrgRoleId != OrgRoleIds.DistrictAdmin);

        var activeStaffCount = await staffQuery.CountAsync(p => p.IsActive, ct);
        var deactivatedStaffCount = await staffQuery.CountAsync(p => !p.IsActive, ct);

        // ------- Invites needing attention: pending + expired ROWS; revoked/accepted excluded.
        // Invites into a deactivated school are excluded (nothing actionable there — the Phase 3
        // expiry worker skips them for the same reason); SchoolAdmin never sees district-admin
        // invites (SchoolId == null fails the school match). -------
        var invitesQuery = _context.StaffInvites
            .AsNoTracking()
            .Where(i => i.DistrictId == ctx.DistrictId && i.IsActive && i.AcceptedAt == null
                     && (i.SchoolId == null || i.School!.IsActive));
        if (!isDistrictAdmin)
            invitesQuery = invitesQuery.Where(i => i.SchoolId == ctx.SchoolId && i.OrgRoleId != OrgRoleIds.DistrictAdmin);

        var invites = await invitesQuery
            .OrderBy(i => i.InviteExpiresAt) // triage order: expired first, then closest to expiry
            .Select(i => new DashboardInviteModel
            {
                Id = i.Id,
                Email = i.Email,
                OrgRoleId = i.OrgRoleId,
                OrgRoleName = i.OrgRole.Name,
                SchoolId = i.SchoolId,
                SchoolName = i.School != null ? i.School.Name : null,
                InviteExpiresAt = i.InviteExpiresAt,
                Status = i.InviteExpiresAt > now ? "pending" : "expired"
            })
            .ToListAsync(ct);

        // ------- Attention lists: active students in active schools only -------
        var studentsQuery = _context.SchoolStudents
            .AsNoTracking()
            .Where(st => st.IsActive && st.School.IsActive && st.School.DistrictId == ctx.DistrictId);
        if (!isDistrictAdmin)
            studentsQuery = studentsQuery.Where(st => st.SchoolId == ctx.SchoolId!.Value);

        // "No assigned staff" = zero access rows that are active AND whose grantee still holds an
        // ACTIVE StaffProfile in THIS district — a student whose only grantee was deactivated MUST
        // appear (district scope keeps a grantee's active profile elsewhere from masking that).
        var studentsWithoutStaff = await studentsQuery
            .Where(st => !_context.SchoolStudentAccesses.Any(a =>
                a.SchoolStudentId == st.Id
                && a.IsActive
                && _context.StaffProfiles.Any(p => p.UserId == a.UserId && p.IsActive && p.DistrictId == ctx.DistrictId)))
            .OrderBy(st => st.School.Name).ThenBy(st => st.LastName).ThenBy(st => st.FirstName)
            .Select(st => new DashboardStudentModel
            {
                SchoolStudentId = st.Id,
                FirstName = st.FirstName,
                LastName = st.LastName,
                SchoolName = st.School.Name
            })
            .ToListAsync(ct);

        // "No linked parent" = no ChildLink with IsActive && AcceptedAt != null && ChildProfileId != null.
        // Rows distinguish "invite pending" (an active un-accepted ChildLink exists) from "never invited".
        var studentsWithoutParent = await studentsQuery
            .Where(st => !_context.ChildLinks.Any(l =>
                l.SchoolStudentId == st.Id && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null))
            .OrderBy(st => st.School.Name).ThenBy(st => st.LastName).ThenBy(st => st.FirstName)
            .Select(st => new DashboardNoParentStudentModel
            {
                SchoolStudentId = st.Id,
                FirstName = st.FirstName,
                LastName = st.LastName,
                SchoolName = st.School.Name,
                ParentInvitePending = _context.ChildLinks.Any(l =>
                    l.SchoolStudentId == st.Id && l.IsActive && l.AcceptedAt == null)
            })
            .ToListAsync(ct);

        return ServiceResult<DistrictDashboardModel>.SuccessResult(new DistrictDashboardModel
        {
            Schools = schools,
            StaffSummary = new DashboardStaffSummaryModel
            {
                ActiveCount = activeStaffCount,
                DeactivatedCount = deactivatedStaffCount,
                // Pending invite ROWS only — expired invites stay in the list, flagged, but don't count.
                InvitedCount = invites.Count(i => i.Status == "pending")
            },
            InvitesNeedingAttention = invites,
            StudentsWithoutStaff = studentsWithoutStaff,
            StudentsWithoutParent = studentsWithoutParent
        });
    }

    public async Task<ServiceResult<List<DistrictSchoolModel>>> GetSchoolsAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<List<DistrictSchoolModel>>.FailureResult("Educator profile not found.");

        // Any active staff in the district may read the school directory (school pickers need it);
        // SchoolAdmin/Teacher get the full district list as read-only directory info.
        var schools = await _context.Schools
            .AsNoTracking()
            .Where(s => s.DistrictId == ctx.DistrictId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new DistrictSchoolModel
            {
                Id = s.Id,
                Name = s.Name,
                StateCode = s.StateCode,
                ActiveStudentCount = _context.SchoolStudents.Count(st => st.SchoolId == s.Id && st.IsActive),
                ActiveStaffCount = _context.StaffProfiles.Count(p => p.SchoolId == s.Id && p.IsActive)
            })
            .ToListAsync(ct);

        return ServiceResult<List<DistrictSchoolModel>>.SuccessResult(schools);
    }

    public async Task<ServiceResult<DistrictSchoolModel>> CreateSchoolAsync(int userId, CreateSchoolModel model, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<DistrictSchoolModel>.FailureResult("Educator profile not found.");
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            return ServiceResult<DistrictSchoolModel>.FailureResult("You do not have permission to create schools.");

        if (string.IsNullOrWhiteSpace(model.Name))
            return ServiceResult<DistrictSchoolModel>.FailureResult("School name is required.");
        var name = model.Name.Trim();
        if (name.Length > 200)
            return ServiceResult<DistrictSchoolModel>.FailureResult("School name must be 200 characters or fewer.");

        var stateCode = NormalizeStateCode(model.StateCode);
        if (stateCode != null && stateCode.Length != 2)
            return ServiceResult<DistrictSchoolModel>.FailureResult("State code must be 2 characters.");

        // Default the state code from the district when the caller omits one.
        if (stateCode == null)
        {
            stateCode = await _context.Districts
                .AsNoTracking()
                .Where(d => d.Id == ctx.DistrictId)
                .Select(d => d.StateCode)
                .FirstOrDefaultAsync(ct);
        }

        var school = new School
        {
            DistrictId = ctx.DistrictId,
            Name = name,
            StateCode = stateCode,
            IsActive = true,
            CreatedById = userId
        };
        await _context.Schools.AddAsync(school, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<DistrictSchoolModel>.SuccessResult(new DistrictSchoolModel
        {
            Id = school.Id,
            Name = school.Name,
            StateCode = school.StateCode,
            ActiveStudentCount = 0,
            ActiveStaffCount = 0
        });
    }

    public async Task<ServiceResult<DistrictSchoolModel>> UpdateSchoolAsync(int userId, int schoolId, UpdateSchoolModel model, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<DistrictSchoolModel>.FailureResult("Educator profile not found.");
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            return ServiceResult<DistrictSchoolModel>.FailureResult("You do not have permission to edit schools.");

        if (string.IsNullOrWhiteSpace(model.Name))
            return ServiceResult<DistrictSchoolModel>.FailureResult("School name is required.");
        var name = model.Name.Trim();
        if (name.Length > 200)
            return ServiceResult<DistrictSchoolModel>.FailureResult("School name must be 200 characters or fewer.");

        var stateCode = NormalizeStateCode(model.StateCode);
        if (stateCode != null && stateCode.Length != 2)
            return ServiceResult<DistrictSchoolModel>.FailureResult("State code must be 2 characters.");

        // Confine to the caller's own district; don't leak existence of schools elsewhere.
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == schoolId && s.DistrictId == ctx.DistrictId, ct);
        if (school == null)
            return ServiceResult<DistrictSchoolModel>.FailureResult("School not found.");

        school.Name = name;
        school.StateCode = stateCode;
        await _context.SaveChangesAsync(ct);

        var activeStudentCount = await _context.SchoolStudents
            .AsNoTracking()
            .CountAsync(st => st.SchoolId == school.Id && st.IsActive, ct);
        var activeStaffCount = await _context.StaffProfiles
            .AsNoTracking()
            .CountAsync(p => p.SchoolId == school.Id && p.IsActive, ct);

        return ServiceResult<DistrictSchoolModel>.SuccessResult(new DistrictSchoolModel
        {
            Id = school.Id,
            Name = school.Name,
            StateCode = school.StateCode,
            ActiveStudentCount = activeStudentCount,
            ActiveStaffCount = activeStaffCount
        });
    }

    public async Task<ServiceResult> DeactivateSchoolAsync(int userId, int schoolId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult.FailureResult("Educator profile not found.");
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            return ServiceResult.FailureResult("You do not have permission to deactivate schools.");

        // Confine to the caller's own district; a cross-district id reads as not-found (no existence leak).
        var school = await _context.Schools
            .FirstOrDefaultAsync(s => s.Id == schoolId && s.DistrictId == ctx.DistrictId, ct);
        if (school == null)
            return ServiceResult.FailureResult("School not found.");

        if (!school.IsActive)
            return ServiceResult.SuccessResult("School is already deactivated.");

        var activeStudentCount = await _context.SchoolStudents
            .AsNoTracking()
            .CountAsync(st => st.SchoolId == schoolId && st.IsActive, ct);
        if (activeStudentCount > 0)
            return ServiceResult.FailureResult(
                $"This school cannot be deactivated while it has {activeStudentCount} active student(s). Move or remove them first.");

        var activeStaffCount = await _context.StaffProfiles
            .AsNoTracking()
            .CountAsync(p => p.SchoolId == schoolId && p.IsActive, ct);
        if (activeStaffCount > 0)
            return ServiceResult.FailureResult(
                $"This school cannot be deactivated while it has {activeStaffCount} active staff member(s). Reassign or deactivate them first.");

        school.IsActive = false;
        await _context.SaveChangesAsync(ct);

        return ServiceResult.SuccessResult("School deactivated.");
    }

    private static string? NormalizeStateCode(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToUpperInvariant();
}
