using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 5, deliverable C: the district compliance board, adoption, and engagement reads.
/// <see cref="ComplianceBoard_CountsMatchRosterAttentionFilters"/> is the parity guarantee: every board
/// count and the roster's matching <c>attention</c> filter total must agree, because both go through the
/// same <see cref="StudentAttentionRules"/> expression trees.
/// </summary>
public sealed class DistrictComplianceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private DistrictService CreateDistrictService(Domain.Data.ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), NullLogger<DistrictService>.Instance);

    private EducatorService CreateEducatorService(Domain.Data.ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), new CapturingAuditLogger(), NullLogger<EducatorService>.Instance);

    public void Dispose() => _db.Dispose();

    // ----------------------------------------------------------------- Parity: board counts == roster totals

    [Fact]
    public async Task ComplianceBoard_CountsMatchRosterAttentionFilters()
    {
        var districtId = _db.District();
        var schoolA = _db.School(districtId, "School A");
        var schoolB = _db.School(districtId, "School B");
        var (adminId, _) = _db.Staff("districtadmin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var (leadId, _) = _db.Staff("lead@example.com", districtId, schoolA, Models.OrgRoleIds.Teacher);

        var today = DateTime.UtcNow.Date;

        // Overdue annual review, has a lead, no family.
        var overdueAnnualStudent = _db.Student(schoolA, "Ann", "OverdueAnnual");
        _db.TeamMember(overdueAnnualStudent, leadId, TeamRole.CaseManager, isLead: true);

        // Overdue re-evaluation.
        var overdueReevalStudent = _db.Student(schoolB, "Rex", "OverdueReeval");
        _db.TeamMember(overdueReevalStudent, leadId, TeamRole.CaseManager, isLead: true);

        // Due within 30 days.
        var due30Student = _db.Student(schoolA, "Dee", "DueSoon30");
        _db.TeamMember(due30Student, leadId, TeamRole.CaseManager, isLead: true);

        // Due within 60 (but not 30) days.
        var due60Student = _db.Student(schoolB, "Six", "DueSoon60");
        _db.TeamMember(due60Student, leadId, TeamRole.CaseManager, isLead: true);

        // Unknown dates (no annual/reeval due date and no IEP/ETR date to fall back on).
        var unknownStudent = _db.Student(schoolA, "Uma", "Unknown");
        _db.TeamMember(unknownStudent, leadId, TeamRole.CaseManager, isLead: true);

        // No lead case manager at all.
        var noLeadStudent = _db.Student(schoolB, "Noel", "NoLead");

        using (var seedCtx = _db.Context())
        {
            seedCtx.SchoolStudents.Single(s => s.Id == overdueAnnualStudent).AnnualReviewDueDate = today.AddDays(-10);
            seedCtx.SchoolStudents.Single(s => s.Id == overdueReevalStudent).ReevaluationDueDate = today.AddDays(-10);
            seedCtx.SchoolStudents.Single(s => s.Id == due30Student).AnnualReviewDueDate = today.AddDays(20);
            seedCtx.SchoolStudents.Single(s => s.Id == due60Student).AnnualReviewDueDate = today.AddDays(45);
            // The other four students all need a resolvable date too, else they'd ALSO count as
            // UnknownDates and pollute the parity comparison for that bucket.
            seedCtx.SchoolStudents.Single(s => s.Id == overdueAnnualStudent).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == overdueReevalStudent).AnnualReviewDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == due30Student).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == due60Student).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == noLeadStudent).AnnualReviewDueDate = today.AddDays(400);
            seedCtx.SchoolStudents.Single(s => s.Id == noLeadStudent).ReevaluationDueDate = today.AddDays(400);
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        var board = await CreateDistrictService(ctx).GetComplianceBoardAsync(adminId, null, null, null);
        Assert.True(board.Success, board.Message);
        var summary = board.Data!.Summary;

        var educator = CreateEducatorService(ctx);
        async Task<int> RosterTotal(StudentAttention attention)
        {
            var result = await educator.SearchStudentsAsync(adminId, new StudentSearchQuery { Attention = attention, PageSize = 200 });
            Assert.True(result.Success, result.Message);
            return result.Data!.Total;
        }

        Assert.Equal(await RosterTotal(StudentAttention.OverdueAnnual), summary.OverdueAnnual);
        Assert.Equal(await RosterTotal(StudentAttention.OverdueReeval), summary.OverdueReeval);
        Assert.Equal(await RosterTotal(StudentAttention.Due30), summary.Due30);
        Assert.Equal(await RosterTotal(StudentAttention.Due60), summary.Due60);
        Assert.Equal(await RosterTotal(StudentAttention.UnknownDates), summary.UnknownDates);
        Assert.Equal(await RosterTotal(StudentAttention.NoCaseManager), summary.NoLead);

        // Sanity: the buckets actually caught the students they were designed for (not all zero).
        Assert.True(summary.OverdueAnnual >= 1);
        Assert.True(summary.OverdueReeval >= 1);
        Assert.True(summary.Due30 >= 1);
        Assert.True(summary.Due60 >= 1); // cumulative: includes the Due30 student too
        Assert.True(summary.UnknownDates >= 1);
        Assert.True(summary.NoLead >= 1);

        // Every school in scope appears, and per-school rows sum to the district summary.
        Assert.Equal(2, board.Data.BySchool.Count);
        Assert.Equal(summary.OverdueAnnual, board.Data.BySchool.Sum(r => r.OverdueAnnual));
        Assert.Equal(summary.ActiveStudents, board.Data.BySchool.Sum(r => r.ActiveStudents));

        // Drill map points back at the roster with the matching attention filter.
        Assert.Equal("attention=OverdueAnnual", board.Data.Drill["overdueAnnual"]);
    }

    // ----------------------------------------------------------------- SchoolAdmin scoping

    [Fact]
    public async Task ComplianceBoard_SchoolAdmin_ForcedToOwnSchool_IgnoresRequestedSchoolId()
    {
        var districtId = _db.District();
        var schoolA = _db.School(districtId, "School A");
        var schoolB = _db.School(districtId, "School B");
        var (schoolAdminId, _) = _db.Staff("schooladmin@example.com", districtId, schoolA, Models.OrgRoleIds.SchoolAdmin);

        var studentInA = _db.Student(schoolA, "In", "SchoolA");
        var studentInB = _db.Student(schoolB, "In", "SchoolB");
        using (var seedCtx = _db.Context())
        {
            seedCtx.SchoolStudents.Single(s => s.Id == studentInA).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
            seedCtx.SchoolStudents.Single(s => s.Id == studentInB).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        // Explicitly ask for School B — a SchoolAdmin must still only ever see their own school.
        var board = await CreateDistrictService(ctx).GetComplianceBoardAsync(schoolAdminId, schoolB, null, null);

        Assert.True(board.Success, board.Message);
        var row = Assert.Single(board.Data!.BySchool);
        Assert.Equal(schoolA, row.SchoolId);
        Assert.Equal(1, row.OverdueAnnual);
    }

    [Fact]
    public async Task ComplianceBoard_SchoolAdmin_WithNoSchoolBinding_ReturnsEmptyNotError()
    {
        var districtId = _db.District();
        var (schoolAdminId, _) = _db.Staff("unbound@example.com", districtId, null, Models.OrgRoleIds.SchoolAdmin);

        using var ctx = _db.Context();
        var board = await CreateDistrictService(ctx).GetComplianceBoardAsync(schoolAdminId, null, null, null);

        Assert.True(board.Success, board.Message);
        Assert.Empty(board.Data!.BySchool);
        Assert.Equal(0, board.Data.Summary.ActiveStudents);
    }

    [Fact]
    public async Task ComplianceBoard_NonAdmin_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (teacherId, _) = _db.Staff("teacher@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var board = await CreateDistrictService(ctx).GetComplianceBoardAsync(teacherId, null, null, null);

        Assert.False(board.Success);
    }

    // ----------------------------------------------------------------- Adoption

    [Fact]
    public async Task Adoption_ActiveRule_CountsStaffWithAnAccessAuditLogEntryInWindow()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminId, _) = _db.Staff("adoptionadmin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var (activeStaffId, _) = _db.Staff("active@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var (inactiveStaffId, _) = _db.Staff("inactive@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);

        using (var seedCtx = _db.Context())
        {
            seedCtx.AccessAuditLogs.Add(new AccessAuditLog { Action = AuditAction.View, ActorUserId = activeStaffId, ResourceType = "SchoolStudent", ResourceId = 1, CreatedAt = DateTime.UtcNow.AddDays(-2) });
            // Outside the 30-day window -> does not count as active.
            seedCtx.AccessAuditLogs.Add(new AccessAuditLog { Action = AuditAction.View, ActorUserId = inactiveStaffId, ResourceType = "SchoolStudent", ResourceId = 1, CreatedAt = DateTime.UtcNow.AddDays(-45) });
            seedCtx.SaveChanges();
        }

        using var ctx = _db.Context();
        // Scoped to the school so the district-level admin (bound to no single school) doesn't inflate
        // staffTotal — GetAdoptionAsync's unfiltered (schoolId: null) total is district-wide by design.
        var result = await CreateDistrictService(ctx).GetAdoptionAsync(adminId, schoolId, 30);

        Assert.True(result.Success, result.Message);
        Assert.Equal(30, result.Data!.Days);
        Assert.Equal(2, result.Data.StaffTotal);
        Assert.Equal(1, result.Data.StaffActiveLast14);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.ActiveRule));
    }

    // ----------------------------------------------------------------- Engagement

    [Fact]
    public async Task Engagement_CountsActiveStudentsAndFamilyLinks()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminId, _) = _db.Staff("engagementadmin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        var parentUserId = _db.SeedUser("engagementparent@example.com");

        var linkedStudent = _db.Student(schoolId, "Linked", "Kid");
        var unlinkedStudent = _db.Student(schoolId, "Unlinked", "Kid");
        var childProfile = _db.ChildProfile(parentUserId);
        _db.ChildLink(linkedStudent, childProfile);

        using var ctx = _db.Context();
        var result = await CreateDistrictService(ctx).GetEngagementAsync(adminId, null);

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Data!.ActiveStudents);
        Assert.Equal(1, result.Data.StudentsWithFamilyLink);
        Assert.Equal(0, result.Data.DraftsShared);
        Assert.Equal(0, result.Data.ResponsesReceived);
        var row = Assert.Single(result.Data.BySchool);
        Assert.Equal(2, row.ActiveStudents);
        Assert.Equal(1, row.StudentsWithFamilyLink);
    }
}
