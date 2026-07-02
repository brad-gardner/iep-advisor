using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P1 coverage for <see cref="OrgAccessService"/> — the centralized DB-backed org authorization that
/// replaces the five duplicated SchoolId checks. Exercises the full matrix:
/// DistrictAdmin / SchoolAdmin / Teacher × { own school, other school same district, other-district
/// school, student access at each AccessRole tier vs minRole, student with no grant, inactive profile }.
///
/// Authorization shape under test (player-coach):
///  - <see cref="OrgAccessService.GetStaffContextAsync"/> returns null for a missing/inactive profile.
///  - <see cref="OrgAccessService.CanActOnSchoolAsync"/>: DistrictAdmin = any school in district;
///    SchoolAdmin/Teacher = own school only.
///  - <see cref="OrgAccessService.CanActOnStudentAsync"/>: District/School admins pass within scope with
///    NO SchoolStudentAccess row; Teachers need an active grant with Role &gt;= minRole.
///
/// Regression guard (the P6a-era bug): AccessRole is persisted as a string, so a SQL-side
/// `Role &gt;= minRole` would compare alphabetically. The Collaborator-vs-Owner-vs-Viewer threshold
/// tests below would fail if the service ever reintroduced a SQL-side enum comparison.
///
/// Real SQLite in-memory engine (same pattern as <see cref="StudentInviteServiceTests"/>); the OrgRoles
/// lookup rows are applied by EnsureCreated via HasData.
/// </summary>
public sealed class OrgAccessServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public OrgAccessServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();

        // Sanity: the OrgRoles seed (HasData) must be present, otherwise the FK on StaffProfile.OrgRoleId
        // would silently break every test below.
        Assert.Equal(3, ctx.OrgRoles.Count());
    }

    private ApplicationDbContext CreateContext() => new(_options);
    private OrgAccessService CreateService(ApplicationDbContext ctx) => new(ctx);

    // ----------------------------------------------------------------- seed helpers

    private int SeedDistrict(string name)
    {
        using var ctx = CreateContext();
        var d = new District { Name = name };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedSchool(int districtId, string name)
    {
        using var ctx = CreateContext();
        var s = new School { DistrictId = districtId, Name = name };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private int SeedUser(string email)
    {
        using var ctx = CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FirstName = "F", LastName = "L", Role = UserRole.Educator };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return u.Id;
    }

    /// <summary>Seeds a staff member. <paramref name="schoolId"/> null = DistrictAdmin not school-bound.</summary>
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

    private int SeedStudent(int schoolId, string first = "Sam")
    {
        using var ctx = CreateContext();
        var s = new SchoolStudent { SchoolId = schoolId, FirstName = first, IsActive = true };
        ctx.SchoolStudents.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private void SeedGrant(int schoolStudentId, int userId, AccessRole role, bool isActive = true)
    {
        using var ctx = CreateContext();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = schoolStudentId,
            UserId = userId,
            Role = role,
            IsActive = isActive
        });
        ctx.SaveChanges();
    }

    // ================================================================= GetStaffContext

    [Fact]
    public async Task GetStaffContext_ActiveProfile_ReturnsContext()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var userId = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetStaffContextAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(userId, result!.UserId);
        Assert.Equal(districtId, result.DistrictId);
        Assert.Equal(schoolId, result.SchoolId);
        Assert.Equal(OrgRoleIds.Teacher, result.OrgRoleId);
    }

    [Fact]
    public async Task GetStaffContext_DistrictAdmin_HasNullSchool()
    {
        var districtId = SeedDistrict("D");
        var userId = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetStaffContextAsync(userId);

        Assert.NotNull(result);
        Assert.Null(result!.SchoolId);
        Assert.Equal(OrgRoleIds.DistrictAdmin, result.OrgRoleId);
    }

    [Fact]
    public async Task GetStaffContext_InactiveProfile_ReturnsNull()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var userId = SeedStaff("inactive@x.com", districtId, schoolId, OrgRoleIds.Teacher, isActive: false);

        using var ctx = CreateContext();
        Assert.Null(await CreateService(ctx).GetStaffContextAsync(userId));
    }

    [Fact]
    public async Task GetStaffContext_NoProfile_ReturnsNull()
    {
        var userId = SeedUser("noprofile@x.com");
        using var ctx = CreateContext();
        Assert.Null(await CreateService(ctx).GetStaffContextAsync(userId));
    }

    // ================================================================= CanActOnSchool

    [Fact]
    public async Task CanActOnSchool_DistrictAdmin_OwnDistrictSchool_True()
    {
        var districtId = SeedDistrict("D");
        var schoolA = SeedSchool(districtId, "A");
        var schoolB = SeedSchool(districtId, "B");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        Assert.True(await svc.CanActOnSchoolAsync(da, schoolA));
        Assert.True(await svc.CanActOnSchoolAsync(da, schoolB)); // any school in their district
    }

    [Fact]
    public async Task CanActOnSchool_DistrictAdmin_OtherDistrictSchool_False()
    {
        var districtId = SeedDistrict("D");
        var otherDistrictId = SeedDistrict("Other");
        var foreignSchool = SeedSchool(otherDistrictId, "Foreign");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnSchoolAsync(da, foreignSchool));
    }

    [Fact]
    public async Task CanActOnSchool_SchoolAdmin_OwnSchool_True_OtherSameDistrict_False()
    {
        var districtId = SeedDistrict("D");
        var ownSchool = SeedSchool(districtId, "Own");
        var siblingSchool = SeedSchool(districtId, "Sibling");
        var sa = SeedStaff("sa@x.com", districtId, ownSchool, OrgRoleIds.SchoolAdmin);

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        Assert.True(await svc.CanActOnSchoolAsync(sa, ownSchool));
        // SchoolAdmin is scoped to their own school only — NOT district-wide like a DistrictAdmin.
        Assert.False(await svc.CanActOnSchoolAsync(sa, siblingSchool));
    }

    [Fact]
    public async Task CanActOnSchool_Teacher_OwnSchool_True_OtherSchool_False()
    {
        var districtId = SeedDistrict("D");
        var ownSchool = SeedSchool(districtId, "Own");
        var otherSchool = SeedSchool(districtId, "Other");
        var teacher = SeedStaff("t@x.com", districtId, ownSchool, OrgRoleIds.Teacher);

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        Assert.True(await svc.CanActOnSchoolAsync(teacher, ownSchool));
        Assert.False(await svc.CanActOnSchoolAsync(teacher, otherSchool));
    }

    [Fact]
    public async Task CanActOnSchool_InactiveStaff_False()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var inactive = SeedStaff("x@x.com", districtId, schoolId, OrgRoleIds.DistrictAdmin, isActive: false);

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnSchoolAsync(inactive, schoolId));
    }

    [Fact]
    public async Task CanActOnSchool_NoProfile_False()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var stranger = SeedUser("stranger@x.com");

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnSchoolAsync(stranger, schoolId));
    }

    // ================================================================= CanActOnStudent (admins)

    [Fact]
    public async Task CanActOnStudent_DistrictAdmin_DistrictStudent_True_WithoutGrant()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var studentId = SeedStudent(schoolId);
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        // Player-coach: no SchoolStudentAccess row required; passes at the strictest tier.
        Assert.True(await CreateService(ctx).CanActOnStudentAsync(da, studentId, AccessRole.Owner));
    }

    [Fact]
    public async Task CanActOnStudent_DistrictAdmin_OtherDistrictStudent_False()
    {
        var districtId = SeedDistrict("D");
        var otherDistrictId = SeedDistrict("Other");
        var foreignSchool = SeedSchool(otherDistrictId, "Foreign");
        var foreignStudent = SeedStudent(foreignSchool);
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(da, foreignStudent, AccessRole.Viewer));
    }

    [Fact]
    public async Task CanActOnStudent_SchoolAdmin_OwnSchoolStudent_True_WithoutGrant()
    {
        var districtId = SeedDistrict("D");
        var ownSchool = SeedSchool(districtId, "Own");
        var studentId = SeedStudent(ownSchool);
        var sa = SeedStaff("sa@x.com", districtId, ownSchool, OrgRoleIds.SchoolAdmin);

        using var ctx = CreateContext();
        Assert.True(await CreateService(ctx).CanActOnStudentAsync(sa, studentId, AccessRole.Owner));
    }

    [Fact]
    public async Task CanActOnStudent_SchoolAdmin_SiblingSchoolStudent_False()
    {
        var districtId = SeedDistrict("D");
        var ownSchool = SeedSchool(districtId, "Own");
        var siblingSchool = SeedSchool(districtId, "Sibling");
        var siblingStudent = SeedStudent(siblingSchool);
        var sa = SeedStaff("sa@x.com", districtId, ownSchool, OrgRoleIds.SchoolAdmin);

        using var ctx = CreateContext();
        // SchoolAdmin's player-coach superset is bounded to their own school, not the whole district.
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(sa, siblingStudent, AccessRole.Viewer));
    }

    // ================================================================= CanActOnStudent (teacher tiers)

    [Theory]
    [InlineData(AccessRole.Viewer, AccessRole.Viewer, true)]
    [InlineData(AccessRole.Collaborator, AccessRole.Viewer, true)]
    [InlineData(AccessRole.Owner, AccessRole.Viewer, true)]
    [InlineData(AccessRole.Viewer, AccessRole.Collaborator, false)]
    [InlineData(AccessRole.Collaborator, AccessRole.Collaborator, true)]
    [InlineData(AccessRole.Owner, AccessRole.Collaborator, true)]
    [InlineData(AccessRole.Viewer, AccessRole.Owner, false)]
    [InlineData(AccessRole.Collaborator, AccessRole.Owner, false)]
    [InlineData(AccessRole.Owner, AccessRole.Owner, true)]
    public async Task CanActOnStudent_Teacher_GrantTierVsMinRole(AccessRole grant, AccessRole minRole, bool expected)
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var studentId = SeedStudent(schoolId);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        SeedGrant(studentId, teacher, grant);

        using var ctx = CreateContext();
        // Regression guard: a SQL-side string comparison would mis-rank these (e.g. "Collaborator" < "Viewer").
        Assert.Equal(expected, await CreateService(ctx).CanActOnStudentAsync(teacher, studentId, minRole));
    }

    [Fact]
    public async Task CanActOnStudent_Teacher_NoGrant_False()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var studentId = SeedStudent(schoolId);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        using var ctx = CreateContext();
        // Teacher in the right school but with NO access row → denied even at the lowest tier.
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(teacher, studentId, AccessRole.Viewer));
    }

    [Fact]
    public async Task CanActOnStudent_Teacher_InactiveGrant_False()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var studentId = SeedStudent(schoolId);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        SeedGrant(studentId, teacher, AccessRole.Owner, isActive: false);

        using var ctx = CreateContext();
        // A deactivated grant must not count (reflects revoke immediately).
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(teacher, studentId, AccessRole.Viewer));
    }

    [Fact]
    public async Task CanActOnStudent_Teacher_GrantButOtherSchoolStudent_False()
    {
        var districtId = SeedDistrict("D");
        var ownSchool = SeedSchool(districtId, "Own");
        var otherSchool = SeedSchool(districtId, "Other");
        var otherStudent = SeedStudent(otherSchool);
        var teacher = SeedStaff("t@x.com", districtId, ownSchool, OrgRoleIds.Teacher);
        // Even WITH an Owner grant, a teacher cannot act on a student outside their own school.
        SeedGrant(otherStudent, teacher, AccessRole.Owner);

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(teacher, otherStudent, AccessRole.Viewer));
    }

    [Fact]
    public async Task CanActOnStudent_InactiveAdmin_False()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var studentId = SeedStudent(schoolId);
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin, isActive: false);

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(da, studentId, AccessRole.Viewer));
    }

    [Fact]
    public async Task CanActOnStudent_NonexistentStudent_False()
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        Assert.False(await CreateService(ctx).CanActOnStudentAsync(da, schoolStudentId: 999999, AccessRole.Viewer));
    }

    public void Dispose() => _connection.Dispose();
}
