using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>DB-backed coverage for <see cref="ObligationService"/>: owner resolution, state-code fallback
/// chain, and scope-based listing (plan 4, decision 2).</summary>
public sealed class ObligationServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private ObligationService CreateService(Domain.Data.ApplicationDbContext ctx) => new(ctx, new OrgAccessService(ctx));

    [Fact]
    public async Task GetForStudentAsync_OwnerIsLeadCaseManager()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A", stateCode: "OH");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Lee", last: "Case");
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(leadUserId, studentId);

        Assert.True(result.Success);
        Assert.All(result.Data!, o => Assert.Equal(leadUserId, o.OwnerUserId));
        Assert.All(result.Data!, o => Assert.Equal("Lee Case", o.OwnerName));
    }

    [Fact]
    public async Task GetForStudentAsync_ProfileFallsBackToSchoolThenDistrictStateCode()
    {
        var districtId = _db.District(stateCode: "OH");
        var schoolId = _db.School(districtId, "School A", stateCode: null); // no school-level state code
        var studentId = _db.Student(schoolId, "Sam", "Student", stateCode: null); // no student-level state code either
        var (userId, _) = _db.Staff("viewer@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, userId, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(userId, studentId);

        Assert.True(result.Success);
        Assert.All(result.Data!, o => Assert.Equal(ObligationRules.OhProfile, o.RuleProfile));
    }

    [Fact]
    public async Task GetForStudentAsync_NoDates_BothObligationsAreUnknown()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (userId, _) = _db.Staff("viewer2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, userId, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(userId, studentId);

        Assert.True(result.Success);
        var annual = result.Data!.Single(o => o.Kind == ObligationKind.AnnualReview);
        var reeval = result.Data!.Single(o => o.Kind == ObligationKind.Reevaluation);
        Assert.Equal(ObligationStatus.Unknown, annual.Status);
        Assert.Equal(ObligationStatus.Unknown, reeval.Status);
        Assert.Null(annual.DueDate);
        Assert.Null(reeval.DueDate);
    }

    [Fact]
    public async Task GetForStudentAsync_ViewerWithoutAccess_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (strangerUserId, _) = _db.Staff("stranger@example.com", districtId, _db.School(districtId, "School B"), Models.OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(strangerUserId, studentId);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetMineAsync_NonAdmin_OnlyReturnsObligationsWhereCallerIsLead()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var leadStudentId = _db.Student(schoolId, "Led", "Student");
        var otherStudentId = _db.Student(schoolId, "Other", "Student");
        var (leadUserId, _) = _db.Staff("lead2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var (otherLeadUserId, _) = _db.Staff("otherlead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(leadStudentId, leadUserId, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(otherStudentId, otherLeadUserId, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetMineAsync(leadUserId, null);

        Assert.True(result.Success);
        Assert.All(result.Data!, o => Assert.Equal(leadStudentId, o.SchoolStudentId));
    }

    [Fact]
    public async Task GetMineAsync_Admin_ReturnsDistrictScope()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (adminUserId, _) = _db.Staff("admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetMineAsync(adminUserId, null);

        Assert.True(result.Success);
        Assert.Contains(result.Data!, o => o.SchoolStudentId == studentId);
    }

    [Fact]
    public async Task GetMineAsync_StatusFilter_NarrowsResults()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead3@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetMineAsync(leadUserId, ObligationStatus.Overdue);

        Assert.True(result.Success);
        Assert.All(result.Data!, o => Assert.Equal(ObligationStatus.Overdue, o.Status));
        // No dates on file -> Unknown, not Overdue -> the filtered result is empty.
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task GetForScopeAsync_NonAdmin_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (teacherUserId, _) = _db.Staff("teacher@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForScopeAsync(teacherUserId, null, null);

        Assert.False(result.Success);
    }

    /// <summary>Review-fix contract, todos/086 P3 #3: a SchoolAdmin still bound to a since-deactivated
    /// school must see the SAME empty picture the compliance board shows for that school, not a stale
    /// non-zero one — matches DistrictService.ScopedActiveSchools' School.IsActive filter.</summary>
    [Fact]
    public async Task GetForScopeAsync_SchoolAdmin_InactiveBoundSchool_ReturnsEmpty()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A", isActive: false);
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (schoolAdminId, _) = _db.Staff("inactive-school-admin@example.com", districtId, schoolId, Models.OrgRoleIds.SchoolAdmin);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForScopeAsync(schoolAdminId, null, null);

        Assert.True(result.Success, result.Message);
        Assert.DoesNotContain(result.Data!, o => o.SchoolStudentId == studentId);
    }

    /// <summary>Review-fix contract, todos/074: the StaffContext overload (used by HomeService to avoid a
    /// re-lookup) must return the same result as the userId overload it replaces for that caller.</summary>
    [Fact]
    public async Task GetForScopeAsync_StaffContextOverload_MatchesUserIdOverload()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (adminId, _) = _db.Staff("ctx-overload-admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);

        using var ctx = _db.Context();
        var orgAccess = new OrgAccessService(ctx);
        var service = new ObligationService(ctx, orgAccess);
        var staffCtx = await orgAccess.GetStaffContextAsync(adminId);

        var byUserId = await service.GetForScopeAsync(adminId, null, null);
        var byCtx = await service.GetForScopeAsync(staffCtx!, null, null);

        Assert.True(byUserId.Success, byUserId.Message);
        Assert.True(byCtx.Success, byCtx.Message);
        Assert.Equal(byUserId.Data!.Count, byCtx.Data!.Count);
        Assert.Contains(byCtx.Data, o => o.SchoolStudentId == studentId);
    }

    // ---------------------------------------------------------------- Plan 7: goal + evaluation additions

    /// <summary>Minimal valid AuthoredDocumentVersion + DocumentInstance + template chain so a GoalRecord's
    /// (Restrict) foreign keys are satisfiable — ObligationService only reads GoalRecord/GoalObservation
    /// columns, so the template content itself is irrelevant.</summary>
    private (int InstanceId, int VersionId) SeedGoalDocumentChain(int studentId)
    {
        using var ctx = _db.Context();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = 1, Name = "T", Versions = { version } });
        ctx.SaveChanges();
        var instance = new DocumentInstance
        {
            SchoolStudentId = studentId, DocumentTypeId = 1, DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft, ValuesJson = "{}", RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();
        var authored = new AuthoredDocumentVersion
        {
            SchoolStudentId = studentId, DocumentTypeId = 1, DocumentTemplateVersionId = version.Id,
            VersionNumber = 1, ValuesJson = "{}", FinalizedByUserId = 1, FinalizedAt = DateTime.UtcNow
        };
        ctx.AuthoredDocumentVersions.Add(authored);
        ctx.SaveChanges();
        return (instance.Id, authored.Id);
    }

    private int SeedGoalRecord(int studentId, DateTime projectedAt, DateTime? lastObservedAt, GoalRecordStatus status = GoalRecordStatus.Active)
    {
        var (instanceId, versionId) = SeedGoalDocumentChain(studentId);
        using var ctx = _db.Context();
        var goal = new GoalRecord
        {
            SchoolStudentId = studentId,
            LineageId = Guid.NewGuid(),
            AuthoredDocumentVersionId = versionId,
            DocumentInstanceId = instanceId,
            FieldKey = Guid.NewGuid(),
            GoalText = "Read at grade level",
            Status = status,
            ProjectedAt = projectedAt
        };
        ctx.GoalRecords.Add(goal);
        ctx.SaveChanges();
        if (lastObservedAt.HasValue)
        {
            ctx.GoalObservations.Add(new GoalObservation { GoalRecordId = goal.Id, ObservedAt = lastObservedAt.Value, Value = 10m, RecordedByUserId = 1 });
            ctx.SaveChanges();
        }
        return goal.Id;
    }

    [Fact]
    public async Task GetForStudentAsync_ActiveGoalStaleAt45Days_ProducesGoalObservationStaleObligation()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-goal@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Lee", last: "Lead");
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        SeedGoalRecord(studentId, projectedAt: DateTime.UtcNow.Date.AddDays(-GoalRecordRules.StaleAfterDays), lastObservedAt: null);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(leadUserId, studentId);

        Assert.True(result.Success, result.Message);
        var stale = Assert.Single(result.Data!, o => o.Kind == ObligationKind.GoalObservationStale);
        Assert.Equal(ObligationStatus.Overdue, stale.Status);
    }

    [Fact]
    public async Task GetForStudentAsync_StaleGoal_OwnerIsActiveProviderNotLead()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-owner@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Lee", last: "Lead");
        var (providerUserId, _) = _db.Staff("provider-owner@example.com", districtId, schoolId, Models.OrgRoleIds.RelatedServiceProvider, first: "Pat", last: "Provider");
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(studentId, providerUserId, TeamRole.SpeechLanguagePathologist, isLead: false);

        SeedGoalRecord(studentId, projectedAt: DateTime.UtcNow.Date.AddDays(-GoalRecordRules.StaleAfterDays), lastObservedAt: null);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(leadUserId, studentId);

        var stale = Assert.Single(result.Data!, o => o.Kind == ObligationKind.GoalObservationStale);
        Assert.Equal(providerUserId, stale.OwnerUserId);
    }

    [Fact]
    public async Task GetForStudentAsync_StaleGoal_NoProviderOnTeam_OwnerFallsBackToLead()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-fallback@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Lee", last: "Lead");
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        SeedGoalRecord(studentId, projectedAt: DateTime.UtcNow.Date.AddDays(-GoalRecordRules.StaleAfterDays), lastObservedAt: null);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(leadUserId, studentId);

        var stale = Assert.Single(result.Data!, o => o.Kind == ObligationKind.GoalObservationStale);
        Assert.Equal(leadUserId, stale.OwnerUserId);
    }

    [Fact]
    public async Task GetForStudentAsync_GoalObservedRecently_NoStaleObligation()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-fresh@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        SeedGoalRecord(studentId, projectedAt: DateTime.UtcNow.Date.AddDays(-90), lastObservedAt: DateTime.UtcNow.Date.AddDays(-1));

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetForStudentAsync(leadUserId, studentId);

        Assert.DoesNotContain(result.Data!, o => o.Kind == ObligationKind.GoalObservationStale);
    }

    [Fact]
    public async Task GetForStudentAsync_OpenEvaluationCase_ProducesEvaluationDeterminationObligation()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-eval@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        var dueDate = DateTime.UtcNow.Date.AddDays(10);
        using (var ctx = _db.Context())
        {
            ctx.EvaluationCases.Add(new EvaluationCase
            {
                SchoolStudentId = studentId, Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date.AddDays(-20),
                Status = EvaluationCaseStatus.InProgress, ConsentReceivedAt = DateTime.UtcNow.Date.AddDays(-10),
                DeterminationDueDate = dueDate, CreatedByUserId = leadUserId
            });
            ctx.SaveChanges();
        }

        using var readCtx = _db.Context();
        var result = await CreateService(readCtx).GetForStudentAsync(leadUserId, studentId);

        var obligation = Assert.Single(result.Data!, o => o.Kind == ObligationKind.EvaluationDetermination);
        Assert.Equal(dueDate, obligation.DueDate);
        Assert.Equal(leadUserId, obligation.OwnerUserId);
    }

    [Fact]
    public async Task GetForStudentAsync_OverdueEvaluatorAssignment_ProducesEvaluatorSubmissionObligationOwnedByEvaluator()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-assign@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var (evaluatorUserId, _) = _db.Staff("evaluator-assign@example.com", districtId, schoolId, Models.OrgRoleIds.RelatedServiceProvider, first: "Evan", last: "Uator");
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        using (var ctx = _db.Context())
        {
            var kase = new EvaluationCase
            {
                SchoolStudentId = studentId, Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date.AddDays(-30),
                Status = EvaluationCaseStatus.InProgress, CreatedByUserId = leadUserId
            };
            ctx.EvaluationCases.Add(kase);
            ctx.SaveChanges();
            ctx.EvaluatorAssignments.Add(new EvaluatorAssignment
            {
                EvaluationCaseId = kase.Id, UserId = evaluatorUserId, Domain = "Speech-Language",
                DueDate = DateTime.UtcNow.Date.AddDays(-5)
            });
            ctx.SaveChanges();
        }

        using var readCtx = _db.Context();
        var result = await CreateService(readCtx).GetForStudentAsync(leadUserId, studentId);

        var obligation = Assert.Single(result.Data!, o => o.Kind == ObligationKind.EvaluatorSubmission);
        Assert.Equal(evaluatorUserId, obligation.OwnerUserId);
        Assert.Equal(ObligationStatus.Overdue, obligation.Status);
    }

    [Fact]
    public async Task GetForStudentAsync_SubmittedEvaluatorAssignment_NoEvaluatorSubmissionObligation()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead-submitted@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var (evaluatorUserId, _) = _db.Staff("evaluator-submitted@example.com", districtId, schoolId, Models.OrgRoleIds.RelatedServiceProvider);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        using (var ctx = _db.Context())
        {
            var kase = new EvaluationCase
            {
                SchoolStudentId = studentId, Kind = EvaluationCaseKind.Initial, ReferralDate = DateTime.UtcNow.Date.AddDays(-30),
                Status = EvaluationCaseStatus.InProgress, CreatedByUserId = leadUserId
            };
            ctx.EvaluationCases.Add(kase);
            ctx.SaveChanges();
            ctx.EvaluatorAssignments.Add(new EvaluatorAssignment
            {
                EvaluationCaseId = kase.Id, UserId = evaluatorUserId, Domain = "Speech-Language",
                DueDate = DateTime.UtcNow.Date.AddDays(-5), SubmittedAt = DateTime.UtcNow.Date.AddDays(-6)
            });
            ctx.SaveChanges();
        }

        using var readCtx = _db.Context();
        var result = await CreateService(readCtx).GetForStudentAsync(leadUserId, studentId);

        Assert.DoesNotContain(result.Data!, o => o.Kind == ObligationKind.EvaluatorSubmission);
    }

    public void Dispose() => _db.Dispose();
}
