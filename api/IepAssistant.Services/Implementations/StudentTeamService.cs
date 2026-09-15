using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// IEP team membership (see <see cref="IStudentTeamService"/>). Authorization: reads = Viewer on the
/// student; mutations = admin in scope (DistrictAdmin/SchoolAdmin via <see cref="IOrgAccessService"/>)
/// or the student's current lead case manager. Writes go through <see cref="StudentTeamWriter"/> so the
/// access-row and single-lead invariants are shared with bulk assignment and the importer.
/// </summary>
public class StudentTeamService : IStudentTeamService
{
    private const string PermissionMessage = "You do not have permission to manage this student's team.";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly ILogger<StudentTeamService> _logger;

    public StudentTeamService(ApplicationDbContext context, IOrgAccessService orgAccess, ILogger<StudentTeamService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _logger = logger;
    }

    public async Task<ServiceResult<List<StudentTeamMemberModel>>> GetTeamAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<StudentTeamMemberModel>>.FailureResult("You do not have permission to access this student.");

        var members = await ProjectMembers(_context.StudentTeamMembers.AsNoTracking()
                .Where(m => m.SchoolStudentId == studentId && m.IsActive))
            .ToListAsync(ct);

        return ServiceResult<List<StudentTeamMemberModel>>.SuccessResult(members
            .OrderByDescending(m => m.IsLead)
            .ThenBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .ToList());
    }

    public async Task<ServiceResult<List<EligibleStaffModel>>> GetEligibleStaffAsync(int userId, int studentId, CancellationToken ct = default)
    {
        var (student, caller, denied) = await AuthorizeMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<List<EligibleStaffModel>>.FailureResult(denied);

        var districtId = caller!.DistrictId;
        var schoolId = student!.SchoolId;
        var staff = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.IsActive && p.DistrictId == districtId
                     && ((p.SchoolId == schoolId && p.OrgRoleId != OrgRoleIds.DistrictAdmin)
                         || p.OrgRoleId == OrgRoleIds.RelatedServiceProvider)
                     && !_context.StudentTeamMembers.Any(m => m.SchoolStudentId == studentId && m.IsActive && m.UserId == p.UserId))
            .OrderBy(p => p.User.LastName).ThenBy(p => p.User.FirstName)
            .Select(p => new EligibleStaffModel
            {
                StaffProfileId = p.Id,
                UserId = p.UserId,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                Email = p.User.Email,
                OrgRoleId = p.OrgRoleId,
                OrgRoleName = p.OrgRole.Name,
                SchoolId = p.SchoolId,
                SchoolName = p.School != null ? p.School.Name : null
            })
            .ToListAsync(ct);

        return ServiceResult<List<EligibleStaffModel>>.SuccessResult(staff);
    }

    public async Task<ServiceResult<StudentTeamMemberModel>> AddMemberAsync(int userId, int studentId, AddTeamMemberModel model, CancellationToken ct = default)
    {
        var (student, caller, denied) = await AuthorizeMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<StudentTeamMemberModel>.FailureResult(denied);

        var target = await _context.StaffProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == model.StaffProfileId, ct);
        var candidateError = StudentTeamWriter.ValidateTeamCandidate(target, caller!.DistrictId, student!.SchoolId);
        if (candidateError != null)
            return ServiceResult<StudentTeamMemberModel>.FailureResult(candidateError);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        var member = await StudentTeamWriter.UpsertMemberAsync(_context, student, target!.UserId, model.TeamRole,
            model.IsLead == true, model.AccessRole, userId, ct);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Team member {MemberId} (user {TargetUserId}, {TeamRole}, lead={IsLead}) added to student {StudentId} by user {CallerId}",
            member.Id, member.UserId, member.TeamRole, member.IsLead, studentId, userId);

        return ServiceResult<StudentTeamMemberModel>.SuccessResult(await LoadMemberAsync(member.Id, ct));
    }

    public async Task<ServiceResult<StudentTeamMemberModel>> UpdateMemberAsync(int userId, int studentId, int memberId, UpdateTeamMemberModel model, CancellationToken ct = default)
    {
        var (student, _, denied) = await AuthorizeMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<StudentTeamMemberModel>.FailureResult(denied);

        var member = await _context.StudentTeamMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.SchoolStudentId == studentId && m.IsActive, ct);
        if (member == null)
            return ServiceResult<StudentTeamMemberModel>.FailureResult("Team member not found.");

        if (model.TeamRole != null)
            member.TeamRole = model.TeamRole.Value;
        member.UpdatedById = userId;
        if (model.AccessRole != null)
            await StudentTeamWriter.UpsertAccessAsync(_context, studentId, member.UserId, member.TeamRole, model.AccessRole, userId, ct);
        student!.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<StudentTeamMemberModel>.SuccessResult(await LoadMemberAsync(member.Id, ct));
    }

    public async Task<ServiceResult<StudentTeamMemberModel>> SetLeadAsync(int userId, int studentId, int memberId, CancellationToken ct = default)
    {
        var (student, _, denied) = await AuthorizeMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult<StudentTeamMemberModel>.FailureResult(denied);

        var member = await _context.StudentTeamMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.SchoolStudentId == studentId && m.IsActive, ct);
        if (member == null)
            return ServiceResult<StudentTeamMemberModel>.FailureResult("Team member not found.");

        if (!member.IsLead)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await StudentTeamWriter.PromoteToLeadAsync(_context, student!, member, userId, ct);
            await tx.CommitAsync(ct);
            _logger.LogInformation("Team member {MemberId} made lead for student {StudentId} by user {CallerId}", memberId, studentId, userId);
        }

        return ServiceResult<StudentTeamMemberModel>.SuccessResult(await LoadMemberAsync(member.Id, ct));
    }

    public async Task<ServiceResult> RemoveMemberAsync(int userId, int studentId, int memberId, CancellationToken ct = default)
    {
        var (student, _, denied) = await AuthorizeMutationAsync(userId, studentId, ct);
        if (denied != null)
            return ServiceResult.FailureResult(denied);

        var member = await _context.StudentTeamMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.SchoolStudentId == studentId, ct);
        if (member == null)
            return ServiceResult.FailureResult("Team member not found.");
        if (!member.IsActive)
            return ServiceResult.SuccessResult("Team member already removed.");

        if (member.IsLead)
        {
            var othersRemain = await _context.StudentTeamMembers
                .AnyAsync(m => m.SchoolStudentId == studentId && m.IsActive && m.Id != memberId, ct);
            if (othersRemain)
                return ServiceResult.FailureResult("Choose a new lead case manager first.");
        }

        await StudentTeamWriter.DeactivateMemberAsync(_context, student!, member, userId, null, ct);

        _logger.LogInformation("Team member {MemberId} removed from student {StudentId} by user {CallerId}", memberId, studentId, userId);
        return ServiceResult.SuccessResult("Team member removed.");
    }

    // ----------------------------------------------------------------- helpers

    /// <summary>Loads the tracked student and checks the caller may change its team: admin in scope, or the lead.</summary>
    private async Task<(SchoolStudent? Student, StaffContext? Caller, string? Denied)> AuthorizeMutationAsync(int userId, int studentId, CancellationToken ct)
    {
        var caller = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (caller == null)
            return (null, null, "Educator profile not found.");

        // Scope check (admins pass by scope; teacher-tier need an active access row); also rules out
        // non-existent students without leaking existence.
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return (null, caller, PermissionMessage);

        var student = await _context.SchoolStudents.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return (null, caller, "Student not found.");

        if (!OrgRoleIds.IsAdmin(caller.OrgRoleId) && student.CaseManagerUserId != userId)
            return (null, caller, PermissionMessage);

        return (student, caller, null);
    }

    private async Task<StudentTeamMemberModel> LoadMemberAsync(int memberId, CancellationToken ct)
        => await ProjectMembers(_context.StudentTeamMembers.AsNoTracking().Where(m => m.Id == memberId)).FirstAsync(ct);

    private IQueryable<StudentTeamMemberModel> ProjectMembers(IQueryable<StudentTeamMember> query)
        => query.Select(m => new StudentTeamMemberModel
        {
            Id = m.Id,
            UserId = m.UserId,
            StaffProfileId = _context.StaffProfiles.Where(p => p.UserId == m.UserId).Select(p => p.Id).FirstOrDefault(),
            FirstName = m.User.FirstName,
            LastName = m.User.LastName,
            Email = m.User.Email,
            OrgRoleName = _context.StaffProfiles.Where(p => p.UserId == m.UserId).Select(p => p.OrgRole.Name).FirstOrDefault() ?? string.Empty,
            TeamRole = m.TeamRole,
            IsLead = m.IsLead,
            AccessRole = _context.SchoolStudentAccesses
                .Where(a => a.SchoolStudentId == m.SchoolStudentId && a.UserId == m.UserId && a.IsActive)
                .Select(a => (AccessRole?)a.Role)
                .FirstOrDefault() ?? AccessRole.Viewer,
            IsActive = m.IsActive,
            AddedAt = m.CreatedAt
        });
}
