using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 3 student record lifecycle on <see cref="EducatorService"/>: edit / exit / reactivate / archive /
/// transfer authorization and side effects, external-id uniqueness per district, roster search + paging,
/// and bulk case-manager assignment. Real SQLite in-memory engine via <see cref="RosterTestDb"/>.
/// </summary>
public sealed class StudentLifecycleTests : IDisposable
{
    private readonly RosterTestDb _db = new();
    private readonly CapturingAuditLogger _audit = new();

    private EducatorService Service(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), _audit, NullLogger<EducatorService>.Instance);

    private static UpdateSchoolStudentModel Update(string first = "Sam", string last = "Student", string? externalId = null, GradeLevel? grade = null) => new()
    {
        FirstName = first,
        LastName = last,
        ExternalStudentId = externalId,
        GradeLevel = grade,
        DateOfBirth = new DateTime(2014, 5, 6),
        DisabilityCategory = DisabilityCategory.Autism,
        HomeLanguage = "es",
        IepDate = new DateTime(2026, 1, 15)
    };

    // ----------------------------------------------------------------- Update

    [Fact]
    public async Task UpdateStudent_CollaboratorTeacher_UpdatesAllFieldsAndAuditsEdit()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (teacher, _) = _db.Staff("t@x.com", district, school, OrgRoleIds.Teacher);
        var student = _db.Student(school, "Old", "Name");
        _db.Access(student, teacher, AccessRole.Collaborator);

        using var ctx = _db.Context();
        var result = await Service(ctx).UpdateStudentAsync(teacher, student, Update("New", "Person", "000123", GradeLevel.G7));

        Assert.True(result.Success, result.Message);
        Assert.Equal("New", result.Data!.FirstName);
        Assert.Equal("000123", result.Data.ExternalStudentId);
        Assert.Equal(GradeLevel.G7, result.Data.GradeLevel);
        Assert.Equal(DisabilityCategory.Autism, result.Data.DisabilityCategory);
        Assert.Equal("es", result.Data.HomeLanguage);
        Assert.Equal(new DateTime(2026, 1, 15), result.Data.IepDate);
        Assert.True(result.Data.IsActive);
        var audit = Assert.Single(_audit.Entries);
        Assert.Equal((AuditAction.Edit, "SchoolStudent", student), (audit.Action, audit.ResourceType, audit.ResourceId));
    }

    [Fact]
    public async Task UpdateStudent_ViewerTeacher_IsDenied()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (teacher, _) = _db.Staff("t@x.com", district, school, OrgRoleIds.Teacher);
        var student = _db.Student(school);
        _db.Access(student, teacher, AccessRole.Viewer);

        using var ctx = _db.Context();
        var result = await Service(ctx).UpdateStudentAsync(teacher, student, Update());

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task UpdateStudent_DuplicateExternalIdInDistrict_IsRejected_ButSameIdInOtherDistrictIsFine()
    {
        var district = _db.District("A");
        var school = _db.School(district, "Maple");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        _db.Student(school, "Taken", externalId: "000123");
        var student = _db.Student(school, "Other");

        var otherDistrict = _db.District("B");
        var otherSchool = _db.School(otherDistrict, "Oak");
        var (otherAdmin, _) = _db.Staff("db@x.com", otherDistrict, null, OrgRoleIds.DistrictAdmin);
        var otherStudent = _db.Student(otherSchool, "Elsewhere");

        using var ctx = _db.Context();
        var dup = await Service(ctx).UpdateStudentAsync(admin, student, Update(externalId: "000123"));
        var ok = await Service(ctx).UpdateStudentAsync(otherAdmin, otherStudent, Update(externalId: "000123"));

        Assert.False(dup.Success);
        Assert.Equal("Student ID already in use in this district.", dup.Message);
        Assert.True(ok.Success, ok.Message);
        Assert.Equal("000123", ok.Data!.ExternalStudentId);
    }

    // ----------------------------------------------------------------- Exit / reactivate / archive authz

    [Fact]
    public async Task ExitStudent_Teacher_IsDenied_EvenAsOwner()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (teacher, _) = _db.Staff("t@x.com", district, school, OrgRoleIds.Teacher);
        var student = _db.Student(school);
        _db.Access(student, teacher, AccessRole.Owner);

        using var ctx = _db.Context();
        var result = await Service(ctx).ExitStudentAsync(teacher, student, new ExitStudentModel { ExitReason = ExitReason.Graduated });

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message);
        Assert.Equal(StudentStatus.Active, ctx.SchoolStudents.AsNoTracking().Single(s => s.Id == student).Status);
    }

    [Fact]
    public async Task ExitStudent_SchoolAdminInScope_SetsExitedFieldsAndClearsIsActive()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (admin, _) = _db.Staff("sa@x.com", district, school, OrgRoleIds.SchoolAdmin);
        var student = _db.Student(school);
        var exitedAt = new DateTime(2026, 6, 1);

        using var ctx = _db.Context();
        var result = await Service(ctx).ExitStudentAsync(admin, student, new ExitStudentModel { ExitReason = ExitReason.Graduated, ExitedAt = exitedAt });

        Assert.True(result.Success, result.Message);
        Assert.Equal(StudentStatus.Exited, result.Data!.Status);
        Assert.Equal(ExitReason.Graduated, result.Data.ExitReason);
        Assert.Equal(exitedAt, result.Data.ExitedAt);
        Assert.False(result.Data.IsActive);
        Assert.Contains(_audit.Entries, e => e.Action == AuditAction.Edit && e.ResourceType == "SchoolStudent" && e.ResourceId == student);
    }

    [Fact]
    public async Task ExitStudent_DistrictAdminOfOtherDistrict_IsDenied()
    {
        var district = _db.District("A");
        var school = _db.School(district, "Maple");
        var student = _db.Student(school);
        var otherDistrict = _db.District("B");
        var (otherAdmin, _) = _db.Staff("db@x.com", otherDistrict, null, OrgRoleIds.DistrictAdmin);

        using var ctx = _db.Context();
        var result = await Service(ctx).ExitStudentAsync(otherAdmin, student, new ExitStudentModel { ExitReason = ExitReason.Withdrawn });

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message);
    }

    [Fact]
    public async Task ReactivateStudent_AfterExit_RestoresActiveAndClearsExitFields()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var student = _db.Student(school);

        using var ctx = _db.Context();
        await Service(ctx).ExitStudentAsync(admin, student, new ExitStudentModel { ExitReason = ExitReason.Transferred });
        var result = await Service(ctx).ReactivateStudentAsync(admin, student);

        Assert.True(result.Success, result.Message);
        Assert.Equal(StudentStatus.Active, result.Data!.Status);
        Assert.Null(result.Data.ExitedAt);
        Assert.Null(result.Data.ExitReason);
        Assert.True(result.Data.IsActive);
    }

    [Fact]
    public async Task ArchiveStudent_SchoolAdminOfOtherSchool_IsDenied_AndInScopeArchives()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var schoolB = _db.School(district, "B");
        var (adminA, _) = _db.Staff("a@x.com", district, schoolA, OrgRoleIds.SchoolAdmin);
        var (adminB, _) = _db.Staff("b@x.com", district, schoolB, OrgRoleIds.SchoolAdmin);
        var student = _db.Student(schoolA);

        using var ctx = _db.Context();
        var denied = await Service(ctx).ArchiveStudentAsync(adminB, student);
        var allowed = await Service(ctx).ArchiveStudentAsync(adminA, student);

        Assert.False(denied.Success);
        Assert.Contains("permission", denied.Message);
        Assert.True(allowed.Success, allowed.Message);
        Assert.Equal(StudentStatus.Archived, allowed.Data!.Status);
        Assert.False(allowed.Data.IsActive);
    }

    [Fact]
    public async Task GetStudents_DefaultsToActiveOnly_AndModelIsActiveMirrorsStatus()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var active = _db.Student(school, "Active");
        _db.Student(school, "Gone", status: StudentStatus.Exited);
        _db.Student(school, "Old", status: StudentStatus.Archived);

        using var ctx = _db.Context();
        var result = await Service(ctx).GetStudentsAsync(admin);

        var only = Assert.Single(result.Data!);
        Assert.Equal(active, only.Id);
        Assert.True(only.IsActive);
    }

    // ----------------------------------------------------------------- Transfer

    [Fact]
    public async Task TransferStudent_KeepsDocumentsAndLinks_DeactivatesOutOfBuildingStaff_KeepsProvider()
    {
        var district = _db.District(stateCode: "OH");
        var schoolA = _db.School(district, "A", "OH");
        var schoolB = _db.School(district, "B", "OH");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var (teacherA, _) = _db.Staff("ta@x.com", district, schoolA, OrgRoleIds.Teacher);
        var (teacherB, _) = _db.Staff("tb@x.com", district, schoolB, OrgRoleIds.Teacher);
        var (slp, _) = _db.Staff("slp@x.com", district, schoolA, OrgRoleIds.RelatedServiceProvider);
        var student = _db.Student(schoolA, stateCode: "OH");
        _db.TeamMember(student, teacherA, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(student, teacherB, TeamRole.GeneralEducationTeacher);
        _db.TeamMember(student, slp, TeamRole.SpeechLanguagePathologist);
        int draftId, linkId;
        using (var seed = _db.Context())
        {
            var link = new ChildLink { SchoolStudentId = student, InviteEmail = "p@x.com", IsActive = true };
            seed.ChildLinks.Add(link);
            var draft = new IepDraft { SchoolStudentId = student, Title = "Draft IEP" };
            seed.IepDrafts.Add(draft);
            seed.SaveChanges();
            draftId = draft.Id;
            linkId = link.Id;
        }

        using var ctx = _db.Context();
        var result = await Service(ctx).TransferStudentAsync(admin, student, schoolB);

        Assert.True(result.Success, result.Message);
        Assert.Equal(schoolB, result.Data!.SchoolId);
        Assert.Null(result.Data.CaseManagerUserId); // the lead was at school A and could not follow
        using var check = _db.Context();
        var members = check.StudentTeamMembers.Where(m => m.SchoolStudentId == student).ToDictionary(m => m.UserId);
        Assert.False(members[teacherA].IsActive);
        Assert.False(members[teacherA].IsLead);
        Assert.Contains("transfer", members[teacherA].Note);
        Assert.True(members[teacherB].IsActive);
        Assert.True(members[slp].IsActive); // RelatedServiceProvider spans buildings
        var access = check.SchoolStudentAccesses.Where(a => a.SchoolStudentId == student).ToDictionary(a => a.UserId);
        Assert.False(access[teacherA].IsActive);
        Assert.True(access[teacherB].IsActive);
        Assert.True(access[slp].IsActive);
        Assert.Equal(student, check.IepDrafts.Single(d => d.Id == draftId).SchoolStudentId);
        Assert.Equal(student, check.ChildLinks.Single(l => l.Id == linkId).SchoolStudentId);
        Assert.Contains(_audit.Entries, e => e.Action == AuditAction.Edit && e.ResourceType == "SchoolStudent" && e.ResourceId == student);
    }

    [Fact]
    public async Task TransferStudent_InheritedStateFollowsNewSchool_ExplicitStateIsKept()
    {
        var district = _db.District(stateCode: "OH");
        var schoolA = _db.School(district, "A", "OH");
        var schoolB = _db.School(district, "B", "PA");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var inherited = _db.Student(schoolA, "Inh", stateCode: "OH");
        var explicitState = _db.Student(schoolA, "Exp", stateCode: "NY");

        using var ctx = _db.Context();
        var a = await Service(ctx).TransferStudentAsync(admin, inherited, schoolB);
        var b = await Service(ctx).TransferStudentAsync(admin, explicitState, schoolB);

        Assert.Equal("PA", a.Data!.StateCode);
        Assert.Equal("NY", b.Data!.StateCode);
    }

    [Fact]
    public async Task TransferStudent_SchoolAdmin_IsDenied_AndInactiveOrForeignSchoolIsNotFound()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var inactive = _db.School(district, "Closed", isActive: false);
        var foreign = _db.School(_db.District("Other"), "Elsewhere");
        var (schoolAdmin, _) = _db.Staff("sa@x.com", district, schoolA, OrgRoleIds.SchoolAdmin);
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var student = _db.Student(schoolA);

        using var ctx = _db.Context();
        var denied = await Service(ctx).TransferStudentAsync(schoolAdmin, student, schoolA);
        var toInactive = await Service(ctx).TransferStudentAsync(admin, student, inactive);
        var toForeign = await Service(ctx).TransferStudentAsync(admin, student, foreign);

        Assert.Contains("permission", denied.Message);
        Assert.Equal("School not found.", toInactive.Message);
        Assert.Equal("School not found.", toForeign.Message);
    }

    // ----------------------------------------------------------------- Search + paging

    [Fact]
    public async Task SearchStudents_MatchesNameOrExternalId_FiltersStatusGradeSchool_AndPages()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var schoolB = _db.School(district, "B");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        _db.Student(schoolA, "Ann", "Zed", "000777", GradeLevel.G6);
        _db.Student(schoolA, "Bob", "Young", "000778", GradeLevel.G7);
        _db.Student(schoolB, "Cal", "Xu", "000779", GradeLevel.G7);
        _db.Student(schoolA, "Dee", "Wu", "000780", GradeLevel.G7, StudentStatus.Exited);

        using var ctx = _db.Context();
        var svc = Service(ctx);
        var byId = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Query = "0778" });
        var byName = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Query = "zed" });
        var grade7Active = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Grade = GradeLevel.G7 });
        var grade7All = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Grade = GradeLevel.G7, Status = null });
        var schoolBOnly = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { SchoolId = schoolB });
        var page2 = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Page = 2, PageSize = 2 });

        Assert.Equal("Bob", Assert.Single(byId.Data!.Items).FirstName);
        Assert.Equal("Ann", Assert.Single(byName.Data!.Items).FirstName);
        Assert.Equal(new[] { "Cal", "Bob" }, grade7Active.Data!.Items.Select(s => s.FirstName)); // ordered by last name: Xu, Young
        Assert.Equal(3, grade7All.Data!.Total);
        Assert.Equal("Cal", Assert.Single(schoolBOnly.Data!.Items).FirstName);
        Assert.Equal(3, page2.Data!.Total);
        Assert.Equal(2, page2.Data.Page);
        Assert.Equal("Ann", Assert.Single(page2.Data.Items).FirstName); // Xu, Young | Zed
    }

    [Fact]
    public async Task SearchStudents_SchoolAdmin_CannotWidenScopeWithSchoolId()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var schoolB = _db.School(district, "B");
        var (adminA, _) = _db.Staff("a@x.com", district, schoolA, OrgRoleIds.SchoolAdmin);
        _db.Student(schoolA, "Mine");
        _db.Student(schoolB, "Theirs");

        using var ctx = _db.Context();
        var result = await Service(ctx).SearchStudentsAsync(adminA, new StudentSearchQuery { SchoolId = schoolB });

        Assert.True(result.Success);
        Assert.Empty(result.Data!.Items);
        Assert.Equal(0, result.Data.Total);
    }

    [Fact]
    public async Task SearchStudents_RelatedServiceProvider_SeesGrantedStudentsAcrossBuildings()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var schoolB = _db.School(district, "B");
        var (slp, _) = _db.Staff("slp@x.com", district, schoolA, OrgRoleIds.RelatedServiceProvider);
        var inA = _db.Student(schoolA, "InA");
        var inB = _db.Student(schoolB, "InB");
        _db.Student(schoolB, "NoGrant");
        _db.Access(inA, slp);
        _db.Access(inB, slp);

        using var ctx = _db.Context();
        var result = await Service(ctx).SearchStudentsAsync(slp, new StudentSearchQuery());

        Assert.Equal(new[] { inA, inB }, result.Data!.Items.Select(s => s.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task SearchStudents_AttentionFilters_NarrowServerSide_AndPage()
    {
        var district = _db.District();
        var school = _db.School(district, "A");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var (activeLead, _) = _db.Staff("lead@x.com", district, school, OrgRoleIds.Teacher);
        var (goneLead, _) = _db.Staff("gone@x.com", district, school, OrgRoleIds.Teacher, isActive: false);
        var withLead = _db.Student(school, "Has", "Lead");
        var leadLeft = _db.Student(school, "Lead", "Left");
        var noLead1 = _db.Student(school, "No", "Lead1");
        var noLead2 = _db.Student(school, "No", "Lead2");
        var exited = _db.Student(school, "Ex", "Ited", status: StudentStatus.Exited);
        _db.TeamMember(withLead, activeLead, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(leadLeft, goneLead, TeamRole.CaseManager, isLead: true);
        using (var seed = _db.Context())
        {
            var parent = new User { Email = "parent@x.com", PasswordHash = "x", FirstName = "Pat", LastName = "Parent", Role = UserRole.Parent };
            seed.Users.Add(parent);
            seed.SaveChanges();
            var child = new ChildProfile { UserId = parent.Id, FirstName = "Kid" };
            seed.ChildProfiles.Add(child);
            seed.SaveChanges();
            seed.ChildLinks.Add(new ChildLink { SchoolStudentId = withLead, IsActive = true, AcceptedAt = DateTime.UtcNow, ChildProfileId = child.Id });   // linked
            seed.ChildLinks.Add(new ChildLink { SchoolStudentId = noLead1, IsActive = true, AcceptedAt = null, InviteEmail = "p@x.com" });                // pending only
            seed.ChildLinks.Add(new ChildLink { SchoolStudentId = noLead2, IsActive = false, AcceptedAt = DateTime.UtcNow, ChildProfileId = child.Id }); // revoked
            seed.SaveChanges();
        }

        using var ctx = _db.Context();
        var svc = Service(ctx);
        var noCaseManager = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Attention = StudentAttention.NoCaseManager });
        var noCaseManagerPage2 = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Attention = StudentAttention.NoCaseManager, Page = 2, PageSize = 2 });
        var noParent = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Attention = StudentAttention.NoLinkedParent });
        var noParentSearch = await svc.SearchStudentsAsync(admin, new StudentSearchQuery { Attention = StudentAttention.NoLinkedParent, Query = "Lead1" });

        // Active students without an active lead whose profile is still active (a deactivated lead counts as missing).
        Assert.Equal(new[] { leadLeft, noLead1, noLead2 }, noCaseManager.Data!.Items.Select(s => s.Id).OrderBy(id => id));
        Assert.Equal(3, noCaseManager.Data.Total);
        Assert.Equal(3, noCaseManagerPage2.Data!.Total);
        Assert.Equal(leadLeft, Assert.Single(noCaseManagerPage2.Data.Items).Id); // ordered by last name: Lead1, Lead2 | Left
        // Active students without an accepted, active parent link (pending or revoked links do not count).
        Assert.Equal(new[] { leadLeft, noLead1, noLead2 }, noParent.Data!.Items.Select(s => s.Id).OrderBy(id => id));
        Assert.Equal(noLead1, Assert.Single(noParentSearch.Data!.Items).Id);
        Assert.DoesNotContain(exited, noParent.Data.Items.Select(s => s.Id));
    }

    // ----------------------------------------------------------------- Bulk case manager

    [Fact]
    public async Task AssignCaseManagerBulk_SetsLeadOnEachStudent_ReplacingPreviousLead_AndSkipsAlreadyAssigned()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var (oldLead, _) = _db.Staff("old@x.com", district, school, OrgRoleIds.Teacher);
        var (newLead, _) = _db.Staff("new@x.com", district, school, OrgRoleIds.Teacher);
        var s1 = _db.Student(school, "One");
        var s2 = _db.Student(school, "Two");
        var s3 = _db.Student(school, "Three");
        _db.TeamMember(s1, oldLead, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(s3, newLead, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var result = await Service(ctx).AssignCaseManagerBulkAsync(admin, new BulkAssignCaseManagerModel { StudentIds = new() { s1, s2, s3 }, UserId = newLead });

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Data!.Updated);
        using var check = _db.Context();
        foreach (var id in new[] { s1, s2, s3 })
        {
            Assert.Equal(newLead, check.SchoolStudents.Single(s => s.Id == id).CaseManagerUserId);
            var lead = Assert.Single(check.StudentTeamMembers.Where(m => m.SchoolStudentId == id && m.IsActive && m.IsLead));
            Assert.Equal(newLead, lead.UserId);
        }
        var demoted = check.StudentTeamMembers.Single(m => m.SchoolStudentId == s1 && m.UserId == oldLead);
        Assert.True(demoted.IsActive);
        Assert.False(demoted.IsLead);
        Assert.Equal(AccessRole.Owner, check.SchoolStudentAccesses.Single(a => a.SchoolStudentId == s2 && a.UserId == newLead).Role);
    }

    [Fact]
    public async Task AssignCaseManagerBulk_TeacherCaller_OrStudentOutOfScope_OrStaffAtOtherSchool_IsRefused()
    {
        var district = _db.District();
        var schoolA = _db.School(district, "A");
        var schoolB = _db.School(district, "B");
        var (teacher, _) = _db.Staff("t@x.com", district, schoolA, OrgRoleIds.Teacher);
        var (adminA, _) = _db.Staff("sa@x.com", district, schoolA, OrgRoleIds.SchoolAdmin);
        var (teacherB, _) = _db.Staff("tb@x.com", district, schoolB, OrgRoleIds.Teacher);
        var inA = _db.Student(schoolA);
        var inB = _db.Student(schoolB);

        using var ctx = _db.Context();
        var svc = Service(ctx);
        var byTeacher = await svc.AssignCaseManagerBulkAsync(teacher, new BulkAssignCaseManagerModel { StudentIds = new() { inA }, UserId = teacher });
        var outOfScope = await svc.AssignCaseManagerBulkAsync(adminA, new BulkAssignCaseManagerModel { StudentIds = new() { inA, inB }, UserId = teacher });
        var wrongSchool = await svc.AssignCaseManagerBulkAsync(adminA, new BulkAssignCaseManagerModel { StudentIds = new() { inA }, UserId = teacherB });

        Assert.Contains("permission", byTeacher.Message);
        Assert.Contains("permission", outOfScope.Message);
        Assert.Contains("not at this student's school", wrongSchool.Message);
        Assert.Empty(ctx.StudentTeamMembers);
    }

    [Fact]
    public async Task AssignCaseManagerBulk_IsSetBased_ForManyStudents_AndCapsTheSelection()
    {
        var district = _db.District();
        var school = _db.School(district, "Maple");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        var (oldLead, _) = _db.Staff("old@x.com", district, school, OrgRoleIds.Teacher);
        var (newLead, _) = _db.Staff("new@x.com", district, school, OrgRoleIds.Teacher);
        var ids = Enumerable.Range(1, 60).Select(i => _db.Student(school, "Kid", "N" + i)).ToList();
        foreach (var id in ids.Take(30))
            _db.TeamMember(id, oldLead, TeamRole.CaseManager, isLead: true);
        var counter = new DbActivityCounter();

        using var ctx = _db.Context(counter);
        var svc = Service(ctx);
        var result = await svc.AssignCaseManagerBulkAsync(admin, new BulkAssignCaseManagerModel { StudentIds = ids, UserId = newLead });
        var tooMany = await svc.AssignCaseManagerBulkAsync(admin, new BulkAssignCaseManagerModel { StudentIds = Enumerable.Range(1, 501).ToList(), UserId = newLead });
        var unknownId = await svc.AssignCaseManagerBulkAsync(admin, new BulkAssignCaseManagerModel { StudentIds = new() { ids[0], 999_999 }, UserId = newLead });

        Assert.True(result.Success, result.Message);
        Assert.Equal(60, result.Data!.Updated);
        Assert.True(counter.SaveChanges <= 2, $"SaveChanges = {counter.SaveChanges}"); // demotes, then deferred promotes
        Assert.True(counter.Queries < 10, $"Queries = {counter.Queries}");            // one scoped authz query + preloads, not per student
        Assert.Equal("Choose at most 500 students at a time.", tooMany.Message);
        Assert.Contains("permission", unknownId.Message);
        using var check = _db.Context();
        Assert.Equal(60, check.StudentTeamMembers.Count(m => m.UserId == newLead && m.IsActive && m.IsLead));
        Assert.Equal(30, check.StudentTeamMembers.Count(m => m.UserId == oldLead && m.IsActive && !m.IsLead));
        Assert.Equal(60, check.SchoolStudents.Count(s => s.CaseManagerUserId == newLead));
        Assert.Equal(60, _audit.Entries.Count(e => e.ResourceType == "SchoolStudent" && e.Action == AuditAction.Edit));
    }

    public void Dispose() => _db.Dispose();
}
