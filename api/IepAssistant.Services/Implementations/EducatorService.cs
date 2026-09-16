using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class EducatorService : IEducatorService
{
    internal const string DuplicateExternalIdMessage = "Student ID already in use in this district.";
    private const string StudentResource = "SchoolStudent";

    /// <summary>Server-side bound on a bulk case-manager selection (mirrored by the request DTO).</summary>
    public const int MaxBulkStudents = 500;

    internal static string TransferNote(string newSchoolName) => $"Deactivated on transfer to {newSchoolName}";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuditLogger _audit;
    private readonly ILogger<EducatorService> _logger;

    public EducatorService(ApplicationDbContext context, IOrgAccessService orgAccess, IAuditLogger audit, ILogger<EducatorService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _audit = audit;
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

    // ----------------------------------------------------------------- Create / read

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
            // SchoolAdmin / Teacher-tier: own school only. An explicit mismatched school is denied.
            if (ctx.SchoolId == null)
                return ServiceResult<SchoolStudentModel>.FailureResult("A school is required to create a student.");
            if (model.SchoolId != null && model.SchoolId.Value != ctx.SchoolId.Value)
                return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to create a student in another school.");
            targetSchoolId = ctx.SchoolId.Value;
        }

        var school = await _context.Schools.AsNoTracking()
            .Where(s => s.Id == targetSchoolId)
            .Select(s => new { s.DistrictId, s.Name, StateCode = s.StateCode ?? s.District.StateCode })
            .FirstAsync(ct);

        var externalId = NormalizeExternalId(model.ExternalStudentId);
        if (externalId != null && await ExternalIdInUseAsync(school.DistrictId, externalId, excludeStudentId: null, ct))
            return ServiceResult<SchoolStudentModel>.FailureResult(DuplicateExternalIdMessage);

        var student = new SchoolStudent
        {
            SchoolId = targetSchoolId,
            DistrictId = school.DistrictId,
            FirstName = model.FirstName.Trim(),
            LastName = NormalizeOptional(model.LastName),
            DateOfBirth = model.DateOfBirth,
            // Default to the school's state so state-specific templates resolve for the student.
            StateCode = string.IsNullOrWhiteSpace(model.StateCode) ? school.StateCode : model.StateCode.Trim(),
            ExternalStudentId = externalId,
            GradeLevel = model.GradeLevel,
            DisabilityCategory = model.DisabilityCategory,
            HomeLanguage = NormalizeOptional(model.HomeLanguage) ?? "en",
            IepDate = model.IepDate?.Date,
            AnnualReviewDueDate = model.AnnualReviewDueDate?.Date,
            EtrDate = model.EtrDate?.Date,
            ReevaluationDueDate = model.ReevaluationDueDate?.Date,
            Status = StudentStatus.Active,
            IsActive = true,
            CreatedById = userId
        };
        await _context.SchoolStudents.AddAsync(student, ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsExternalIdCollision(ex))
        {
            _context.Entry(student).State = EntityState.Detached;
            return ServiceResult<SchoolStudentModel>.FailureResult(DuplicateExternalIdMessage);
        }

        await _context.SchoolStudentAccesses.AddAsync(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id,
            UserId = userId,
            Role = AccessRole.Owner,
            IsActive = true,
            CreatedById = userId
        }, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<SchoolStudentModel>.SuccessResult(await LoadStudentAsync(student.Id, ct));
    }

    public async Task<ServiceResult<List<SchoolStudentModel>>> GetStudentsAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<List<SchoolStudentModel>>.FailureResult("Educator profile not found.");

        var query = ScopedStudents(ctx, userId);
        if (query == null)
            return ServiceResult<List<SchoolStudentModel>>.SuccessResult(new List<SchoolStudentModel>());

        var students = await query
            .Where(s => s.Status == StudentStatus.Active)
            .AsNoTracking()
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Select(Projection)
            .ToListAsync(ct);

        return ServiceResult<List<SchoolStudentModel>>.SuccessResult(students);
    }

    public async Task<ServiceResult<PagedResult<SchoolStudentModel>>> SearchStudentsAsync(int userId, StudentSearchQuery search, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<PagedResult<SchoolStudentModel>>.FailureResult("Educator profile not found.");

        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, 200);

        var query = ScopedStudents(ctx, userId);
        if (query == null)
            return ServiceResult<PagedResult<SchoolStudentModel>>.SuccessResult(new PagedResult<SchoolStudentModel> { Page = page, PageSize = pageSize });

        // Filters only ever NARROW the role-scoped query — a client-supplied schoolId outside the
        // caller's scope simply yields nothing.
        if (search.Status != null)
            query = query.Where(s => s.Status == search.Status.Value);
        if (search.SchoolId != null)
            query = query.Where(s => s.SchoolId == search.SchoolId.Value);
        if (search.Grade != null)
            query = query.Where(s => s.GradeLevel == search.Grade.Value);
        // Dashboard "needs attention" deep links: the same predicates DistrictService uses for its
        // tiles, composed onto the role-scoped query so paging and the other filters still apply.
        if (search.Attention == StudentAttention.NoCaseManager)
        {
            var districtId = ctx.DistrictId;
            query = query.Where(s => !_context.StudentTeamMembers.Any(m =>
                m.SchoolStudentId == s.Id && m.IsActive && m.IsLead
                && _context.StaffProfiles.Any(p => p.UserId == m.UserId && p.IsActive && p.DistrictId == districtId)));
        }
        else if (search.Attention == StudentAttention.NoLinkedParent)
        {
            query = query.Where(s => !_context.ChildLinks.Any(l =>
                l.SchoolStudentId == s.Id && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null));
        }
        if (!string.IsNullOrWhiteSpace(search.Query))
        {
            // LIKE is case-insensitive under SQL Server's default collation and for ASCII on SQLite;
            // wildcards in the user's text are escaped so they match literally.
            var pattern = "%" + EscapeLike(search.Query.Trim()) + "%";
            query = query.Where(s =>
                EF.Functions.Like(s.FirstName, pattern, "\\")
                || (s.LastName != null && EF.Functions.Like(s.LastName, pattern, "\\"))
                || (s.ExternalStudentId != null && EF.Functions.Like(s.ExternalStudentId, pattern, "\\")));
        }

        query = query.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(Projection)
            .ToListAsync(ct);

        return ServiceResult<PagedResult<SchoolStudentModel>>.SuccessResult(new PagedResult<SchoolStudentModel>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
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
            .Select(Projection)
            .FirstOrDefaultAsync(ct);
        if (model == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student not found.");

        return ServiceResult<SchoolStudentModel>.SuccessResult(model);
    }

    // ----------------------------------------------------------------- Lifecycle

    public async Task<ServiceResult<SchoolStudentModel>> UpdateStudentAsync(int userId, int studentId, UpdateSchoolStudentModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.FirstName))
            return ServiceResult<SchoolStudentModel>.FailureResult("Student first name is required.");

        // Collaborator+ on the student (teacher-tier) or an admin in scope (player-coach superset).
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Collaborator, ct))
            return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to edit this student.");

        var student = await _context.SchoolStudents.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student not found.");

        var externalId = NormalizeExternalId(model.ExternalStudentId);
        if (externalId != null && externalId != student.ExternalStudentId
            && await ExternalIdInUseAsync(student.DistrictId, externalId, student.Id, ct))
            return ServiceResult<SchoolStudentModel>.FailureResult(DuplicateExternalIdMessage);

        student.FirstName = model.FirstName.Trim();
        student.LastName = NormalizeOptional(model.LastName);
        student.DateOfBirth = model.DateOfBirth;
        student.StateCode = NormalizeOptional(model.StateCode)?.ToUpperInvariant();
        student.ExternalStudentId = externalId;
        student.GradeLevel = model.GradeLevel;
        student.DisabilityCategory = model.DisabilityCategory;
        if (model.DisabilityCategory != null && model.DisabilityCategory != DisabilityCategory.Other)
            student.LegacyDisabilityText = null; // a real category supersedes the preserved legacy text
        student.HomeLanguage = NormalizeOptional(model.HomeLanguage);
        student.IepDate = model.IepDate?.Date;
        student.AnnualReviewDueDate = model.AnnualReviewDueDate?.Date;
        student.EtrDate = model.EtrDate?.Date;
        student.ReevaluationDueDate = model.ReevaluationDueDate?.Date;
        student.UpdatedById = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsExternalIdCollision(ex))
        {
            return ServiceResult<SchoolStudentModel>.FailureResult(DuplicateExternalIdMessage);
        }

        _audit.Record(AuditAction.Edit, userId, StudentResource, studentId);
        return ServiceResult<SchoolStudentModel>.SuccessResult(await LoadStudentAsync(studentId, ct));
    }

    public async Task<ServiceResult<SchoolStudentModel>> ExitStudentAsync(int userId, int studentId, ExitStudentModel model, CancellationToken ct = default)
    {
        var (student, denied) = await LoadForAdminMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<SchoolStudentModel>.FailureResult(denied);

        if (student!.Status == StudentStatus.Exited)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student has already been exited.");

        student.Status = StudentStatus.Exited;
        student.IsActive = false;
        student.ExitedAt = model.ExitedAt ?? DateTime.UtcNow;
        student.ExitReason = model.ExitReason;
        student.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, StudentResource, studentId);
        _logger.LogInformation("Student {StudentId} exited ({Reason}) by user {CallerId}", studentId, model.ExitReason, userId);
        return ServiceResult<SchoolStudentModel>.SuccessResult(await LoadStudentAsync(studentId, ct));
    }

    public async Task<ServiceResult<SchoolStudentModel>> ReactivateStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        var (student, denied) = await LoadForAdminMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<SchoolStudentModel>.FailureResult(denied);

        if (student!.Status != StudentStatus.Active)
        {
            student.Status = StudentStatus.Active;
            student.IsActive = true;
            student.ExitedAt = null;
            student.ExitReason = null;
            student.UpdatedById = userId;
            await _context.SaveChangesAsync(ct);

            _audit.Record(AuditAction.Edit, userId, StudentResource, studentId);
            _logger.LogInformation("Student {StudentId} reactivated by user {CallerId}", studentId, userId);
        }

        return ServiceResult<SchoolStudentModel>.SuccessResult(await LoadStudentAsync(studentId, ct));
    }

    public async Task<ServiceResult<SchoolStudentModel>> ArchiveStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        var (student, denied) = await LoadForAdminMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<SchoolStudentModel>.FailureResult(denied);

        if (student!.Status != StudentStatus.Archived)
        {
            student.Status = StudentStatus.Archived;
            student.IsActive = false;
            student.UpdatedById = userId;
            await _context.SaveChangesAsync(ct);

            _audit.Record(AuditAction.Edit, userId, StudentResource, studentId);
            _logger.LogInformation("Student {StudentId} archived by user {CallerId}", studentId, userId);
        }

        return ServiceResult<SchoolStudentModel>.SuccessResult(await LoadStudentAsync(studentId, ct));
    }

    public async Task<ServiceResult<SchoolStudentModel>> TransferStudentAsync(int userId, int studentId, int newSchoolId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Educator profile not found.");
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to transfer students.");
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<SchoolStudentModel>.FailureResult("You do not have permission to transfer this student.");

        var student = await _context.SchoolStudents
            .Include(s => s.School).ThenInclude(s => s.District)
            .FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("Student not found.");

        // Both schools must be active schools of the caller's district.
        var newSchool = await _context.Schools.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == newSchoolId && s.DistrictId == ctx.DistrictId && s.IsActive, ct);
        if (newSchool == null)
            return ServiceResult<SchoolStudentModel>.FailureResult("School not found.");
        if (newSchool.Id == student.SchoolId)
            return ServiceResult<SchoolStudentModel>.FailureResult("The student is already at that school.");

        var oldSchoolName = student.School.Name;
        var oldSchoolState = student.School.StateCode ?? student.School.District.StateCode;

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        // State inherited from the old building (null or equal to it) follows the student to the new one.
        if (student.StateCode == null || string.Equals(student.StateCode, oldSchoolState, StringComparison.OrdinalIgnoreCase))
            student.StateCode = newSchool.StateCode ?? student.School.District.StateCode;
        student.SchoolId = newSchool.Id;
        student.DistrictId = newSchool.DistrictId;
        student.UpdatedById = userId;

        // Team + access rows for staff who cannot follow the student (StudentTeamWriter.IsPortable):
        // same side effect the roster importer applies when a row moves a student.
        var portableUserIds = await StudentTeamWriter.LoadPortableUserIdsAsync(_context, newSchool.DistrictId, newSchool.Id, ct);
        var team = await StudentTeamBatch.LoadAsync(_context, new[] { student }, userId, ct);
        var (members, accesses) = team.DeactivateNonPortable(student, portableUserIds, TransferNote(newSchool.Name));
        await team.SaveAsync(ct);
        await tx.CommitAsync(ct);

        _audit.Record(AuditAction.Edit, userId, StudentResource, studentId);
        _logger.LogInformation("Student {StudentId} transferred from {OldSchool} to {NewSchool} by user {CallerId}; {Members} team member(s) and {Accesses} access row(s) deactivated",
            studentId, oldSchoolName, newSchool.Name, userId, members, accesses);

        return ServiceResult<SchoolStudentModel>.SuccessResult(await LoadStudentAsync(studentId, ct));
    }

    public async Task<ServiceResult<BulkAssignResultModel>> AssignCaseManagerBulkAsync(int userId, BulkAssignCaseManagerModel model, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return ServiceResult<BulkAssignResultModel>.FailureResult("Educator profile not found.");
        if (!OrgRoleIds.IsAdmin(ctx.OrgRoleId))
            return ServiceResult<BulkAssignResultModel>.FailureResult("You do not have permission to assign case managers.");

        var studentIds = model.StudentIds.Distinct().ToList();
        if (studentIds.Count == 0)
            return ServiceResult<BulkAssignResultModel>.FailureResult("Choose at least one student.");
        if (studentIds.Count > MaxBulkStudents)
            return ServiceResult<BulkAssignResultModel>.FailureResult($"Choose at most {MaxBulkStudents} students at a time.");

        var target = await _context.StaffProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == model.UserId && p.IsActive && p.DistrictId == ctx.DistrictId, ct);
        if (target == null)
            return ServiceResult<BulkAssignResultModel>.FailureResult("Staff member not found.");
        if (target.OrgRoleId == OrgRoleIds.DistrictAdmin)
            return ServiceResult<BulkAssignResultModel>.FailureResult("A District Admin does not need a per-student assignment.");

        // One scoped query authorizes the whole selection (ScopedStudents encodes the admin rule of
        // CanActOnStudentAsync); any id outside the caller's scope — or unknown — fails the request.
        var scoped = ScopedStudents(ctx, userId);
        var students = scoped == null
            ? new List<SchoolStudent>()
            : await scoped.Where(s => studentIds.Contains(s.Id)).ToListAsync(ct);
        if (students.Count != studentIds.Count)
            return ServiceResult<BulkAssignResultModel>.FailureResult("You do not have permission to manage one or more of the selected students.");
        foreach (var student in students)
        {
            var error = StudentTeamWriter.ValidateTeamCandidate(target, ctx.DistrictId, student.SchoolId);
            if (error != null)
                return ServiceResult<BulkAssignResultModel>.FailureResult($"{student.FirstName} {student.LastName}: {error}".Trim());
        }

        var toAssign = students.Where(s => s.CaseManagerUserId != target.UserId).ToList();
        if (toAssign.Count > 0)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            var team = await StudentTeamBatch.LoadAsync(_context, toAssign, userId, ct);
            foreach (var student in toAssign)
                team.AssignLead(student, target.UserId, TeamRole.CaseManager, accessOverride: null);
            await team.SaveAsync(ct);
            await tx.CommitAsync(ct);

            foreach (var student in toAssign)
                _audit.Record(AuditAction.Edit, userId, StudentResource, student.Id);
        }

        _logger.LogInformation("Case manager user {TargetUserId} assigned to {Count} student(s) by user {CallerId}", target.UserId, toAssign.Count, userId);
        return ServiceResult<BulkAssignResultModel>.SuccessResult(new BulkAssignResultModel { Updated = toAssign.Count });
    }

    // ----------------------------------------------------------------- Staff assignment (legacy access rows)

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
        if (!OrgRoleIds.IsAdmin(caller.OrgRoleId))
            return ServiceResult<StudentStaffAccessModel>.FailureResult("You do not have permission to assign staff to this student.");

        // The student must exist and fall within the caller's scope.
        var studentSchoolId = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == studentId && s.Status == StudentStatus.Active)
            .Select(s => (int?)s.SchoolId)
            .FirstOrDefaultAsync(ct);
        if (studentSchoolId == null)
            return ServiceResult<StudentStaffAccessModel>.FailureResult("Student not found.");
        if (!await _orgAccess.CanActOnSchoolAsync(userId, studentSchoolId.Value, ct))
            return ServiceResult<StudentStaffAccessModel>.FailureResult("You do not have permission to assign staff to this student.");

        // The target staff member must be active and bound to the student's school (a school-bound
        // teacher/school-admin) or a multi-building provider. District admins act by scope and don't need
        // (or get) per-student grants.
        var target = await _context.StaffProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.StaffProfileId && p.IsActive, ct);
        var candidateError = StudentTeamWriter.ValidateTeamCandidate(target, caller.DistrictId, studentSchoolId.Value);
        if (candidateError != null)
            return ServiceResult<StudentStaffAccessModel>.FailureResult(candidateError);

        // Upsert against the unique (SchoolStudentId, UserId) row: reactivate / update role rather than
        // inserting a duplicate (the index would reject it anyway).
        var existing = await _context.SchoolStudentAccesses
            .FirstOrDefaultAsync(a => a.SchoolStudentId == studentId && a.UserId == target!.UserId, ct);
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
                UserId = target!.UserId,
                Role = model.AccessRole,
                IsActive = true,
                CreatedById = userId,
                UpdatedById = userId
            };
            await _context.SchoolStudentAccesses.AddAsync(existing, ct);
        }
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff profile {StaffProfileId} (user {TargetUserId}) granted {Role} access to student {StudentId} by user {CallerId}",
            target!.Id, target.UserId, model.AccessRole, studentId, userId);

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

        if (!OrgRoleIds.IsAdmin(caller.OrgRoleId))
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

    // ----------------------------------------------------------------- helpers

    /// <summary>
    /// Role-branched roster scope (no status filter) so list authz == detail authz
    /// (<see cref="IOrgAccessService.CanActOnStudentAsync"/>): teacher-tier see only students they hold an
    /// active SchoolStudentAccess on (Teacher/GeneralEducator within their school, RelatedServiceProvider
    /// within the district's active schools); SchoolAdmin their whole school; DistrictAdmin every active
    /// school in their district. Null = the caller can see nothing (unbound SchoolAdmin/Teacher).
    /// </summary>
    private IQueryable<SchoolStudent>? ScopedStudents(StaffContext ctx, int userId)
    {
        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
            return _context.SchoolStudents.Where(s => s.School.IsActive && s.School.DistrictId == ctx.DistrictId);

        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            if (ctx.SchoolId == null)
                return null;
            return _context.SchoolStudents.Where(s => s.SchoolId == ctx.SchoolId.Value);
        }

        if (ctx.OrgRoleId == OrgRoleIds.RelatedServiceProvider)
        {
            return _context.SchoolStudents.Where(s =>
                s.School.IsActive && s.School.DistrictId == ctx.DistrictId
                && _context.SchoolStudentAccesses.Any(a => a.SchoolStudentId == s.Id && a.UserId == userId && a.IsActive));
        }

        // Teacher / GeneralEducator: own school + an active access row (any role).
        if (ctx.SchoolId == null)
            return null;
        return _context.SchoolStudents.Where(s =>
            s.SchoolId == ctx.SchoolId.Value
            && _context.SchoolStudentAccesses.Any(a => a.SchoolStudentId == s.Id && a.UserId == userId && a.IsActive));
    }

    /// <summary>Admin-only lifecycle gate: caller is DistrictAdmin/SchoolAdmin and the student is in their scope.</summary>
    private async Task<(SchoolStudent? Student, string? Denied)> LoadForAdminMutationAsync(int userId, int studentId, CancellationToken ct)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return (null, "Educator profile not found.");
        if (!OrgRoleIds.IsAdmin(ctx.OrgRoleId))
            return (null, "You do not have permission to change this student's status.");
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return (null, "You do not have permission to change this student's status.");

        var student = await _context.SchoolStudents.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        return student == null ? (null, "Student not found.") : (student, null);
    }

    private Task<SchoolStudentModel> LoadStudentAsync(int studentId, CancellationToken ct)
        => _context.SchoolStudents.AsNoTracking().Where(s => s.Id == studentId).Select(Projection).FirstAsync(ct);

    private async Task<bool> ExternalIdInUseAsync(int districtId, string externalId, int? excludeStudentId, CancellationToken ct)
        => await _context.SchoolStudents.AsNoTracking()
            .AnyAsync(s => s.DistrictId == districtId && s.ExternalStudentId == externalId && (excludeStudentId == null || s.Id != excludeStudentId.Value), ct);

    /// <summary>True when the failure is the per-district ExternalStudentId unique index (shared with the importer).</summary>
    internal static bool IsExternalIdCollision(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("IX_SchoolStudents_DistrictId_ExternalStudentId", StringComparison.OrdinalIgnoreCase) == true
           || ex.InnerException?.Message.Contains("SchoolStudents.DistrictId, SchoolStudents.ExternalStudentId", StringComparison.OrdinalIgnoreCase) == true;

    internal static string? NormalizeExternalId(string? raw) => NormalizeOptional(raw);

    private static string? NormalizeOptional(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    private static string EscapeLike(string text)
        => text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    /// <summary>Single query projection shared by every read so the DTO is populated identically everywhere.</summary>
    internal static readonly Expression<Func<SchoolStudent, SchoolStudentModel>> Projection = s => new SchoolStudentModel
    {
        Id = s.Id,
        SchoolId = s.SchoolId,
        SchoolName = s.School.Name,
        FirstName = s.FirstName,
        LastName = s.LastName,
        DateOfBirth = s.DateOfBirth,
        StateCode = s.StateCode,
        ExternalStudentId = s.ExternalStudentId,
        GradeLevel = s.GradeLevel,
        DisabilityCategory = s.DisabilityCategory,
        LegacyDisabilityText = s.LegacyDisabilityText,
        HomeLanguage = s.HomeLanguage,
        Status = s.Status,
        ExitedAt = s.ExitedAt,
        ExitReason = s.ExitReason,
        CaseManagerUserId = s.CaseManagerUserId,
        CaseManagerName = s.CaseManager != null ? (s.CaseManager.FirstName + " " + s.CaseManager.LastName).Trim() : null,
        IepDate = s.IepDate,
        AnnualReviewDueDate = s.AnnualReviewDueDate,
        EtrDate = s.EtrDate,
        ReevaluationDueDate = s.ReevaluationDueDate,
        IsActive = s.Status == StudentStatus.Active,
        CreatedAt = s.CreatedAt
    };

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
}
