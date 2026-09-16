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
        if (!OrgRoleIds.IsAdmin(ctx.OrgRoleId))
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
                ActiveStudentCount = _context.SchoolStudents.Count(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active)
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
            .Where(st => st.Status == StudentStatus.Active && st.School.IsActive && st.School.DistrictId == ctx.DistrictId);
        if (!isDistrictAdmin)
            studentsQuery = studentsQuery.Where(st => st.SchoolId == ctx.SchoolId!.Value);

        // "No case manager" (plan 3: the DTO keeps its StudentsWithoutStaff name) = no ACTIVE lead team
        // member whose user still holds an ACTIVE StaffProfile in THIS district — a student whose lead
        // was deactivated MUST appear (district scope keeps a grantee's active profile elsewhere from
        // masking that).
        var studentsWithoutStaff = await studentsQuery
            .Where(st => !_context.StudentTeamMembers.Any(m =>
                m.SchoolStudentId == st.Id
                && m.IsActive && m.IsLead
                && _context.StaffProfiles.Any(p => p.UserId == m.UserId && p.IsActive && p.DistrictId == ctx.DistrictId)))
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
                ActiveStudentCount = _context.SchoolStudents.Count(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active),
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
            .CountAsync(st => st.SchoolId == school.Id && st.Status == StudentStatus.Active, ct);
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
            .CountAsync(st => st.SchoolId == schoolId && st.Status == StudentStatus.Active, ct);
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

    // ----------------------------------------------------------------- Plan 5: compliance / adoption / engagement

    public async Task<ServiceResult<ComplianceBoardModel>> GetComplianceBoardAsync(int userId, int? schoolId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        if (!AdminQueryLimits.IsWithinRange(from, today) || !AdminQueryLimits.IsWithinRange(to, today))
            return ServiceResult<ComplianceBoardModel>.FailureResult("The requested date range is out of bounds.");

        var scope = await ResolveAdminScopeAsync(userId, schoolId, ct);
        if (scope.Error != null)
            return ServiceResult<ComplianceBoardModel>.FailureResult(scope.Error);
        if (scope.Empty)
            return ServiceResult<ComplianceBoardModel>.SuccessResult(EmptyComplianceBoard());

        var fromDate = (from ?? today).Date;
        var toDate = (to ?? fromDate.AddDays(60)).Date;
        if (toDate < fromDate)
            toDate = fromDate;

        var schoolsQuery = ScopedActiveSchools(scope.DistrictId, scope.SchoolId);
        var districtId = scope.DistrictId;

        // One query: each row's counts are correlated COUNT subqueries against SchoolStudents (the same
        // pattern GetDashboardAsync already uses for ActiveStudentCount), so this is a single round trip
        // regardless of how many schools are in scope. Every predicate is the SAME Expression<> the
        // roster's StudentAttention filter uses, so a board count and its drilldown always agree. Due30/
        // Due60 are always anchored on TODAY (never on the caller's from/to) — only DueInRange uses the
        // requested [fromDate, toDate] window — so a non-default date-range selection can never desync
        // the Due30/Due60 tiles from their drilldowns (review-fix contract addition 1).
        var bySchool = await schoolsQuery
            .OrderBy(s => s.Name)
            .Select(s => new ComplianceSchoolRowModel
            {
                SchoolId = s.Id,
                SchoolName = s.Name,
                ActiveStudents = _context.SchoolStudents.Count(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active),
                OverdueAnnual = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.OverdueAnnual(today)),
                OverdueReeval = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.OverdueReeval(today)),
                Due30 = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.DueWithin(today, today.AddDays(30))),
                Due60 = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.DueWithin(today, today.AddDays(60))),
                DueInRange = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.DueWithin(fromDate, toDate)),
                UnknownDates = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.UnknownDates()),
                NoLead = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.NoLead(_context, districtId))
            })
            .ToListAsync(ct);

        var summary = new ComplianceSummaryModel
        {
            ActiveStudents = bySchool.Sum(r => r.ActiveStudents),
            OverdueAnnual = bySchool.Sum(r => r.OverdueAnnual),
            OverdueReeval = bySchool.Sum(r => r.OverdueReeval),
            Due30 = bySchool.Sum(r => r.Due30),
            Due60 = bySchool.Sum(r => r.Due60),
            DueInRange = bySchool.Sum(r => r.DueInRange),
            UnknownDates = bySchool.Sum(r => r.UnknownDates),
            NoLead = bySchool.Sum(r => r.NoLead)
        };

        return ServiceResult<ComplianceBoardModel>.SuccessResult(new ComplianceBoardModel
        {
            GeneratedAt = DateTime.UtcNow,
            From = fromDate,
            To = toDate,
            Summary = summary,
            BySchool = bySchool,
            Drill = ComplianceDrillMap(fromDate, toDate)
        });
    }

    public async Task<ServiceResult<AdoptionModel>> GetAdoptionAsync(int userId, int? schoolId, int days, CancellationToken ct = default)
    {
        days = AdminQueryLimits.ClampDays(days, defaultDays: 30);

        var scope = await ResolveAdminScopeAsync(userId, schoolId, ct);
        if (scope.Error != null)
            return ServiceResult<AdoptionModel>.FailureResult(scope.Error);
        if (scope.Empty)
            return ServiceResult<AdoptionModel>.SuccessResult(new AdoptionModel { Days = days, ActiveRule = AdoptionActiveRule });

        var windowStart = DateTime.UtcNow.AddDays(-days);

        var schoolsQuery = ScopedActiveSchools(scope.DistrictId, scope.SchoolId);

        var bySchool = await schoolsQuery
            .OrderBy(s => s.Name)
            .Select(s => new AdoptionSchoolModel
            {
                SchoolId = s.Id,
                SchoolName = s.Name,
                StaffTotal = _context.StaffProfiles.Count(p => p.SchoolId == s.Id && p.IsActive),
                StaffActive = _context.StaffProfiles.Count(p => p.SchoolId == s.Id && p.IsActive
                    && _context.AccessAuditLogs.Any(a => a.ActorUserId == p.UserId && a.CreatedAt >= windowStart)),
                DraftsStarted = _context.DocumentInstances.Count(i => i.SchoolStudent.SchoolId == s.Id && i.CreatedAt >= windowStart),
                DraftsFinalized = _context.AuthoredDocumentVersions.Count(v => v.SchoolStudent.SchoolId == s.Id && v.FinalizedAt >= windowStart)
            })
            .ToListAsync(ct);

        // Staff/active totals are computed over the FULL admin scope (district-wide when unfiltered),
        // not summed from bySchool, so a DistrictAdmin-tier user (bound to no single school) still
        // counts toward the district total.
        var staffScope = _context.StaffProfiles.AsNoTracking().Where(p => p.IsActive && p.DistrictId == scope.DistrictId);
        if (scope.SchoolId.HasValue)
            staffScope = staffScope.Where(p => p.SchoolId == scope.SchoolId.Value);

        var staffTotal = await staffScope.CountAsync(ct);
        var staffActive = await staffScope.CountAsync(p => _context.AccessAuditLogs.Any(a => a.ActorUserId == p.UserId && a.CreatedAt >= windowStart), ct);

        return ServiceResult<AdoptionModel>.SuccessResult(new AdoptionModel
        {
            Days = days,
            StaffActiveLast14 = staffActive,
            StaffTotal = staffTotal,
            BySchool = bySchool,
            DraftsStarted = bySchool.Sum(r => r.DraftsStarted),
            DraftsFinalized = bySchool.Sum(r => r.DraftsFinalized),
            ActiveRule = AdoptionActiveRule
        });
    }

    public async Task<ServiceResult<EngagementModel>> GetEngagementAsync(int userId, int? schoolId, CancellationToken ct = default)
    {
        var scope = await ResolveAdminScopeAsync(userId, schoolId, ct);
        if (scope.Error != null)
            return ServiceResult<EngagementModel>.FailureResult(scope.Error);
        if (scope.Empty)
            return ServiceResult<EngagementModel>.SuccessResult(new EngagementModel());

        var schoolsQuery = ScopedActiveSchools(scope.DistrictId, scope.SchoolId);

        var bySchool = await schoolsQuery
            .OrderBy(s => s.Name)
            .Select(s => new EngagementSchoolModel
            {
                SchoolId = s.Id,
                SchoolName = s.Name,
                ActiveStudents = _context.SchoolStudents.Count(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active),
                StudentsWithFamilyLink = _context.SchoolStudents.Where(st => st.SchoolId == s.Id && st.Status == StudentStatus.Active).Count(StudentAttentionRules.HasFamily(_context))
            })
            .ToListAsync(ct);

        return ServiceResult<EngagementModel>.SuccessResult(new EngagementModel
        {
            ActiveStudents = bySchool.Sum(r => r.ActiveStudents),
            StudentsWithFamilyLink = bySchool.Sum(r => r.StudentsWithFamilyLink),
            DraftsShared = 0,
            ResponsesReceived = 0,
            BySchool = bySchool
        });
    }

    // ----------------------------------------------------------------- Plan 5 helpers

    private const string AdoptionActiveRule = "Active = at least one FERPA access-audit entry (view, edit, share, finalize, or export) for that staff member in the window.";

    private sealed record AdminScope(int DistrictId, int? SchoolId, bool Empty, string? Error);

    /// <summary>
    /// Resolves the caller's admin scope for the plan-5 board reads: DistrictAdmin gets the whole
    /// district (optionally narrowed to an in-district active school); SchoolAdmin is FORCED to their
    /// own school regardless of a caller-supplied schoolId (mirrors <see cref="GetDashboardAsync"/>); a
    /// SchoolAdmin with no school binding gets a valid empty result, not an error; every other role is denied.
    /// </summary>
    private async Task<AdminScope> ResolveAdminScopeAsync(int userId, int? schoolId, CancellationToken ct)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return new AdminScope(0, null, true, "Educator profile not found.");
        if (!OrgRoleIds.IsAdmin(ctx.OrgRoleId))
            return new AdminScope(0, null, true, "You do not have permission to view this data.");

        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            if (schoolId.HasValue && !await _orgAccess.CanActOnSchoolAsync(userId, schoolId.Value, ct))
                return new AdminScope(0, null, true, "You do not have permission to view this school's data.");
            return new AdminScope(ctx.DistrictId, schoolId, false, null);
        }

        // SchoolAdmin: forced to their own school; no binding -> valid empty payload (mirrors GetDashboardAsync).
        if (ctx.SchoolId == null)
            return new AdminScope(ctx.DistrictId, null, true, null);
        return new AdminScope(ctx.DistrictId, ctx.SchoolId, false, null);
    }

    private IQueryable<School> ScopedActiveSchools(int districtId, int? schoolId)
    {
        var query = _context.Schools.AsNoTracking().Where(s => s.DistrictId == districtId && s.IsActive);
        if (schoolId.HasValue)
            query = query.Where(s => s.Id == schoolId.Value);
        return query;
    }

    private static ComplianceBoardModel EmptyComplianceBoard()
    {
        var from = DateTime.UtcNow.Date;
        var to = from.AddDays(60);
        return new ComplianceBoardModel
        {
            GeneratedAt = DateTime.UtcNow,
            From = from,
            To = to,
            Drill = ComplianceDrillMap(from, to)
        };
    }

    /// <summary>Due30/Due60/overdue drill keys never carry date params (they're always today-anchored);
    /// only "dueInRange" carries the caller's effective [<paramref name="from"/>, <paramref name="to"/>]
    /// window, so its roster rows always equal the tile regardless of the chosen range.</summary>
    private static Dictionary<string, string> ComplianceDrillMap(DateTime from, DateTime to) => new()
    {
        ["overdueAnnual"] = "attention=OverdueAnnual",
        ["overdueReeval"] = "attention=OverdueReeval",
        ["due30"] = "attention=Due30",
        ["due60"] = "attention=Due60",
        ["dueInRange"] = $"attention=DueInRange&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",
        ["unknownDates"] = "attention=UnknownDates",
        ["noLead"] = "attention=NoCaseManager"
    };
}
