using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Computed procedural deadlines (see <see cref="IObligationService"/>, plan 4 decision 2).</summary>
public class ObligationService : IObligationService
{
    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;

    public ObligationService(ApplicationDbContext context, IOrgAccessService orgAccess)
    {
        _context = context;
        _orgAccess = orgAccess;
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetForStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<ObligationModel>>.FailureResult("You do not have permission to view this student's obligations.");

        var context = await LoadContextAsync(studentId, ct);
        if (context == null)
            return ServiceResult<List<ObligationModel>>.FailureResult("Student not found.");

        return ServiceResult<List<ObligationModel>>.SuccessResult(ComputeForStudent(context, DateTime.UtcNow.Date));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetMineAsync(int userId, ObligationStatus? status, CancellationToken ct = default)
    {
        var staffCtx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (staffCtx == null)
            return ServiceResult<List<ObligationModel>>.FailureResult("Educator profile not found.");

        var students = OrgRoleIds.IsAdmin(staffCtx.OrgRoleId)
            ? await LoadScopedStudentsAsync(staffCtx, null, ct)
            : await LoadLeadStudentsAsync(userId, ct);

        return ServiceResult<List<ObligationModel>>.SuccessResult(ComputeAndFilter(students, status));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetLeadOnlyAsync(int userId, CancellationToken ct = default)
    {
        var students = await LoadLeadStudentsAsync(userId, ct);
        return ServiceResult<List<ObligationModel>>.SuccessResult(ComputeAndFilter(students, null));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetForLeadUsersAsync(IEnumerable<int> userIds, CancellationToken ct = default)
    {
        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return ServiceResult<List<ObligationModel>>.SuccessResult(new List<ObligationModel>());

        var students = await ProjectContext(_context.SchoolStudents.AsNoTracking()
                .Where(s => s.CaseManagerUserId != null && idList.Contains(s.CaseManagerUserId.Value) && s.Status == StudentStatus.Active))
            .ToListAsync(ct);

        return ServiceResult<List<ObligationModel>>.SuccessResult(ComputeAndFilter(students, null));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetForScopeAsync(int userId, int? schoolId, ObligationStatus? status, CancellationToken ct = default)
    {
        var staffCtx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (staffCtx == null)
            return ServiceResult<List<ObligationModel>>.FailureResult("Educator profile not found.");
        if (!OrgRoleIds.IsAdmin(staffCtx.OrgRoleId))
            return ServiceResult<List<ObligationModel>>.FailureResult("You do not have permission to view district obligations.");
        if (schoolId.HasValue && !await _orgAccess.CanActOnSchoolAsync(userId, schoolId.Value, ct))
            return ServiceResult<List<ObligationModel>>.FailureResult("You do not have permission to view this school's obligations.");

        var students = await LoadScopedStudentsAsync(staffCtx, schoolId, ct);
        return ServiceResult<List<ObligationModel>>.SuccessResult(ComputeAndFilter(students, status));
    }

    // ----------------------------------------------------------------- Loading

    private sealed class StudentObligationContext
    {
        public int Id { get; init; }
        public string StudentName { get; init; } = string.Empty;
        public DateTime? IepDate { get; init; }
        public DateTime? AnnualReviewDueDate { get; init; }
        public DateTime? EtrDate { get; init; }
        public DateTime? ReevaluationDueDate { get; init; }
        public string? EffectiveStateCode { get; init; }
        public int? CaseManagerUserId { get; init; }
        public string? CaseManagerName { get; init; }
    }

    private async Task<StudentObligationContext?> LoadContextAsync(int studentId, CancellationToken ct)
        => await ProjectContext(_context.SchoolStudents.AsNoTracking().Where(s => s.Id == studentId)).FirstOrDefaultAsync(ct);

    private async Task<List<StudentObligationContext>> LoadLeadStudentsAsync(int userId, CancellationToken ct)
        => await ProjectContext(_context.SchoolStudents.AsNoTracking()
                .Where(s => s.CaseManagerUserId == userId && s.Status == StudentStatus.Active))
            .ToListAsync(ct);

    private async Task<List<StudentObligationContext>> LoadScopedStudentsAsync(StaffContext ctx, int? schoolId, CancellationToken ct)
    {
        IQueryable<SchoolStudent> query;
        if (ctx.OrgRoleId == OrgRoleIds.DistrictAdmin)
        {
            query = _context.SchoolStudents.Where(s => s.School.IsActive && s.School.DistrictId == ctx.DistrictId);
        }
        else if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            if (ctx.SchoolId == null)
                return new List<StudentObligationContext>();
            query = _context.SchoolStudents.Where(s => s.SchoolId == ctx.SchoolId.Value);
        }
        else
        {
            return new List<StudentObligationContext>();
        }

        query = query.Where(s => s.Status == StudentStatus.Active);
        if (schoolId.HasValue)
            query = query.Where(s => s.SchoolId == schoolId.Value);

        return await ProjectContext(query.AsNoTracking()).ToListAsync(ct);
    }

    private IQueryable<StudentObligationContext> ProjectContext(IQueryable<SchoolStudent> query) => query.Select(s => new StudentObligationContext
    {
        Id = s.Id,
        StudentName = (s.FirstName + " " + s.LastName).Trim(),
        IepDate = s.IepDate,
        AnnualReviewDueDate = s.AnnualReviewDueDate,
        EtrDate = s.EtrDate,
        ReevaluationDueDate = s.ReevaluationDueDate,
        EffectiveStateCode = s.StateCode ?? s.School.StateCode ?? s.School.District.StateCode,
        CaseManagerUserId = s.CaseManagerUserId,
        CaseManagerName = s.CaseManager != null ? (s.CaseManager.FirstName + " " + s.CaseManager.LastName).Trim() : null
    });

    // ----------------------------------------------------------------- Computation

    private static List<ObligationModel> ComputeAndFilter(List<StudentObligationContext> students, ObligationStatus? status)
    {
        var today = DateTime.UtcNow.Date;
        var result = students.SelectMany(s => ComputeForStudent(s, today)).ToList();
        if (status.HasValue)
            result = result.Where(o => o.Status == status.Value).ToList();
        return result.OrderBy(o => o.DueDate ?? DateTime.MaxValue).ThenBy(o => o.StudentName).ToList();
    }

    private static List<ObligationModel> ComputeForStudent(StudentObligationContext s, DateTime today)
    {
        var profile = ObligationRules.ResolveProfile(s.EffectiveStateCode);
        var obligations = new List<ObligationModel>();

        var (annualDue, annualLabel) = ObligationRules.ResolveAnnualReviewDue(s.AnnualReviewDueDate, s.IepDate);
        obligations.Add(BuildModel(s, ObligationKind.AnnualReview, annualDue, annualLabel, profile, today));

        var (reevalDue, reevalLabel) = ObligationRules.ResolveReevaluationDue(s.ReevaluationDueDate, s.EtrDate);
        obligations.Add(BuildModel(s, ObligationKind.Reevaluation, reevalDue, reevalLabel, profile, today));

        if (ObligationRules.NeedsEtrDue(s.IepDate, s.EtrDate))
            obligations.Add(BuildModel(s, ObligationKind.EtrDue, null, "No ETR date on file", profile, today));

        return obligations;
    }

    private static ObligationModel BuildModel(StudentObligationContext s, ObligationKind kind, DateTime? dueDate, string sourceLabel, string profile, DateTime today) => new()
    {
        Kind = kind,
        DueDate = dueDate,
        Status = ObligationRules.ResolveStatus(dueDate, today),
        SourceLabel = sourceLabel,
        OwnerUserId = s.CaseManagerUserId,
        OwnerName = s.CaseManagerName,
        SchoolStudentId = s.Id,
        StudentName = s.StudentName,
        DaysUntilDue = ObligationRules.DaysUntilDue(dueDate, today),
        RuleProfile = profile
    };
}
