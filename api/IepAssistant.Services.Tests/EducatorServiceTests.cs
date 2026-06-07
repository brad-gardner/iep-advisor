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
/// P3b coverage: student creation under the educator's school with Owner access, and SchoolId-bound
/// scoping that rejects cross-school student access. Staff are provisioned by direct seeding
/// (District + School + StaffProfile) — self-serve onboarding was removed in P5. Uses a real SQLite
/// in-memory engine (same pattern as <see cref="AnalysisRunTestFixture"/>).
/// </summary>
public sealed class EducatorServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public EducatorServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private EducatorService CreateService(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), NullLogger<EducatorService>.Instance);

    private int SeedUser(string email)
    {
        using var ctx = CreateContext();
        var user = new User
        {
            Email = email,
            PasswordHash = "x",
            FirstName = "Ed",
            LastName = "Ucator",
            Role = UserRole.Educator
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    private int SeedDistrict(string name, string? stateCode = null)
    {
        using var ctx = CreateContext();
        var d = new District { Name = name, StateCode = stateCode };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedSchool(int districtId, string name, string? stateCode = null)
    {
        using var ctx = CreateContext();
        var s = new School { DistrictId = districtId, Name = name, StateCode = stateCode, IsActive = true };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private void SeedStaff(int userId, int districtId, int? schoolId, int orgRoleId = OrgRoleIds.Teacher)
    {
        using var ctx = CreateContext();
        ctx.StaffProfiles.Add(new StaffProfile
        {
            UserId = userId,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = orgRoleId,
            IsActive = true
        });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task CreateStudent_PlacesStudentInEducatorSchool_AndGrantsOwnerAccess()
    {
        var userId = SeedUser("teacher3@example.com");
        var districtId = SeedDistrict("Pine District", "OH");
        var schoolId = SeedSchool(districtId, "Pine Middle", "OH");
        SeedStaff(userId, districtId, schoolId);

        int studentId;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateStudentAsync(userId, new CreateSchoolStudentModel
            {
                FirstName = "Sam",
                LastName = "Student",
                GradeLevel = "5"
            });

            Assert.True(result.Success);
            Assert.Equal(schoolId, result.Data!.SchoolId);
            studentId = result.Data.Id;
        }

        using (var ctx = CreateContext())
        {
            var access = ctx.SchoolStudentAccesses.Single(a => a.SchoolStudentId == studentId);
            Assert.Equal(userId, access.UserId);
            Assert.Equal(AccessRole.Owner, access.Role);
            Assert.True(access.IsActive);
        }
    }

    [Fact]
    public async Task CreateStudent_WithoutStaffProfile_Fails()
    {
        var userId = SeedUser("notonboarded@example.com");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateStudentAsync(userId, new CreateSchoolStudentModel
        {
            FirstName = "Nope"
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetStudent_CrossSchool_ReturnsFailure_NotTheStudent()
    {
        var educatorA = SeedUser("a-school@example.com");
        var educatorB = SeedUser("b-school@example.com");

        // Educator A in district/school A.
        var districtA = SeedDistrict("District A", "OH");
        var schoolA = SeedSchool(districtA, "School A", "OH");
        SeedStaff(educatorA, districtA, schoolA);

        // Educator B in a different district/school.
        var districtB = SeedDistrict("District B", "OH");
        var schoolB = SeedSchool(districtB, "School B", "OH");
        SeedStaff(educatorB, districtB, schoolB);

        // Educator B creates a student in school B.
        int studentInB;
        using (var ctx = CreateContext())
        {
            var created = await CreateService(ctx).CreateStudentAsync(educatorB, new CreateSchoolStudentModel
            {
                FirstName = "Bee",
                LastName = "Student"
            });
            studentInB = created.Data!.Id;
        }

        // Educator A (school A) attempts to read a student in school B -> failure, no data.
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).GetStudentAsync(educatorA, studentInB);
            Assert.False(result.Success);
            Assert.Null(result.Data);
        }

        // Educator B can read their own student.
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).GetStudentAsync(educatorB, studentInB);
            Assert.True(result.Success);
            Assert.Equal(studentInB, result.Data!.Id);
        }
    }

    [Fact]
    public async Task GetStudents_ReturnsOnlyEducatorsOwnSchoolStudents()
    {
        var educatorA = SeedUser("list-a@example.com");
        var educatorB = SeedUser("list-b@example.com");

        var districtA = SeedDistrict("List District A", "OH");
        var schoolA = SeedSchool(districtA, "List School A", "OH");
        SeedStaff(educatorA, districtA, schoolA);

        var districtB = SeedDistrict("List District B", "OH");
        var schoolB = SeedSchool(districtB, "List School B", "OH");
        SeedStaff(educatorB, districtB, schoolB);

        using (var ctx = CreateContext())
        {
            await CreateService(ctx).CreateStudentAsync(educatorA, new CreateSchoolStudentModel { FirstName = "A1" });
            await CreateService(ctx).CreateStudentAsync(educatorA, new CreateSchoolStudentModel { FirstName = "A2" });
            await CreateService(ctx).CreateStudentAsync(educatorB, new CreateSchoolStudentModel { FirstName = "B1" });
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).GetStudentsAsync(educatorA);
            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Count);
            Assert.All(result.Data, s => Assert.StartsWith("A", s.FirstName));
        }
    }

    public void Dispose() => _connection.Dispose();
}
