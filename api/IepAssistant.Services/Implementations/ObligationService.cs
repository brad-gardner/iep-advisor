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

        return ServiceResult<List<ObligationModel>>.SuccessResult(await ComputeAndFilterAsync(new List<StudentObligationContext> { context }, null, ct));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetMineAsync(int userId, ObligationStatus? status, CancellationToken ct = default)
    {
        var staffCtx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (staffCtx == null)
            return ServiceResult<List<ObligationModel>>.FailureResult("Educator profile not found.");

        var students = OrgRoleIds.IsAdmin(staffCtx.OrgRoleId)
            ? await LoadScopedStudentsAsync(staffCtx, null, ct)
            : await LoadLeadStudentsAsync(userId, ct);

        return ServiceResult<List<ObligationModel>>.SuccessResult(await ComputeAndFilterAsync(students, status, ct));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetLeadOnlyAsync(int userId, CancellationToken ct = default)
    {
        var students = await LoadLeadStudentsAsync(userId, ct);
        return ServiceResult<List<ObligationModel>>.SuccessResult(await ComputeAndFilterAsync(students, null, ct));
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetForLeadUsersAsync(IEnumerable<int> userIds, CancellationToken ct = default)
    {
        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return ServiceResult<List<ObligationModel>>.SuccessResult(new List<ObligationModel>());

        var students = await ProjectContext(_context.SchoolStudents.AsNoTracking()
                .Where(s => s.CaseManagerUserId != null && idList.Contains(s.CaseManagerUserId.Value) && s.Status == StudentStatus.Active))
            .ToListAsync(ct);

        return ServiceResult<List<ObligationModel>>.SuccessResult(await ComputeAndFilterAsync(students, null, ct));
    }

    public async Task<Dictionary<int, List<ObligationModel>>> GetForStaffDigestAsync(IEnumerable<int> userIds, CancellationToken ct = default)
    {
        var idList = userIds.Distinct().ToList();
        var result = new Dictionary<int, List<ObligationModel>>();
        if (idList.Count == 0)
            return result;

        // Everyone: the students they personally lead.
        var leadStudents = await ProjectContext(_context.SchoolStudents.AsNoTracking()
                .Where(s => s.CaseManagerUserId != null && idList.Contains(s.CaseManagerUserId.Value) && s.Status == StudentStatus.Active))
            .ToListAsync(ct);
        foreach (var group in (await ComputeAndFilterAsync(leadStudents, null, ct)).Where(o => o.OwnerUserId.HasValue).GroupBy(o => o.OwnerUserId!.Value))
            result[group.Key] = group.ToList();

        // Admins: their whole scope, the same superset GetMineAsync gives them one at a time.
        var admins = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.IsActive && idList.Contains(p.UserId)
                        && (p.OrgRoleId == OrgRoleIds.DistrictAdmin || (p.OrgRoleId == OrgRoleIds.SchoolAdmin && p.SchoolId != null)))
            .Select(p => new { p.UserId, p.OrgRoleId, p.DistrictId, p.SchoolId })
            .ToListAsync(ct);
        if (admins.Count == 0)
            return result;

        var districtIds = admins.Where(a => a.OrgRoleId == OrgRoleIds.DistrictAdmin).Select(a => a.DistrictId).Distinct().ToList();
        var schoolIds = admins.Where(a => a.OrgRoleId == OrgRoleIds.SchoolAdmin).Select(a => a.SchoolId!.Value).Distinct().ToList();
        var scoped = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Status == StudentStatus.Active
                        && ((districtIds.Contains(s.School.DistrictId) && s.School.IsActive) || schoolIds.Contains(s.SchoolId)))
            .Select(s => new { s.Id, s.SchoolId, s.School.DistrictId, SchoolActive = s.School.IsActive })
            .ToListAsync(ct);
        var scopedIds = scoped.Select(s => s.Id).ToList();
        var scopedContexts = await ProjectContext(_context.SchoolStudents.AsNoTracking().Where(s => scopedIds.Contains(s.Id))).ToListAsync(ct);
        var obligationsByStudent = (await ComputeAndFilterAsync(scopedContexts, null, ct)).GroupBy(o => o.SchoolStudentId).ToDictionary(g => g.Key, g => g.ToList());
        var placement = scoped.ToDictionary(s => s.Id);

        foreach (var admin in admins)
        {
            var mine = result.TryGetValue(admin.UserId, out var existing) ? existing : new List<ObligationModel>();
            var seen = mine.Select(o => (o.SchoolStudentId, o.Kind)).ToHashSet();
            foreach (var (studentId, obligations) in obligationsByStudent)
            {
                var where = placement[studentId];
                var inScope = admin.OrgRoleId == OrgRoleIds.DistrictAdmin
                    ? where.DistrictId == admin.DistrictId && where.SchoolActive
                    : where.SchoolId == admin.SchoolId;
                if (!inScope) continue;
                foreach (var o in obligations)
                    if (seen.Add((o.SchoolStudentId, o.Kind))) mine.Add(o);
            }
            result[admin.UserId] = mine;
        }
        return result;
    }

    public async Task<ServiceResult<List<ObligationModel>>> GetForScopeAsync(int userId, int? schoolId, ObligationStatus? status, CancellationToken ct = default)
    {
        var staffCtx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (staffCtx == null)
            return ServiceResult<List<ObligationModel>>.FailureResult("Educator profile not found.");
        return await GetForScopeAsync(staffCtx, schoolId, status, ct);
    }

    /// <summary>Same as <see cref="GetForScopeAsync(int, int?, ObligationStatus?, CancellationToken)"/>
    /// but skips the staff-context re-lookup for a caller (e.g. <see cref="HomeService"/>) that already
    /// resolved it — avoids a duplicated round trip on the admin home path (review-fix contract, todos/074).</summary>
    public async Task<ServiceResult<List<ObligationModel>>> GetForScopeAsync(StaffContext staffCtx, int? schoolId, ObligationStatus? status, CancellationToken ct = default)
    {
        if (!OrgRoleIds.IsAdmin(staffCtx.OrgRoleId))
            return ServiceResult<List<ObligationModel>>.FailureResult("You do not have permission to view district obligations.");
        if (schoolId.HasValue && !await _orgAccess.CanActOnSchoolAsync(staffCtx.UserId, schoolId.Value, ct))
            return ServiceResult<List<ObligationModel>>.FailureResult("You do not have permission to view this school's obligations.");

        var students = await LoadScopedStudentsAsync(staffCtx, schoolId, ct);
        return ServiceResult<List<ObligationModel>>.SuccessResult(await ComputeAndFilterAsync(students, status, ct));
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
            // Matches DistrictService.ScopedActiveSchools' School.IsActive filter — a SchoolAdmin still
            // bound to a since-deactivated school must see the same (empty) picture the compliance board
            // shows for that school, not a stale non-zero one (review-fix contract, todos/086 P3 #3).
            query = _context.SchoolStudents.Where(s => s.SchoolId == ctx.SchoolId.Value && s.School.IsActive);
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

    /// <summary>
    /// The plan 4 date-based obligations (synchronous, from already-loaded <see cref="StudentObligationContext"/>
    /// rows) PLUS the plan 7 additions that need their own queries: <see cref="ObligationKind.GoalObservationStale"/>
    /// and <see cref="ObligationKind.EvaluationDetermination"/>/<see cref="ObligationKind.EvaluatorSubmission"/>
    /// (see <see cref="LoadGoalObligationsAsync"/>/<see cref="LoadEvaluationObligationsAsync"/>). Every call
    /// site that used to call the old synchronous <c>ComputeAndFilter</c> now awaits this instead.
    /// </summary>
    private async Task<List<ObligationModel>> ComputeAndFilterAsync(List<StudentObligationContext> students, ObligationStatus? status, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var result = students.SelectMany(s => ComputeForStudent(s, today)).ToList();

        if (students.Count > 0)
        {
            var studentIds = students.Select(s => s.Id).ToList();
            var byId = students.ToDictionary(s => s.Id);
            result.AddRange(await LoadGoalObligationsAsync(studentIds, byId, today, ct));
            result.AddRange(await LoadEvaluationObligationsAsync(studentIds, byId, today, ct));
        }

        if (status.HasValue)
            result = result.Where(o => o.Status == status.Value).ToList();
        return result.OrderBy(o => o.DueDate ?? DateTime.MaxValue).ThenBy(o => o.StudentName).ToList();
    }

    /// <summary>
    /// Plan 7: an <see cref="ObligationKind.GoalObservationStale"/> row per Active <see cref="GoalRecord"/>
    /// with no observation in <see cref="GoalRecordRules.StaleAfterDays"/> days. Owner = a provider on the
    /// student's active team (any active, non-lead <see cref="StudentTeamMember"/>, deterministically the
    /// lowest-Id one), else the lead case manager (contract: "owner = a provider on the active team, else lead").
    /// </summary>
    private async Task<List<ObligationModel>> LoadGoalObligationsAsync(
        List<int> studentIds, IReadOnlyDictionary<int, StudentObligationContext> byId, DateTime today, CancellationToken ct)
    {
        var activeGoals = await _context.GoalRecords.AsNoTracking()
            .Where(g => studentIds.Contains(g.SchoolStudentId) && g.Status == GoalRecordStatus.Active)
            .Select(g => new
            {
                g.Id,
                g.SchoolStudentId,
                g.GoalText,
                g.ProjectedAt,
                LastObservedAt = g.Observations.OrderByDescending(o => o.ObservedAt).Select(o => (DateTime?)o.ObservedAt).FirstOrDefault()
            })
            .ToListAsync(ct);

        var staleGoals = activeGoals
            .Where(g => GoalRecordRules.IsStale(g.LastObservedAt ?? g.ProjectedAt, today))
            .ToList();
        if (staleGoals.Count == 0)
            return new List<ObligationModel>();

        var providerOwners = await ResolveProviderOwnersAsync(staleGoals.Select(g => g.SchoolStudentId).Distinct().ToList(), ct);

        var result = new List<ObligationModel>();
        foreach (var g in staleGoals)
        {
            if (!byId.TryGetValue(g.SchoolStudentId, out var studentCtx))
                continue;

            var (ownerUserId, ownerName) = providerOwners.TryGetValue(g.SchoolStudentId, out var provider)
                ? provider
                : (studentCtx.CaseManagerUserId, studentCtx.CaseManagerName);

            // The day the goal BECAME stale (last activity + the grace window) — used as the obligation's
            // due date for sort/status purposes, mirroring how the other kinds derive a due date.
            var dueDate = (g.LastObservedAt ?? g.ProjectedAt).Date.AddDays(GoalRecordRules.StaleAfterDays);

            result.Add(new ObligationModel
            {
                Kind = ObligationKind.GoalObservationStale,
                DueDate = dueDate,
                // Always Overdue, never DueSoon/Upcoming: staleGoals is already filtered to goals that
                // HAVE crossed the 45-day threshold, so ResolveStatus's "due today counts as DueSoon"
                // rule would misreport day 45 itself (todos-equivalent: this obligation only ever exists
                // once it is already true).
                Status = ObligationStatus.Overdue,
                SourceLabel = $"No progress observation logged for \"{g.GoalText}\" in {GoalRecordRules.StaleAfterDays}+ days",
                OwnerUserId = ownerUserId,
                OwnerName = ownerName,
                SchoolStudentId = g.SchoolStudentId,
                StudentName = studentCtx.StudentName,
                DaysUntilDue = ObligationRules.DaysUntilDue(dueDate, today),
                RuleProfile = ObligationRules.ResolveProfile(studentCtx.EffectiveStateCode)
            });
        }
        return result;
    }

    /// <summary>The first active, non-lead <see cref="StudentTeamMember"/> per student (lowest Id — deterministic), for goal-obligation ownership.</summary>
    private async Task<Dictionary<int, (int? UserId, string? Name)>> ResolveProviderOwnersAsync(List<int> studentIds, CancellationToken ct)
    {
        var rows = await _context.StudentTeamMembers.AsNoTracking()
            .Where(m => studentIds.Contains(m.SchoolStudentId) && m.IsActive && !m.IsLead)
            .OrderBy(m => m.Id)
            .Select(m => new { m.SchoolStudentId, m.UserId, Name = (m.User.FirstName + " " + m.User.LastName).Trim() })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.SchoolStudentId)
            .ToDictionary(g => g.Key, g => ((int?)g.First().UserId, (string?)g.First().Name));
    }

    /// <summary>
    /// Plan 7: <see cref="ObligationKind.EvaluationDetermination"/> for every open (not Determined/Closed)
    /// <see cref="EvaluationCase"/>, owner = lead case manager else the case creator; and
    /// <see cref="ObligationKind.EvaluatorSubmission"/> per overdue (past due, not yet submitted)
    /// <see cref="EvaluatorAssignment"/> on such a case, owner = the evaluator.
    /// </summary>
    private async Task<List<ObligationModel>> LoadEvaluationObligationsAsync(
        List<int> studentIds, IReadOnlyDictionary<int, StudentObligationContext> byId, DateTime today, CancellationToken ct)
    {
        var result = new List<ObligationModel>();

        var openCases = await _context.EvaluationCases.AsNoTracking()
            .Where(c => studentIds.Contains(c.SchoolStudentId)
                        && c.Status != EvaluationCaseStatus.Closed && c.Status != EvaluationCaseStatus.Determined)
            .Select(c => new { c.SchoolStudentId, c.DeterminationDueDate, c.DueDateOverrideReason, c.CreatedByUserId })
            .ToListAsync(ct);

        if (openCases.Count > 0)
        {
            // Case-creator display names, needed only when a student has no lead case manager on file.
            var creatorIdsNeeded = openCases
                .Where(c => !(byId.TryGetValue(c.SchoolStudentId, out var sc) && sc.CaseManagerUserId.HasValue))
                .Select(c => c.CreatedByUserId)
                .Distinct()
                .ToList();
            var creatorNames = creatorIdsNeeded.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Users.AsNoTracking()
                    .Where(u => creatorIdsNeeded.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);

            foreach (var c in openCases)
            {
                if (!byId.TryGetValue(c.SchoolStudentId, out var studentCtx))
                    continue;

                var ownerUserId = studentCtx.CaseManagerUserId ?? c.CreatedByUserId;
                var ownerName = studentCtx.CaseManagerUserId.HasValue ? studentCtx.CaseManagerName : creatorNames.GetValueOrDefault(c.CreatedByUserId);

                result.Add(new ObligationModel
                {
                    Kind = ObligationKind.EvaluationDetermination,
                    DueDate = c.DeterminationDueDate,
                    Status = ObligationRules.ResolveStatus(c.DeterminationDueDate, today),
                    SourceLabel = c.DueDateOverrideReason ?? "Consent received + 60 calendar days",
                    OwnerUserId = ownerUserId,
                    OwnerName = ownerName,
                    SchoolStudentId = c.SchoolStudentId,
                    StudentName = studentCtx.StudentName,
                    DaysUntilDue = ObligationRules.DaysUntilDue(c.DeterminationDueDate, today),
                    RuleProfile = ObligationRules.ResolveProfile(studentCtx.EffectiveStateCode)
                });
            }
        }

        // EvaluatorSubmission is generated only for assignments that are ALREADY overdue (contract:
        // "EvaluatorSubmission per overdue assignment") — unlike the other kinds, an upcoming/DueSoon
        // assignment is not surfaced as a compliance obligation.
        var overdueAssignments = await _context.EvaluatorAssignments.AsNoTracking()
            .Where(a => studentIds.Contains(a.EvaluationCase.SchoolStudentId)
                        && a.SubmittedAt == null && a.DueDate != null && a.DueDate.Value.Date < today
                        && a.EvaluationCase.Status != EvaluationCaseStatus.Closed
                        && a.EvaluationCase.Status != EvaluationCaseStatus.Determined)
            .Select(a => new
            {
                a.UserId,
                a.Domain,
                a.DueDate,
                StudentId = a.EvaluationCase.SchoolStudentId,
                DisplayName = (a.User.FirstName + " " + a.User.LastName).Trim()
            })
            .ToListAsync(ct);

        foreach (var a in overdueAssignments)
        {
            if (!byId.TryGetValue(a.StudentId, out var studentCtx))
                continue;

            result.Add(new ObligationModel
            {
                Kind = ObligationKind.EvaluatorSubmission,
                DueDate = a.DueDate,
                Status = ObligationRules.ResolveStatus(a.DueDate, today),
                SourceLabel = $"{a.Domain} evaluation not yet submitted",
                OwnerUserId = a.UserId,
                OwnerName = a.DisplayName,
                SchoolStudentId = a.StudentId,
                StudentName = studentCtx.StudentName,
                DaysUntilDue = ObligationRules.DaysUntilDue(a.DueDate, today),
                RuleProfile = ObligationRules.ResolveProfile(studentCtx.EffectiveStateCode)
            });
        }

        return result;
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
