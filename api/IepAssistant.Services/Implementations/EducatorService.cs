using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class EducatorService : IEducatorService
{
    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly ILogger<EducatorService> _logger;

    public EducatorService(ApplicationDbContext context, IOrgAccessService orgAccess, ILogger<EducatorService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _logger = logger;
    }

    public async Task<ServiceResult<EducatorProfileModel>> GetMeAsync(int userId, CancellationToken ct = default)
    {
        var profile = await _context.StaffProfiles
            .AsNoTracking()
            .Include(t => t.District)
            .Include(t => t.School)
            .Include(t => t.OrgRole)
            .FirstOrDefaultAsync(t => t.UserId == userId, ct);

        if (profile == null)
            return ServiceResult<EducatorProfileModel>.FailureResult("Educator profile not found.");

        return ServiceResult<EducatorProfileModel>.SuccessResult(BuildProfileModel(profile));
    }

    public async Task<ServiceResult<SchoolStudentModel>> CreateStudentAsync(int userId, CreateSchoolStudentModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            return ServiceResult<SchoolStudentModel>.FailureResult("Student first name is required.");

        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Educator profile not found.");

        // Resolve the target school per org role (never SchoolId=0/NRE).
        int targetSchoolId;
        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            // DistrictAdmin has no implicit school: an explicit, active, in-district school is required.
            if (model.SchoolId == null)
                return ServiceResult<SchoolStudentModel>.FailureResult("A school is required. Please choose a school for this student.");

            var schoolOk = await _context.Schools.AsNoTracking()
                .AnyAsync(s => s.Id == model.SchoolId.Value && s.DistrictId == ctx.DistrictId && s.IsActive, ct);
            if (!schoolOk)
                return ServiceResult<SchoolStudentModel>.FailureResult("School not found.");
            targetSchoolId = model.SchoolId.Value;
        }
        else
        {
            // SchoolAdmin / Teacher: own school only. An explicit mismatched school is denied.
            if (ctx.SchoolId == null)
                return ServiceResult<SchoolStudentModel>.FailureResult("A school is required to create a student.");
            if (model.SchoolId != null && model.SchoolId.Value != ctx.SchoolId.Value)
                return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to create a student in another school.");
            targetSchoolId = ctx.SchoolId.Value;
        }

        var student = new SchoolStudent
        {
            SchoolId = targetSchoolId,
            FirstName = model.FirstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(model.LastName) ? null : model.LastName.Trim(),
            DateOfBirth = model.DateOfBirth,
            StateCode = string.IsNullOrWhiteSpace(model.StateCode) ? null : model.StateCode.Trim(),
            GradeLevel = string.IsNullOrWhiteSpace(model.GradeLevel) ? null : model.GradeLevel.Trim(),
            DisabilityCategory = string.IsNullOrWhiteSpace(model.DisabilityCategory) ? null : model.DisabilityCategory.Trim(),
            IsActive = true,
            CreatedById = userId
        };
        await _context.SchoolStudents.AddAsync(student, ct);
        await _context.SaveChangesAsync(ct);

        await _context.SchoolStudentAccesses.AddAsync(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id,
            UserId = userId,
            Role = AccessRole.Owner,
            IsActive = true,
            CreatedById = userId
        }, ct);
        await _context.SaveChangesAsync(ct);

        var schoolName = await _context.Schools.AsNoTracking()
            .Where(s => s.Id == targetSchoolId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        return ServiceResult<SchoolStudentModel>.SuccessResult(MapStudent(student, schoolName));
    }

    public async Task<ServiceResult<List<SchoolStudentModel>>> GetStudentsAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<List<SchoolStudentModel>>.FailureResult("Educator profile not found.");

        // Base query: active students projected with their school name (so DistrictAdmin can group/filter).
        // Role-branched so that the list authorization matches GetStudentAsync exactly (no
        // "visible-but-not-openable"): teachers see only students they hold an active SchoolStudentAccess
        // on; SchoolAdmin sees their whole school; DistrictAdmin sees all active students across the active
        // schools in their district.
        IQueryable<SchoolStudent> query;
        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            query = _context.SchoolStudents
                .Where(s => s.IsActive
                         && s.School.IsActive
                         && s.School.DistrictId == ctx.DistrictId);
        }
        else if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            if (ctx.SchoolId == null)
                return ServiceResult<List<SchoolStudentModel>>.SuccessResult(new List<SchoolStudentModel>());
            query = _context.SchoolStudents
                .Where(s => s.IsActive && s.SchoolId == ctx.SchoolId.Value);
        }
        else
        {
            // Teacher: only students with an active SchoolStudentAccess granted to this user (any role).
            // This is the same gate CanActOnStudentAsync applies in detail, so list == detail.
            query = _context.SchoolStudents
                .Where(s => s.IsActive
                         && _context.SchoolStudentAccesses.Any(a =>
                                a.SchoolStudentId == s.Id && a.UserId == userId && a.IsActive));
        }

        var students = await query
            .AsNoTracking()
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(s => new SchoolStudentModel
            {
                Id = s.Id,
                SchoolId = s.SchoolId,
                SchoolName = s.School.Name,
                FirstName = s.FirstName,
                LastName = s.LastName,
                DateOfBirth = s.DateOfBirth,
                StateCode = s.StateCode,
                GradeLevel = s.GradeLevel,
                DisabilityCategory = s.DisabilityCategory,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<SchoolStudentModel>>.SuccessResult(students);
    }

    public async Task<ServiceResult<SchoolStudentModel>> GetStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        // Org access (player-coach: admins pass within scope; teachers need an active SchoolStudentAccess).
        // Identical gate to GetStudentsAsync ⇒ list authz == detail authz.
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to access this student.");

        var model = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new SchoolStudentModel
            {
                Id = s.Id,
                SchoolId = s.SchoolId,
                SchoolName = s.School.Name,
                FirstName = s.FirstName,
                LastName = s.LastName,
                DateOfBirth = s.DateOfBirth,
                StateCode = s.StateCode,
                GradeLevel = s.GradeLevel,
                DisabilityCategory = s.DisabilityCategory,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            })
            .FirstOrDefaultAsync(ct);
        if (model == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student not found.");

        return ServiceResult<SchoolStudentModel>.SuccessResult(model);
    }

    // ----------------------------------------------------------------- Staff assignment

    public async Task<ServiceResult<List<StudentStaffAccessModel>>> GetStudentStaffAccessAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<StudentStaffAccessModel>>.FailureResult("You do not have permission to access this student.");

        // Active grants joined to the grantee's StaffProfile (name/email/org role). A grant whose user has
        // no StaffProfile (shouldn't happen for staff grants) is excluded by the inner join.
        var grants = await (
            from a in _context.SchoolStudentAccesses.AsNoTracking()
            where a.SchoolStudentId == studentId && a.IsActive
            join p in _context.StaffProfiles.AsNoTracking() on a.UserId equals p.UserId
            orderby a.User.LastName, a.User.FirstName
            select new StudentStaffAccessModel
            {
                AccessId = a.Id,
                StaffProfileId = p.Id,
                UserId = a.UserId,
                FirstName = a.User.FirstName,
                LastName = a.User.LastName,
                Email = a.User.Email,
                OrgRoleName = p.OrgRole.Name,
                AccessRole = a.Role,
                GrantedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<StudentStaffAccessModel>>.SuccessResult(grants);
    }

    public async Task<ServiceResult<StudentStaffAccessModel>> GrantStudentStaffAccessAsync(int userId, int studentId, GrantStudentStaffAccessModel model, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (caller == null)
            return ServiceResult<StudentStaffAccessModel>.FailureResult("Educator profile not found.");

        // ADMIN-only: teachers cannot assign staff.
        if (caller.OrgRoleId is not (OrgRoleIds.DistrictAdmin or OrgRoleIds.SchoolAdmin))
            return ServiceResult<StudentStaffAccessModel>.FailureResult("You do not have permission to assign staff to this student.");

        // The student must exist and fall within the caller's scope.
        var studentSchoolId = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == studentId && s.IsActive)
            .Select(s => (int?)s.SchoolId)
            .FirstOrDefaultAsync(ct);
        if (studentSchoolId == null)
            return ServiceResult<StudentStaffAccessModel>.FailureResult("Student not found.");
        if (!await _orgAccess.CanActOnSchoolAsync(userId, studentSchoolId.Value, ct))
            return ServiceResult<StudentStaffAccessModel>.FailureResult("You do not have permission to assign staff to this student.");

        // The target staff member must be active and bound to the student's school (a school-bound
        // teacher/school-admin). District admins act by scope and don't need (or get) per-student grants.
        var target = await _context.StaffProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.StaffProfileId && p.IsActive, ct);
        if (target == null)
            return ServiceResult<StudentStaffAccessModel>.FailureResult("Staff member not found.");
        if (target.OrgRoleId == OrgRoleIds.DistrictAdmin || target.SchoolId == null)
            return ServiceResult<StudentStaffAccessModel>.FailureResult("A District Admin does not need a per-student assignment.");
        if (target.SchoolId.Value != studentSchoolId.Value)
            return ServiceResult<StudentStaffAccessModel>.FailureResult("That staff member is not at this student's school.");

        // Upsert against the unique (SchoolStudentId, UserId) row: reactivate / update role rather than
        // inserting a duplicate (the index would reject it anyway).
        var existing = await _context.SchoolStudentAccesses
            .FirstOrDefaultAsync(a => a.SchoolStudentId == studentId && a.UserId == target.UserId, ct);
        if (existing != null)
        {
            existing.IsActive = true;
            existing.Role = model.AccessRole;
            existing.UpdatedById = userId;
        }
        else
        {
            existing = new SchoolStudentAccess
            {
                SchoolStudentId = studentId,
                UserId = target.UserId,
                Role = model.AccessRole,
                IsActive = true,
                CreatedById = userId,
                UpdatedById = userId
            };
            await _context.SchoolStudentAccesses.AddAsync(existing, ct);
        }
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff profile {StaffProfileId} (user {TargetUserId}) granted {Role} access to student {StudentId} by user {CallerId}",
            target.Id, target.UserId, model.AccessRole, studentId, userId);

        var result = await _context.SchoolStudentAccesses.AsNoTracking()
            .Where(a => a.Id == existing.Id)
            .Select(a => new StudentStaffAccessModel
            {
                AccessId = a.Id,
                StaffProfileId = target.Id,
                UserId = a.UserId,
                FirstName = a.User.FirstName,
                LastName = a.User.LastName,
                Email = a.User.Email,
                AccessRole = a.Role,
                GrantedAt = a.CreatedAt
            })
            .FirstAsync(ct);

        result.OrgRoleName = await _context.OrgRoles.AsNoTracking()
            .Where(r => r.Id == target.OrgRoleId).Select(r => r.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

        return ServiceResult<StudentStaffAccessModel>.SuccessResult(result);
    }

    public async Task<ServiceResult> RevokeStudentStaffAccessAsync(int userId, int studentId, int accessId, CancellationToken ct = default)
    {
        var caller = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (caller == null)
            return ServiceResult.FailureResult("Educator profile not found.");

        if (caller.OrgRoleId is not (OrgRoleIds.DistrictAdmin or OrgRoleIds.SchoolAdmin))
            return ServiceResult.FailureResult("You do not have permission to manage staff for this student.");

        var studentSchoolId = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => (int?)s.SchoolId)
            .FirstOrDefaultAsync(ct);
        if (studentSchoolId == null)
            return ServiceResult.FailureResult("Student not found.");
        if (!await _orgAccess.CanActOnSchoolAsync(userId, studentSchoolId.Value, ct))
            return ServiceResult.FailureResult("You do not have permission to manage staff for this student.");

        var grant = await _context.SchoolStudentAccesses
            .FirstOrDefaultAsync(a => a.Id == accessId && a.SchoolStudentId == studentId, ct);
        if (grant == null)
            return ServiceResult.FailureResult("Access grant not found.");

        if (!grant.IsActive)
            return ServiceResult.SuccessResult("Access is already revoked.");

        grant.IsActive = false;
        grant.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff↔student access {AccessId} (student {StudentId}) revoked by user {CallerId}",
            accessId, studentId, userId);
        return ServiceResult.SuccessResult("Access revoked.");
    }

    /// <summary>Builds the profile model from a fully navigation-loaded StaffProfile (GetMe path).</summary>
    private static EducatorProfileModel BuildProfileModel(StaffProfile profile) => new()
    {
        StaffProfileId = profile.Id,
        UserId = profile.UserId,
        OrgRoleId = profile.OrgRoleId,
        OrgRoleName = profile.OrgRole?.Name ?? string.Empty,
        DistrictId = profile.DistrictId,
        DistrictName = profile.District?.Name ?? string.Empty,
        SchoolId = profile.SchoolId,
        SchoolName = profile.School?.Name,
        IsActive = profile.IsActive,
        StateCode = profile.School?.StateCode ?? profile.District?.StateCode,
        Title = profile.Title,
        Credentials = profile.Credentials
    };

    private static SchoolStudentModel MapStudent(SchoolStudent s, string? schoolName = null) => new()
    {
        Id = s.Id,
        SchoolId = s.SchoolId,
        SchoolName = schoolName,
        FirstName = s.FirstName,
        LastName = s.LastName,
        DateOfBirth = s.DateOfBirth,
        StateCode = s.StateCode,
        GradeLevel = s.GradeLevel,
        DisabilityCategory = s.DisabilityCategory,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt
    };
}
