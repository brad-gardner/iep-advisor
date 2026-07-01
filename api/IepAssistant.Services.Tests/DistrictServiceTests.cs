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

    private int SeedUser(string email)
    {
        using var ctx = CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FirstName = "F", LastName = "L", Role = UserRole.Educator };
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

    private int SeedStudent(int schoolId, bool isActive = true)
    {
        using var ctx = CreateContext();
        var s = new SchoolStudent { SchoolId = schoolId, FirstName = "Sam", IsActive = isActive };
        ctx.SchoolStudents.Add(s);
        ctx.SaveChanges();
        return s.Id;
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
