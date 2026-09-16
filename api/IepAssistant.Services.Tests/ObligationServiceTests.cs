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

    public void Dispose() => _db.Dispose();
}
