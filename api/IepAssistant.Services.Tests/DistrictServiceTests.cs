using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P3 coverage: district/school management. Reads (overview, school list) are open to any active
/// staff in the district; mutations (create/edit/deactivate school) are DistrictAdmin-only and
/// confined to the caller's own district. Deactivation is blocked while a school has active students
/// or active staff. Uses a real SQLite in-memory engine (same pattern as
/// <see cref="OrgAccessServiceTests"/>); the OrgRoles seed (HasData) is applied by EnsureCreated.
/// </summary>
public sealed class DistrictServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DistrictServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private DistrictService CreateService(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), NullLogger<DistrictService>.Instance);

    // ----------------------------------------------------------------- seed helpers

    private int SeedDistrict(string name, string? stateCode = null)
    {
        using var ctx = CreateContext();
        var d = new District { Name = name, StateCode = stateCode };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedSchool(int districtId, string name, string? stateCode = null, bool isActive = true)
    {
        using var ctx = CreateContext();
        var s = new School { DistrictId = districtId, Name = name, StateCode = stateCode, IsActive = isActive };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private int SeedUser(string email, UserRole role = UserRole.Educator)
    {
        using var ctx = CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FirstName = "F", LastName = "L", Role = role };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return u.Id;
    }

    private int SeedStaff(string email, int districtId, int? schoolId, int orgRoleId, bool isActive = true)
    {
        var userId = SeedUser(email);
        using var ctx = CreateContext();
        ctx.StaffProfiles.Add(new StaffProfile
        {
            UserId = userId,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = orgRoleId,
            IsActive = isActive
        });
        ctx.SaveChanges();
        return userId;
    }

    private int SeedStudent(int schoolId, bool isActive = true, string firstName = "Sam", string? lastName = null)
    {
        using var ctx = CreateContext();
        var s = new SchoolStudent { SchoolId = schoolId, FirstName = firstName, LastName = lastName, IsActive = isActive };
        ctx.SchoolStudents.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private void SeedAccess(int schoolStudentId, int granteeUserId, bool isActive = true)
    {
        using var ctx = CreateContext();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = schoolStudentId,
            UserId = granteeUserId,
            Role = AccessRole.Viewer,
            IsActive = isActive
        });
        ctx.SaveChanges();
    }

    private int SeedChildProfile(int parentUserId)
    {
        using var ctx = CreateContext();
        var p = new ChildProfile { UserId = parentUserId, FirstName = "Kid" };
        ctx.ChildProfiles.Add(p);
        ctx.SaveChanges();
        return p.Id;
    }

    private void SeedChildLink(int schoolStudentId, int? childProfileId = null, DateTime? acceptedAt = null, bool isActive = true)
    {
        using var ctx = CreateContext();
        ctx.ChildLinks.Add(new ChildLink
        {
            SchoolStudentId = schoolStudentId,
            ChildProfileId = childProfileId,
            AcceptedAt = acceptedAt,
            LinkedAt = acceptedAt,
            IsActive = isActive
        });
        ctx.SaveChanges();
    }

    private int SeedStaffInvite(int districtId, string email, int orgRoleId, int? schoolId, int invitedByUserId,
        DateTime? expiresAt = null, bool isActive = true, DateTime? acceptedAt = null)
    {
        using var ctx = CreateContext();
        var invite = new StaffInvite
        {
            Email = email,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = orgRoleId,
            InviteToken = acceptedAt == null ? "hash-" + email : null,
            InviteExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(7),
            AcceptedAt = acceptedAt,
            InvitedByUserId = invitedByUserId,
            IsActive = isActive
        };
        ctx.StaffInvites.Add(invite);
        ctx.SaveChanges();
        return invite.Id;
    }

    // ================================================================= GetOverview

    [Theory]
    [InlineData(OrgRoleIds.DistrictAdmin)]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task GetOverview_AnyActiveStaff_ReturnsCounts(int orgRoleId)
    {
        var districtId = SeedDistrict("Maple", "OH");
        var schoolA = SeedSchool(districtId, "A");
        SeedSchool(districtId, "B");
        SeedSchool(districtId, "Inactive", isActive: false); // excluded from active count

        int? callerSchool = orgRoleId == OrgRoleIds.DistrictAdmin ? null : schoolA;
        var userId = SeedStaff("caller@x.com", districtId, callerSchool, orgRoleId);
        // A second active staff member so the active-staff count is > 1.
        SeedStaff("teacher2@x.com", districtId, schoolA, OrgRoleIds.Teacher);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetOverviewAsync(userId);

        Assert.True(result.Success);
        Assert.Equal("Maple", result.Data!.Name);
        Assert.Equal("OH", result.Data.StateCode);
        Assert.Equal(2, result.Data.ActiveSchoolCount);
        Assert.Equal(2, result.Data.ActiveStaffCount);
    }

    [Fact]
    public async Task GetOverview_NoProfile_Fails()
    {
        var stranger = SeedUser("stranger@x.com");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetOverviewAsync(stranger);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetOverview_InactiveStaff_Fails()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var inactive = SeedStaff("x@x.com", districtId, schoolId, OrgRoleIds.DistrictAdmin, isActive: false);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetOverviewAsync(inactive);
        Assert.False(result.Success);
    }

    // ================================================================= GetDashboard

    [Fact]
    public async Task GetDashboard_DistrictAdmin_AggregatesDistrictWide()
    {
        var districtId = SeedDistrict("D");
        var schoolA = SeedSchool(districtId, "Apple");
        var schoolB = SeedSchool(districtId, "Banana");

        // Students: 2 active in A (inactive excluded from the per-school count), 1 active in B.
        var s1 = SeedStudent(schoolA, firstName: "Ann");
        var s2 = SeedStudent(schoolA, firstName: "Bob");
        SeedStudent(schoolA, isActive: false);
        var s3 = SeedStudent(schoolB, firstName: "Cal");

        // Staff: DA caller + one active teacher (active=2), one deactivated teacher (deactivated=1).
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t1@x.com", districtId, schoolA, OrgRoleIds.Teacher);
        SeedStaff("gone@x.com", districtId, schoolA, OrgRoleIds.Teacher, isActive: false);

        // Invites: pending + expired both listed (expired flagged); only the pending ROW counts as
        // invited; revoked and accepted invites are excluded everywhere.
        SeedStaffInvite(districtId, "pending@x.com", OrgRoleIds.Teacher, schoolA, da);
        SeedStaffInvite(districtId, "expired@x.com", OrgRoleIds.Teacher, schoolA, da, expiresAt: DateTime.UtcNow.AddDays(-1));
        SeedStaffInvite(districtId, "revoked@x.com", OrgRoleIds.Teacher, schoolA, da, isActive: false);
        SeedStaffInvite(districtId, "accepted@x.com", OrgRoleIds.Teacher, schoolA, da, acceptedAt: DateTime.UtcNow.AddDays(-2));

        // s1 is fully covered (active staff access + accepted parent link); s2 has a pending parent
        // invite and no staff; s3 has neither.
        SeedAccess(s1, teacher);
        var parent = SeedUser("parent@x.com", UserRole.Parent);
        var child = SeedChildProfile(parent);
        SeedChildLink(s1, child, acceptedAt: DateTime.UtcNow.AddDays(-3));
        SeedChildLink(s2);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(da);

        Assert.True(result.Success);
        var dash = result.Data!;

        Assert.Equal(2, dash.Schools.Count);
        Assert.Equal(2, dash.Schools.Single(s => s.Name == "Apple").ActiveStudentCount);
        Assert.Equal(1, dash.Schools.Single(s => s.Name == "Banana").ActiveStudentCount);

        Assert.Equal(2, dash.StaffSummary.ActiveCount);
        Assert.Equal(1, dash.StaffSummary.DeactivatedCount);
        Assert.Equal(1, dash.StaffSummary.InvitedCount); // pending rows only — expired not counted

        Assert.Equal(2, dash.InvitesNeedingAttention.Count);
        Assert.Equal("pending", dash.InvitesNeedingAttention.Single(i => i.Email == "pending@x.com").Status);
        Assert.Equal("expired", dash.InvitesNeedingAttention.Single(i => i.Email == "expired@x.com").Status);
        Assert.DoesNotContain(dash.InvitesNeedingAttention, i => i.Email == "revoked@x.com" || i.Email == "accepted@x.com");

        Assert.Equal(new[] { s2, s3 }, dash.StudentsWithoutStaff.Select(s => s.SchoolStudentId).OrderBy(id => id).ToArray());
        Assert.Equal("Apple", dash.StudentsWithoutStaff.Single(s => s.SchoolStudentId == s2).SchoolName);

        Assert.Equal(new[] { s2, s3 }, dash.StudentsWithoutParent.Select(s => s.SchoolStudentId).OrderBy(id => id).ToArray());
        Assert.True(dash.StudentsWithoutParent.Single(s => s.SchoolStudentId == s2).ParentInvitePending);
        Assert.False(dash.StudentsWithoutParent.Single(s => s.SchoolStudentId == s3).ParentInvitePending);
    }

    [Fact]
    public async Task GetDashboard_StudentWhoseOnlyGranteeWasDeactivated_AppearsInNoStaffList()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var activeTeacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var deactivatedTeacher = SeedStaff("gone@x.com", districtId, schoolId, OrgRoleIds.Teacher, isActive: false);

        var covered = SeedStudent(schoolId, firstName: "Covered");
        SeedAccess(covered, activeTeacher);

        // Access row still active, but the grantee's StaffProfile was deactivated → must appear.
        var orphaned = SeedStudent(schoolId, firstName: "Orphaned");
        SeedAccess(orphaned, deactivatedTeacher);

        // Only access row was revoked → must appear too.
        var revoked = SeedStudent(schoolId, firstName: "Revoked");
        SeedAccess(revoked, activeTeacher, isActive: false);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(da);

        Assert.True(result.Success);
        var ids = result.Data!.StudentsWithoutStaff.Select(s => s.SchoolStudentId).ToList();
        Assert.DoesNotContain(covered, ids);
        Assert.Contains(orphaned, ids);
        Assert.Contains(revoked, ids);
    }

    [Fact]
    public async Task GetDashboard_NoParentRows_DistinguishInvitePendingFromNeverInvited()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        var linked = SeedStudent(schoolId, firstName: "Linked");
        var parent = SeedUser("parent@x.com", UserRole.Parent);
        var child = SeedChildProfile(parent);
        SeedChildLink(linked, child, acceptedAt: DateTime.UtcNow.AddDays(-1));

        var invited = SeedStudent(schoolId, firstName: "Invited");
        SeedChildLink(invited); // active, un-accepted → invite pending

        var neverInvited = SeedStudent(schoolId, firstName: "Never");

        var revokedInvite = SeedStudent(schoolId, firstName: "RevokedInvite");
        SeedChildLink(revokedInvite, isActive: false); // revoked un-accepted link is NOT pending

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(da);

        Assert.True(result.Success);
        var rows = result.Data!.StudentsWithoutParent;
        Assert.DoesNotContain(rows, s => s.SchoolStudentId == linked);
        Assert.True(rows.Single(s => s.SchoolStudentId == invited).ParentInvitePending);
        Assert.False(rows.Single(s => s.SchoolStudentId == neverInvited).ParentInvitePending);
        Assert.False(rows.Single(s => s.SchoolStudentId == revokedInvite).ParentInvitePending);
    }

    [Fact]
    public async Task GetDashboard_SchoolAdmin_SeesOwnSchoolSlice_AndDistrictAdminInvitesExcluded()
    {
        var districtId = SeedDistrict("D");
        var schoolA = SeedSchool(districtId, "A");
        var schoolB = SeedSchool(districtId, "B");

        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var schoolAdmin = SeedStaff("sa@x.com", districtId, schoolA, OrgRoleIds.SchoolAdmin);
        SeedStaff("ta@x.com", districtId, schoolA, OrgRoleIds.Teacher);
        SeedStaff("tb@x.com", districtId, schoolB, OrgRoleIds.Teacher);
        SeedStaff("goneA@x.com", districtId, schoolA, OrgRoleIds.Teacher, isActive: false);

        var studentA = SeedStudent(schoolA, firstName: "InA");
        SeedStudent(schoolB, firstName: "InB");

        SeedStaffInvite(districtId, "toA@x.com", OrgRoleIds.Teacher, schoolA, da);
        SeedStaffInvite(districtId, "toB@x.com", OrgRoleIds.Teacher, schoolB, da);
        SeedStaffInvite(districtId, "da-invite@x.com", OrgRoleIds.DistrictAdmin, null, da); // SchoolId == null

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).GetDashboardAsync(schoolAdmin);
            Assert.True(result.Success);
            var dash = result.Data!;

            var school = Assert.Single(dash.Schools);
            Assert.Equal("A", school.Name);
            Assert.Equal(1, school.ActiveStudentCount);

            // Own-school staff only: the SchoolAdmin caller + active teacher A; DA (school-null) hidden.
            Assert.Equal(2, dash.StaffSummary.ActiveCount);
            Assert.Equal(1, dash.StaffSummary.DeactivatedCount);
            Assert.Equal(1, dash.StaffSummary.InvitedCount);

            var invite = Assert.Single(dash.InvitesNeedingAttention);
            Assert.Equal("toA@x.com", invite.Email);

            Assert.Equal(studentA, Assert.Single(dash.StudentsWithoutStaff).SchoolStudentId);
            Assert.Equal(studentA, Assert.Single(dash.StudentsWithoutParent).SchoolStudentId);
        }

        // DistrictAdmin sees everything, INCLUDING the school-null district-admin invite.
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).GetDashboardAsync(da);
            Assert.True(result.Success);
            Assert.Equal(3, result.Data!.InvitesNeedingAttention.Count);
            Assert.Contains(result.Data.InvitesNeedingAttention, i => i.Email == "da-invite@x.com" && i.SchoolId == null);
            Assert.Equal(3, result.Data.StaffSummary.InvitedCount);
            Assert.Equal(2, result.Data.Schools.Count);
        }
    }

    [Fact]
    public async Task GetDashboard_ExcludesInactiveSchoolsInactiveStudentsAndOtherDistricts()
    {
        var districtId = SeedDistrict("D");
        var openSchool = SeedSchool(districtId, "Open");
        var closedSchool = SeedSchool(districtId, "Closed", isActive: false);
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        SeedStudent(closedSchool, firstName: "Ghost"); // active student in an inactive school — excluded
        SeedStudent(openSchool, isActive: false, firstName: "Gone"); // inactive student — excluded
        var visible = SeedStudent(openSchool, firstName: "Here");

        // Rows bound to the inactive school are excluded from the staff summary and invites list too.
        SeedStaff("closed-staff@x.com", districtId, closedSchool, OrgRoleIds.Teacher, isActive: false);
        SeedStaffInvite(districtId, "closed-invite@x.com", OrgRoleIds.Teacher, closedSchool, da);

        // Foreign district must never bleed in.
        var otherDistrict = SeedDistrict("Other");
        var foreignSchool = SeedSchool(otherDistrict, "Foreign");
        SeedStudent(foreignSchool);
        var foreignDa = SeedStaff("fda@x.com", otherDistrict, null, OrgRoleIds.DistrictAdmin);
        SeedStaffInvite(otherDistrict, "foreign@x.com", OrgRoleIds.Teacher, foreignSchool, foreignDa);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(da);

        Assert.True(result.Success);
        var dash = result.Data!;

        var school = Assert.Single(dash.Schools);
        Assert.Equal("Open", school.Name);
        Assert.Equal(1, school.ActiveStudentCount);

        Assert.Equal(visible, Assert.Single(dash.StudentsWithoutStaff).SchoolStudentId);
        Assert.Equal(visible, Assert.Single(dash.StudentsWithoutParent).SchoolStudentId);
        Assert.Empty(dash.InvitesNeedingAttention); // foreign + closed-school invites both excluded
        Assert.Equal(0, dash.StaffSummary.InvitedCount);
        Assert.Equal(1, dash.StaffSummary.ActiveCount); // caller only
        Assert.Equal(0, dash.StaffSummary.DeactivatedCount); // closed-school staff excluded
    }

    [Fact]
    public async Task GetDashboard_GranteeActiveInAnotherDistrictOnly_StudentStillAppearsInNoStaffList()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        var otherDistrict = SeedDistrict("Other");
        var otherSchool = SeedSchool(otherDistrict, "Elsewhere");

        // Grantee is deactivated in THIS district but still holds an active profile elsewhere —
        // the cross-district profile must not mask the local deactivation.
        var grantee = SeedStaff("moved@x.com", districtId, schoolId, OrgRoleIds.Teacher, isActive: false);
        using (var seed = CreateContext())
        {
            seed.StaffProfiles.Add(new StaffProfile
            {
                UserId = grantee,
                DistrictId = otherDistrict,
                SchoolId = otherSchool,
                OrgRoleId = OrgRoleIds.Teacher,
                IsActive = true
            });
            seed.SaveChanges();
        }

        var student = SeedStudent(schoolId, firstName: "Orphaned");
        SeedAccess(student, grantee);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(da);

        Assert.True(result.Success);
        Assert.Equal(student, Assert.Single(result.Data!.StudentsWithoutStaff).SchoolStudentId);
    }

    [Fact]
    public async Task GetDashboard_SchoolAdminWithoutSchoolBinding_ReturnsEmptyPayload()
    {
        // Shouldn't exist per the invite invariants, but must not 500 or leak district-wide data —
        // mirrors EducatorService.GetStudentsAsync, which returns an empty roster for this case.
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        SeedStudent(schoolId);
        var admin = SeedStaff("sa@x.com", districtId, null, OrgRoleIds.SchoolAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(admin);

        Assert.True(result.Success);
        var dash = result.Data!;
        Assert.Empty(dash.Schools);
        Assert.Equal(0, dash.StaffSummary.ActiveCount);
        Assert.Equal(0, dash.StaffSummary.InvitedCount);
        Assert.Empty(dash.InvitesNeedingAttention);
        Assert.Empty(dash.StudentsWithoutStaff);
        Assert.Empty(dash.StudentsWithoutParent);
    }

    [Fact]
    public async Task GetDashboard_EmptyDistrict_ReturnsValidZeroPayload()
    {
        var districtId = SeedDistrict("Empty");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(da);

        Assert.True(result.Success);
        var dash = result.Data!;
        Assert.Empty(dash.Schools);
        Assert.Equal(1, dash.StaffSummary.ActiveCount); // the caller's own profile
        Assert.Equal(0, dash.StaffSummary.DeactivatedCount);
        Assert.Equal(0, dash.StaffSummary.InvitedCount);
        Assert.Empty(dash.InvitesNeedingAttention);
        Assert.Empty(dash.StudentsWithoutStaff);
        Assert.Empty(dash.StudentsWithoutParent);
    }

    [Fact]
    public async Task GetDashboard_Teacher_Denied()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(teacher);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDashboard_ParentUser_NoStaffProfile_Fails()
    {
        // Parents (and students) have no StaffProfile → null staff context → failure result,
        // which the controller maps to a non-200 (existing MapFailure pattern).
        var parent = SeedUser("parent@x.com", UserRole.Parent);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetDashboardAsync(parent);

        Assert.False(result.Success);
    }

    // ================================================================= GetSchools

    [Theory]
    [InlineData(OrgRoleIds.DistrictAdmin)]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task GetSchools_AnyActiveStaff_ListsActiveDistrictSchoolsWithCounts(int orgRoleId)
    {
        var districtId = SeedDistrict("D");
        var schoolA = SeedSchool(districtId, "Apple");
        var schoolB = SeedSchool(districtId, "Banana");
        SeedSchool(districtId, "Closed", isActive: false); // excluded
        // Other district school must never appear.
        var otherDistrict = SeedDistrict("Other");
        SeedSchool(otherDistrict, "Foreign");

        SeedStudent(schoolA);
        SeedStudent(schoolA);
        SeedStudent(schoolA, isActive: false); // not counted
        SeedStaff("staffA@x.com", districtId, schoolA, OrgRoleIds.Teacher);

        int? callerSchool = orgRoleId == OrgRoleIds.DistrictAdmin ? null : schoolB;
        var caller = SeedStaff("caller@x.com", districtId, callerSchool, orgRoleId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetSchoolsAsync(caller);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count); // active only, this district only
        var apple = result.Data.Single(s => s.Name == "Apple");
        Assert.Equal(2, apple.ActiveStudentCount);
        Assert.Equal(1, apple.ActiveStaffCount);
        Assert.DoesNotContain(result.Data, s => s.Name == "Foreign" || s.Name == "Closed");
    }

    [Fact]
    public async Task GetSchools_NoProfile_Fails()
    {
        var stranger = SeedUser("stranger@x.com");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetSchoolsAsync(stranger);
        Assert.False(result.Success);
    }

    // ================================================================= CreateSchool

    [Fact]
    public async Task CreateSchool_DistrictAdmin_Succeeds()
    {
        var districtId = SeedDistrict("D", "TX");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateSchoolAsync(da, new CreateSchoolModel
            {
                Name = "  New School  ",
                StateCode = "ca"
            });
            Assert.True(result.Success);
            Assert.Equal("New School", result.Data!.Name);
            Assert.Equal("CA", result.Data.StateCode);
        }

        using (var ctx = CreateContext())
        {
            var school = ctx.Schools.Single(s => s.Name == "New School");
            Assert.Equal(districtId, school.DistrictId);
            Assert.True(school.IsActive);
        }
    }

    [Fact]
    public async Task CreateSchool_NoStateCode_InheritsFromDistrict()
    {
        var districtId = SeedDistrict("D", "OH");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateSchoolAsync(da, new CreateSchoolModel { Name = "Inherit" });

        Assert.True(result.Success);
        Assert.Equal("OH", result.Data!.StateCode);
    }

    [Theory]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task CreateSchool_NonDistrictAdmin_Denied(int orgRoleId)
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var caller = SeedStaff("caller@x.com", districtId, schoolId, orgRoleId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateSchoolAsync(caller, new CreateSchoolModel { Name = "Nope" });

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.DoesNotContain(verify.Schools, s => s.Name == "Nope");
    }

    [Fact]
    public async Task CreateSchool_BlankName_Fails()
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateSchoolAsync(da, new CreateSchoolModel { Name = "   " });
        Assert.False(result.Success);
    }

    // ================================================================= UpdateSchool

    [Fact]
    public async Task UpdateSchool_DistrictAdmin_EditsNameAndState()
    {
        var districtId = SeedDistrict("D", "OH");
        var schoolId = SeedSchool(districtId, "Old", "OH");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).UpdateSchoolAsync(da, schoolId, new UpdateSchoolModel
            {
                Name = "Renamed",
                StateCode = "ny"
            });
            Assert.True(result.Success);
            Assert.Equal("Renamed", result.Data!.Name);
            Assert.Equal("NY", result.Data.StateCode);
        }

        using (var ctx = CreateContext())
        {
            var school = ctx.Schools.Single(s => s.Id == schoolId);
            Assert.Equal("Renamed", school.Name);
            Assert.Equal("NY", school.StateCode);
        }
    }

    [Theory]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task UpdateSchool_NonDistrictAdmin_Denied(int orgRoleId)
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var caller = SeedStaff("caller@x.com", districtId, schoolId, orgRoleId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).UpdateSchoolAsync(caller, schoolId, new UpdateSchoolModel { Name = "X" });

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateSchool_CrossDistrict_NotFound()
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var otherDistrict = SeedDistrict("Other");
        var foreignSchool = SeedSchool(otherDistrict, "Foreign");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).UpdateSchoolAsync(da, foreignSchool, new UpdateSchoolModel { Name = "Hijack" });

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.Equal("Foreign", verify.Schools.Single(s => s.Id == foreignSchool).Name);
    }

    // ================================================================= DeactivateSchool

    [Fact]
    public async Task DeactivateSchool_Empty_Succeeds()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "Empty");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).DeactivateSchoolAsync(da, schoolId);
            Assert.True(result.Success);
        }

        using (var ctx = CreateContext())
            Assert.False(ctx.Schools.Single(s => s.Id == schoolId).IsActive);
    }

    [Fact]
    public async Task DeactivateSchool_WithActiveStudents_Blocked()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "Busy");
        SeedStudent(schoolId); // active
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeactivateSchoolAsync(da, schoolId);

        Assert.False(result.Success);
        Assert.Contains("student", result.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.True(verify.Schools.Single(s => s.Id == schoolId).IsActive);
    }

    [Fact]
    public async Task DeactivateSchool_WithActiveStaff_Blocked()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "Staffed");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedStaff("teacher@x.com", districtId, schoolId, OrgRoleIds.Teacher); // active staff bound to the school

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeactivateSchoolAsync(da, schoolId);

        Assert.False(result.Success);
        Assert.Contains("staff", result.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.True(verify.Schools.Single(s => s.Id == schoolId).IsActive);
    }

    [Fact]
    public async Task DeactivateSchool_WithOnlyInactiveOccupants_Succeeds()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "Quiet");
        SeedStudent(schoolId, isActive: false);
        SeedStaff("former@x.com", districtId, schoolId, OrgRoleIds.Teacher, isActive: false);
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeactivateSchoolAsync(da, schoolId);
        Assert.True(result.Success);
    }

    [Theory]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task DeactivateSchool_NonDistrictAdmin_Denied(int orgRoleId)
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var caller = SeedStaff("caller@x.com", districtId, schoolId, orgRoleId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeactivateSchoolAsync(caller, schoolId);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.True(verify.Schools.Single(s => s.Id == schoolId).IsActive);
    }

    [Fact]
    public async Task DeactivateSchool_CrossDistrict_NotFound()
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var otherDistrict = SeedDistrict("Other");
        var foreignSchool = SeedSchool(otherDistrict, "Foreign");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeactivateSchoolAsync(da, foreignSchool);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        using var verify = CreateContext();
        Assert.True(verify.Schools.Single(s => s.Id == foreignSchool).IsActive);
    }

    public void Dispose() => _connection.Dispose();
}
