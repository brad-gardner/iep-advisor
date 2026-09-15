using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 3 IEP team rules on <see cref="StudentTeamService"/>: single active lead, cross-building
/// providers, lead-removal guard, permission defaults/overrides, authorization (admin in scope or the
/// current lead), and the evidence bundle reading team rows. Real SQLite in-memory via <see cref="RosterTestDb"/>.
/// </summary>
public sealed class StudentTeamServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private static StudentTeamService Service(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), NullLogger<StudentTeamService>.Instance);

    private (int District, int SchoolA, int SchoolB, int Admin, int Student) Org()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var schoolB = _db.School(district, "B");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var student = _db.Student(schoolA, "Jordan", "Ellis");
        return (district, schoolA, schoolB, admin, student);
    }

    [Fact]
    public async Task AddMember_CaseManagerWithNoLead_BecomesLead_WithOwnerAccess_AndMirrorsCaseManagerUserId()
    {
        var o = Org();
        var (cmUser, cmProfile) = _db.Staff("cm@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher, "Steph", "Case");

        using var ctx = _db.Context();
        var result = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = cmProfile, TeamRole = TeamRole.CaseManager });

        Assert.True(result.Success, result.Message);
        Assert.True(result.Data!.IsLead);
        Assert.Equal(AccessRole.Owner, result.Data.AccessRole);
        Assert.Equal("Steph", result.Data.FirstName);
        Assert.Equal("Teacher", result.Data.OrgRoleName);
        Assert.Equal(cmUser, ctx.SchoolStudents.AsNoTracking().Single(s => s.Id == o.Student).CaseManagerUserId);
    }

    [Fact]
    public async Task AddMember_WithIsLead_DemotesPreviousLead_ButKeepsThemOnTheTeam()
    {
        var o = Org();
        var (oldUser, _) = _db.Staff("old@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (newUser, newProfile) = _db.Staff("new@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        _db.TeamMember(o.Student, oldUser, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = newProfile, TeamRole = TeamRole.InterventionSpecialist, IsLead = true });

        Assert.True(result.Success, result.Message);
        using var check = _db.Context();
        var leads = check.StudentTeamMembers.Where(m => m.SchoolStudentId == o.Student && m.IsActive && m.IsLead).ToList();
        Assert.Equal(newUser, Assert.Single(leads).UserId);
        var old = check.StudentTeamMembers.Single(m => m.UserId == oldUser);
        Assert.True(old.IsActive);
        Assert.False(old.IsLead);
        Assert.Equal(newUser, check.SchoolStudents.Single(s => s.Id == o.Student).CaseManagerUserId);
    }

    [Fact]
    public async Task SetLead_MovesLeadBetweenMembers_KeepingExactlyOneActiveLead()
    {
        var o = Org();
        var (a, _) = _db.Staff("a@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (b, _) = _db.Staff("b@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        _db.TeamMember(o.Student, a, TeamRole.CaseManager, isLead: true);
        var memberB = _db.TeamMember(o.Student, b, TeamRole.Counselor);

        using var ctx = _db.Context();
        var result = await Service(ctx).SetLeadAsync(o.Admin, o.Student, memberB);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Data!.IsLead);
        using var check = _db.Context();
        Assert.Equal(b, Assert.Single(check.StudentTeamMembers.Where(m => m.SchoolStudentId == o.Student && m.IsLead)).UserId);
        Assert.Equal(b, check.SchoolStudents.Single(s => s.Id == o.Student).CaseManagerUserId);
    }

    [Fact]
    public async Task AddMember_RelatedServiceProviderFromAnotherBuilding_IsAllowed_TeacherFromAnotherBuildingIsNot()
    {
        var o = Org();
        var (_, slpProfile) = _db.Staff("slp@x.com", o.District, o.SchoolB, OrgRoleIds.RelatedServiceProvider);
        var (_, teacherBProfile) = _db.Staff("tb@x.com", o.District, o.SchoolB, OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var slp = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = slpProfile, TeamRole = TeamRole.SpeechLanguagePathologist });
        var teacherB = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = teacherBProfile, TeamRole = TeamRole.GeneralEducationTeacher });

        Assert.True(slp.Success, slp.Message);
        Assert.Equal(AccessRole.Collaborator, slp.Data!.AccessRole);
        Assert.False(teacherB.Success);
        Assert.Equal("That staff member is not at this student's school.", teacherB.Message);
    }

    [Fact]
    public async Task AddMember_DistrictAdmin_IsRefused()
    {
        var o = Org();
        var (_, otherAdminProfile) = _db.Staff("da2@x.com", o.District, null, OrgRoleIds.DistrictAdmin);

        using var ctx = _db.Context();
        var result = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = otherAdminProfile, TeamRole = TeamRole.LeaRepresentative });

        Assert.False(result.Success);
        Assert.Equal("A District Admin does not need a per-student assignment.", result.Message);
    }

    [Theory]
    [InlineData(TeamRole.CaseManager, AccessRole.Owner)]
    [InlineData(TeamRole.SpeechLanguagePathologist, AccessRole.Collaborator)]
    [InlineData(TeamRole.GeneralEducationTeacher, AccessRole.Collaborator)]
    [InlineData(TeamRole.LeaRepresentative, AccessRole.Viewer)]
    [InlineData(TeamRole.Interpreter, AccessRole.Viewer)]
    public async Task AddMember_AppliesPermissionDefaultForRole(TeamRole role, AccessRole expected)
    {
        var o = Org();
        var (userId, profile) = _db.Staff("m@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var result = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = profile, TeamRole = role });

        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, result.Data!.AccessRole);
        Assert.Equal(expected, ctx.SchoolStudentAccesses.AsNoTracking().Single(a => a.SchoolStudentId == o.Student && a.UserId == userId).Role);
    }

    [Fact]
    public async Task AddMember_ExplicitAccessRoleOverridesDefault_AndUpdateMemberCanChangeIt()
    {
        var o = Org();
        var (_, profile) = _db.Staff("m@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var added = await Service(ctx).AddMemberAsync(o.Admin, o.Student, new AddTeamMemberModel { StaffProfileId = profile, TeamRole = TeamRole.Interpreter, AccessRole = AccessRole.Collaborator });
        var updated = await Service(ctx).UpdateMemberAsync(o.Admin, o.Student, added.Data!.Id, new UpdateTeamMemberModel { TeamRole = TeamRole.Counselor, AccessRole = AccessRole.Viewer });

        Assert.Equal(AccessRole.Collaborator, added.Data.AccessRole);
        Assert.True(updated.Success, updated.Message);
        Assert.Equal(TeamRole.Counselor, updated.Data!.TeamRole);
        Assert.Equal(AccessRole.Viewer, updated.Data.AccessRole);
    }

    [Fact]
    public async Task RemoveMember_LeadWithOthersRemaining_IsRefused_LeadAloneCanBeRemoved()
    {
        var o = Org();
        var (leadUser, _) = _db.Staff("lead@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (otherUser, _) = _db.Staff("other@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var lead = _db.TeamMember(o.Student, leadUser, TeamRole.CaseManager, isLead: true);
        var other = _db.TeamMember(o.Student, otherUser, TeamRole.Counselor);

        using var ctx = _db.Context();
        var svc = Service(ctx);
        var refused = await svc.RemoveMemberAsync(o.Admin, o.Student, lead);
        var removedOther = await svc.RemoveMemberAsync(o.Admin, o.Student, other);
        var removedLead = await svc.RemoveMemberAsync(o.Admin, o.Student, lead);

        Assert.False(refused.Success);
        Assert.Equal("Choose a new lead case manager first.", refused.Message);
        Assert.True(removedOther.Success, removedOther.Message);
        Assert.True(removedLead.Success, removedLead.Message);
        using var check = _db.Context();
        Assert.Empty(check.StudentTeamMembers.Where(m => m.SchoolStudentId == o.Student && m.IsActive));
        Assert.Empty(check.SchoolStudentAccesses.Where(a => a.SchoolStudentId == o.Student && a.IsActive));
        Assert.Null(check.SchoolStudents.Single(s => s.Id == o.Student).CaseManagerUserId);
    }

    [Fact]
    public async Task AddMember_CurrentLeadMayManageTeam_OtherTeacherMayNot()
    {
        var o = Org();
        var (leadUser, _) = _db.Staff("lead@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (collabUser, _) = _db.Staff("collab@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (_, newProfile) = _db.Staff("new@x.com", o.District, o.SchoolA, OrgRoleIds.GeneralEducator);
        _db.TeamMember(o.Student, leadUser, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(o.Student, collabUser, TeamRole.InterventionSpecialist);

        using var ctx = _db.Context();
        var byCollaborator = await Service(ctx).AddMemberAsync(collabUser, o.Student, new AddTeamMemberModel { StaffProfileId = newProfile, TeamRole = TeamRole.GeneralEducationTeacher });
        var byLead = await Service(ctx).AddMemberAsync(leadUser, o.Student, new AddTeamMemberModel { StaffProfileId = newProfile, TeamRole = TeamRole.GeneralEducationTeacher });

        Assert.False(byCollaborator.Success);
        Assert.Contains("permission", byCollaborator.Message);
        Assert.True(byLead.Success, byLead.Message);
        Assert.Equal("GeneralEducator", byLead.Data!.OrgRoleName);
    }

    [Fact]
    public async Task GetTeam_ListsActiveMembersLeadFirst_WithEffectiveAccess_AndRequiresViewer()
    {
        var o = Org();
        var (leadUser, _) = _db.Staff("lead@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher, "Zoe", "Lead");
        var (aUser, _) = _db.Staff("a@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher, "Adam", "Alpha");
        var (stranger, _) = _db.Staff("s@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        _db.TeamMember(o.Student, aUser, TeamRole.Counselor, access: AccessRole.Owner);
        _db.TeamMember(o.Student, leadUser, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var team = await Service(ctx).GetTeamAsync(o.Admin, o.Student);
        var denied = await Service(ctx).GetTeamAsync(stranger, o.Student);

        Assert.True(team.Success, team.Message);
        Assert.Equal(new[] { "Zoe", "Adam" }, team.Data!.Select(m => m.FirstName));
        Assert.Equal(AccessRole.Owner, team.Data[1].AccessRole);
        Assert.False(denied.Success);
    }

    [Fact]
    public async Task EvidenceBundle_TeamItems_ComeFromTeamTable_WithRoleLabels()
    {
        var o = Org();
        var (leadUser, _) = _db.Staff("lead@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher, "Steph", "Case", title: "Intervention Specialist");
        var (slpUser, _) = _db.Staff("slp@x.com", o.District, o.SchoolB, OrgRoleIds.RelatedServiceProvider, "Rosa", "Diaz");
        var (grantOnly, _) = _db.Staff("g@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher, "Grant", "Only");
        _db.TeamMember(o.Student, leadUser, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(o.Student, slpUser, TeamRole.SpeechLanguagePathologist);
        _db.Access(o.Student, grantOnly); // legacy access row without a team row is not synthesized

        using var ctx = _db.Context();
        var org = new OrgAccessService(ctx);
        var access = new AccessService(ctx);
        var audit = new CapturingAuditLogger();
        var workspace = new StudentWorkspaceService(ctx, access, org, new NoClaudeClient(), NullLogger<StudentWorkspaceService>.Instance);
        var contributions = new ParentContributionService(ctx, access, org, audit);
        var evidence = new StudentEvidenceService(ctx, org, workspace, contributions, audit);
        var result = await evidence.BuildForStaffAsync(o.Admin, o.Student);

        Assert.True(result.Success, result.Message);
        var team = result.Data!.Items.Where(i => i.Kind == EvidenceKind.TeamMember).Select(i => i.Text).ToList();
        Assert.Equal(new[] { "Steph Case — Case Manager (lead)", "Rosa Diaz — Speech-Language Pathologist" }, team);
        Assert.All(result.Data.Items.Where(i => i.Kind == EvidenceKind.TeamMember), i => Assert.Equal("StudentTeamMember", i.SourceType));
    }

    private sealed class NoClaudeClient : Interfaces.IClaudeClient
    {
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    public void Dispose() => _db.Dispose();
}
