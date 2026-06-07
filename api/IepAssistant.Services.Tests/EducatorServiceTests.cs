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
/// P3b coverage: self-serve educator onboarding (find-or-create District/School, idempotent
/// StaffProfile, role flip), student creation under the educator's school with Owner access,
/// and SchoolId-bound scoping that rejects cross-school student access. Uses a real SQLite
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
            Role = UserRole.Parent
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    [Fact]
    public async Task Onboard_CreatesDistrictSchoolProfile_AndSetsEducatorRole()
    {
        var userId = SeedUser("teacher@example.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).OnboardAsync(userId, new OnboardEducatorModel
            {
                DistrictName = "Maple District",
                SchoolName = "Maple Elementary",
                StateCode = "OH"
            });

            Assert.True(result.Success);
            Assert.Equal("Maple District", result.Data!.DistrictName);
            Assert.Equal("Maple Elementary", result.Data.SchoolName);
        }

        using (var ctx = CreateContext())
        {
            Assert.Single(ctx.Districts);
            Assert.Single(ctx.Schools);
            Assert.Single(ctx.StaffProfiles);

            var user = ctx.Users.Single(u => u.Id == userId);
            Assert.Equal(UserRole.Educator, user.Role);
        }
    }

    [Fact]
    public async Task Onboard_CalledTwice_IsIdempotent()
    {
        var userId = SeedUser("teacher2@example.com");

        var model = new OnboardEducatorModel
        {
            DistrictName = "Oak District",
            SchoolName = "Oak High",
            StateCode = "OH"
        };

        using (var ctx = CreateContext())
        {
            var first = await CreateService(ctx).OnboardAsync(userId, model);
            Assert.True(first.Success);
        }

        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx).OnboardAsync(userId, model);
            Assert.True(second.Success);
        }

        using (var ctx = CreateContext())
        {
            // No duplicate district, school, or staff profile.
            Assert.Single(ctx.Districts);
            Assert.Single(ctx.Schools);
            Assert.Single(ctx.StaffProfiles);
        }
    }

    [Fact]
    public async Task CreateStudent_PlacesStudentInEducatorSchool_AndGrantsOwnerAccess()
    {
        var userId = SeedUser("teacher3@example.com");

        int schoolId;
        using (var ctx = CreateContext())
        {
            var profile = await CreateService(ctx).OnboardAsync(userId, new OnboardEducatorModel
            {
                DistrictName = "Pine District",
                SchoolName = "Pine Middle",
                StateCode = "OH"
            });
            schoolId = profile.Data!.SchoolId!.Value;
        }

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

        // Educator A in school A.
        using (var ctx = CreateContext())
        {
            await CreateService(ctx).OnboardAsync(educatorA, new OnboardEducatorModel
            {
                DistrictName = "District A",
                SchoolName = "School A",
                StateCode = "OH"
            });
        }

        // Educator B in a different district/school.
        using (var ctx = CreateContext())
        {
            await CreateService(ctx).OnboardAsync(educatorB, new OnboardEducatorModel
            {
                DistrictName = "District B",
                SchoolName = "School B",
                StateCode = "OH"
            });
        }

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

        using (var ctx = CreateContext())
        {
            await CreateService(ctx).OnboardAsync(educatorA, new OnboardEducatorModel
            {
                DistrictName = "List District A", SchoolName = "List School A", StateCode = "OH"
            });
            await CreateService(ctx).OnboardAsync(educatorB, new OnboardEducatorModel
            {
                DistrictName = "List District B", SchoolName = "List School B", StateCode = "OH"
            });
        }

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
