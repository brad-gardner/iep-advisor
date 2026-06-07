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

    private int SeedStaff(int userId, int districtId, int? schoolId, int orgRoleId = OrgRoleIds.Teacher)
    {
        using var ctx = CreateContext();
        var p = new StaffProfile
        {
            UserId = userId,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = orgRoleId,
            IsActive = true
        };
        ctx.StaffProfiles.Add(p);
        ctx.SaveChanges();
        return p.Id;
    }

    /// <summary>Seeds an active student directly in a school; returns the studentId.</summary>
    private int SeedStudent(int schoolId, string firstName = "Stu", string lastName = "Dent")
    {
        using var ctx = CreateContext();
        var s = new SchoolStudent { SchoolId = schoolId, FirstName = firstName, LastName = lastName, IsActive = true };
        ctx.SchoolStudents.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    /// <summary>Grants a user an active SchoolStudentAccess directly (bypasses the service).</summary>
    private void SeedAccess(int studentId, int userId, AccessRole role = AccessRole.Collaborator, bool isActive = true)
    {
        using var ctx = CreateContext();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = studentId,
            UserId = userId,
            Role = role,
            IsActive = isActive
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

    // ================================================================= Roster matrix (list authz == detail authz)

    [Fact]
    public async Task GetStudents_Teacher_SeesOnlyGrantedStudents_IncludingCreatedByThem()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var teacher = SeedUser("t@x.com");
        SeedStaff(teacher, district, school, OrgRoleIds.Teacher);

        // One student the teacher creates (gets an Owner grant), one in the same school they have no grant on.
        int created;
        using (var ctx = CreateContext())
            created = (await CreateService(ctx).CreateStudentAsync(teacher, new CreateSchoolStudentModel { FirstName = "Mine" })).Data!.Id;
        var ungranted = SeedStudent(school, "NotMine");

        using (var ctx = CreateContext())
        {
            var list = await CreateService(ctx).GetStudentsAsync(teacher);
            Assert.True(list.Success);
            Assert.Single(list.Data!);
            Assert.Equal(created, list.Data![0].Id);
        }

        // Detail authz mirrors list authz exactly.
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).GetStudentAsync(teacher, created)).Success);
        using (var ctx = CreateContext())
            Assert.False((await CreateService(ctx).GetStudentAsync(teacher, ungranted)).Success);
    }

    [Fact]
    public async Task GetStudents_SchoolAdmin_SeesWholeSchool_NotOtherSchools()
    {
        var district = SeedDistrict("D", "OH");
        var mine = SeedSchool(district, "Mine", "OH");
        var other = SeedSchool(district, "Other", "OH");
        var admin = SeedUser("sa@x.com");
        SeedStaff(admin, district, mine, OrgRoleIds.SchoolAdmin);

        var a = SeedStudent(mine, "A");
        var b = SeedStudent(mine, "B");
        var elsewhere = SeedStudent(other, "Z");

        using (var ctx = CreateContext())
        {
            var list = await CreateService(ctx).GetStudentsAsync(admin);
            Assert.Equal(2, list.Data!.Count);
            Assert.All(list.Data, s => Assert.Equal(mine, s.SchoolId));
        }

        // Detail authz == list authz: own-school students open (no grant needed), other-school denied.
        using (var ctx = CreateContext()) Assert.True((await CreateService(ctx).GetStudentAsync(admin, a)).Success);
        using (var ctx = CreateContext()) Assert.True((await CreateService(ctx).GetStudentAsync(admin, b)).Success);
        using (var ctx = CreateContext()) Assert.False((await CreateService(ctx).GetStudentAsync(admin, elsewhere)).Success);
    }

    [Fact]
    public async Task GetStudents_DistrictAdmin_SeesWholeDistrict_AcrossActiveSchools_WithSchoolNames()
    {
        var district = SeedDistrict("D", "OH");
        var s1 = SeedSchool(district, "School One", "OH");
        var s2 = SeedSchool(district, "School Two", "OH");
        var admin = SeedUser("da@x.com");
        SeedStaff(admin, district, null, OrgRoleIds.DistrictAdmin);

        var stu1 = SeedStudent(s1, "One");
        var stu2 = SeedStudent(s2, "Two");

        using var ctx = CreateContext();
        var list = await CreateService(ctx).GetStudentsAsync(admin);
        Assert.True(list.Success);
        Assert.Equal(2, list.Data!.Count);
        // School names are populated so the UI can group/filter.
        Assert.Contains(list.Data!, s => s.Id == stu1 && s.SchoolName == "School One");
        Assert.Contains(list.Data!, s => s.Id == stu2 && s.SchoolName == "School Two");
    }

    [Fact]
    public async Task GetStudents_DistrictAdmin_DoesNotSeeOtherDistricts_NorInactiveSchools()
    {
        var district = SeedDistrict("Mine", "OH");
        var activeSchool = SeedSchool(district, "Active", "OH");
        var admin = SeedUser("da@x.com");
        SeedStaff(admin, district, null, OrgRoleIds.DistrictAdmin);
        var visible = SeedStudent(activeSchool, "Visible");

        // An inactive school in the same district — its students must be hidden.
        int inactiveSchool;
        using (var ctx = CreateContext())
        {
            var s = new School { DistrictId = district, Name = "Closed", StateCode = "OH", IsActive = false };
            ctx.Schools.Add(s); ctx.SaveChanges(); inactiveSchool = s.Id;
        }
        var hiddenByInactiveSchool = SeedStudent(inactiveSchool, "Closed");

        // Another district entirely.
        var otherDistrict = SeedDistrict("Other", "OH");
        var otherSchool = SeedSchool(otherDistrict, "Far", "OH");
        var hiddenByDistrict = SeedStudent(otherSchool, "Far");

        using var ctx2 = CreateContext();
        var list = await CreateService(ctx2).GetStudentsAsync(admin);
        Assert.Single(list.Data!);
        Assert.Equal(visible, list.Data![0].Id);

        // Detail authz parity: the in-district active-school student opens; the others do not.
        using (var c = CreateContext()) Assert.True((await CreateService(c).GetStudentAsync(admin, visible)).Success);
        using (var c = CreateContext()) Assert.False((await CreateService(c).GetStudentAsync(admin, hiddenByInactiveSchool)).Success);
        using (var c = CreateContext()) Assert.False((await CreateService(c).GetStudentAsync(admin, hiddenByDistrict)).Success);
    }

    // ================================================================= CreateStudent: schoolId resolution

    [Fact]
    public async Task CreateStudent_DistrictAdmin_WithoutSchoolId_FailsCleanly()
    {
        var district = SeedDistrict("D", "OH");
        SeedSchool(district, "S", "OH");
        var admin = SeedUser("da@x.com");
        SeedStaff(admin, district, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateStudentAsync(admin, new CreateSchoolStudentModel { FirstName = "NoSchool" });
        Assert.False(result.Success);
        Assert.Contains("school is required", result.Message, StringComparison.OrdinalIgnoreCase);
        // No half-created student.
        Assert.Empty(ctx.SchoolStudents);
    }

    [Fact]
    public async Task CreateStudent_DistrictAdmin_WithValidSchoolId_CreatesAndGrantsOwner()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var admin = SeedUser("da@x.com");
        SeedStaff(admin, district, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateStudentAsync(admin, new CreateSchoolStudentModel { FirstName = "Placed", SchoolId = school });
        Assert.True(result.Success);
        Assert.Equal(school, result.Data!.SchoolId);

        var access = ctx.SchoolStudentAccesses.Single(a => a.SchoolStudentId == result.Data.Id);
        Assert.Equal(admin, access.UserId);
        Assert.Equal(AccessRole.Owner, access.Role);
    }

    [Fact]
    public async Task CreateStudent_DistrictAdmin_OtherDistrictOrInactiveSchool_Denied()
    {
        var district = SeedDistrict("Mine", "OH");
        var admin = SeedUser("da@x.com");
        SeedStaff(admin, district, null, OrgRoleIds.DistrictAdmin);

        // Inactive school in same district.
        int inactive;
        using (var ctx = CreateContext())
        {
            var s = new School { DistrictId = district, Name = "Closed", IsActive = false };
            ctx.Schools.Add(s); ctx.SaveChanges(); inactive = s.Id;
        }
        // School in another district.
        var otherDistrict = SeedDistrict("Other", "OH");
        var foreign = SeedSchool(otherDistrict, "Far", "OH");

        using (var ctx = CreateContext())
            Assert.False((await CreateService(ctx).CreateStudentAsync(admin, new CreateSchoolStudentModel { FirstName = "X", SchoolId = inactive })).Success);
        using (var ctx = CreateContext())
            Assert.False((await CreateService(ctx).CreateStudentAsync(admin, new CreateSchoolStudentModel { FirstName = "X", SchoolId = foreign })).Success);
    }

    [Fact]
    public async Task CreateStudent_Teacher_MismatchedSchoolId_Denied_AbsentUsesOwnSchool()
    {
        var district = SeedDistrict("D", "OH");
        var mine = SeedSchool(district, "Mine", "OH");
        var other = SeedSchool(district, "Other", "OH");
        var teacher = SeedUser("t@x.com");
        SeedStaff(teacher, district, mine, OrgRoleIds.Teacher);

        // Mismatched explicit school -> denied.
        using (var ctx = CreateContext())
            Assert.False((await CreateService(ctx).CreateStudentAsync(teacher, new CreateSchoolStudentModel { FirstName = "X", SchoolId = other })).Success);

        // Absent schoolId -> defaults to own school (existing behavior preserved).
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx).CreateStudentAsync(teacher, new CreateSchoolStudentModel { FirstName = "Own" });
            Assert.True(r.Success);
            Assert.Equal(mine, r.Data!.SchoolId);
        }
    }

    // ================================================================= Assignment endpoints

    [Fact]
    public async Task Grant_HappyPath_DefaultsCollaborator_AndExplicitRole()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var admin = SeedUser("sa@x.com");
        SeedStaff(admin, district, school, OrgRoleIds.SchoolAdmin);
        var teacher = SeedUser("t@x.com");
        var teacherProfile = SeedStaff(teacher, district, school, OrgRoleIds.Teacher);
        var student = SeedStudent(school);

        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx).GrantStudentStaffAccessAsync(admin, student,
                new GrantStudentStaffAccessModel { StaffProfileId = teacherProfile });
            Assert.True(r.Success);
            Assert.Equal(AccessRole.Collaborator, r.Data!.AccessRole);
            Assert.Equal(teacherProfile, r.Data.StaffProfileId);
        }

        // Explicit role updates the existing grant (no duplicate).
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx).GrantStudentStaffAccessAsync(admin, student,
                new GrantStudentStaffAccessModel { StaffProfileId = teacherProfile, AccessRole = AccessRole.Owner });
            Assert.True(r.Success);
            Assert.Equal(AccessRole.Owner, r.Data!.AccessRole);
        }

        using (var ctx = CreateContext())
            Assert.Single(ctx.SchoolStudentAccesses.Where(a => a.SchoolStudentId == student && a.UserId == teacher));
    }

    [Fact]
    public async Task Grant_ReactivatesInactiveRow_NotDuplicate()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var admin = SeedUser("sa@x.com");
        SeedStaff(admin, district, school, OrgRoleIds.SchoolAdmin);
        var teacher = SeedUser("t@x.com");
        var teacherProfile = SeedStaff(teacher, district, school, OrgRoleIds.Teacher);
        var student = SeedStudent(school);
        SeedAccess(student, teacher, AccessRole.Viewer, isActive: false);

        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx).GrantStudentStaffAccessAsync(admin, student,
                new GrantStudentStaffAccessModel { StaffProfileId = teacherProfile, AccessRole = AccessRole.Collaborator });
            Assert.True(r.Success);
        }

        using var ctx2 = CreateContext();
        var rows = ctx2.SchoolStudentAccesses.Where(a => a.SchoolStudentId == student && a.UserId == teacher).ToList();
        Assert.Single(rows);
        Assert.True(rows[0].IsActive);
        Assert.Equal(AccessRole.Collaborator, rows[0].Role);
    }

    [Fact]
    public async Task Grant_TargetStaffWrongSchool_Denied()
    {
        var district = SeedDistrict("D", "OH");
        var mine = SeedSchool(district, "Mine", "OH");
        var other = SeedSchool(district, "Other", "OH");
        var admin = SeedUser("da@x.com");
        SeedStaff(admin, district, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedUser("t@x.com");
        var otherSchoolTeacher = SeedStaff(teacher, district, other, OrgRoleIds.Teacher);
        var student = SeedStudent(mine);

        using var ctx = CreateContext();
        var r = await CreateService(ctx).GrantStudentStaffAccessAsync(admin, student,
            new GrantStudentStaffAccessModel { StaffProfileId = otherSchoolTeacher });
        Assert.False(r.Success);
    }

    [Fact]
    public async Task Grant_TeacherCaller_Denied()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var teacherA = SeedUser("a@x.com");
        SeedStaff(teacherA, district, school, OrgRoleIds.Teacher);
        var teacherB = SeedUser("b@x.com");
        var teacherBProfile = SeedStaff(teacherB, district, school, OrgRoleIds.Teacher);
        var student = SeedStudent(school);
        SeedAccess(student, teacherA, AccessRole.Owner);

        using var ctx = CreateContext();
        var r = await CreateService(ctx).GrantStudentStaffAccessAsync(teacherA, student,
            new GrantStudentStaffAccessModel { StaffProfileId = teacherBProfile });
        Assert.False(r.Success);
        Assert.Contains("permission", r.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Grant_SchoolAdminCrossSchool_Denied()
    {
        var district = SeedDistrict("D", "OH");
        var mine = SeedSchool(district, "Mine", "OH");
        var other = SeedSchool(district, "Other", "OH");
        var admin = SeedUser("sa@x.com");
        SeedStaff(admin, district, mine, OrgRoleIds.SchoolAdmin);
        var teacher = SeedUser("t@x.com");
        var otherTeacher = SeedStaff(teacher, district, other, OrgRoleIds.Teacher);
        var studentInOther = SeedStudent(other);

        using var ctx = CreateContext();
        var r = await CreateService(ctx).GrantStudentStaffAccessAsync(admin, studentInOther,
            new GrantStudentStaffAccessModel { StaffProfileId = otherTeacher });
        Assert.False(r.Success);
        Assert.Contains("permission", r.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Revoke_ImmediatelyRemovesTeacherAccess_AndRegrantWorks()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var admin = SeedUser("sa@x.com");
        SeedStaff(admin, district, school, OrgRoleIds.SchoolAdmin);
        var teacher = SeedUser("t@x.com");
        var teacherProfile = SeedStaff(teacher, district, school, OrgRoleIds.Teacher);
        var student = SeedStudent(school);

        int accessId;
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx).GrantStudentStaffAccessAsync(admin, student,
                new GrantStudentStaffAccessModel { StaffProfileId = teacherProfile });
            accessId = r.Data!.AccessId;
        }

        // Teacher can open the student right after the grant.
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).GetStudentAsync(teacher, student)).Success);

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).RevokeStudentStaffAccessAsync(admin, student, accessId)).Success);

        // Immediate loss: GetStudent now fails for that teacher.
        using (var ctx = CreateContext())
            Assert.False((await CreateService(ctx).GetStudentAsync(teacher, student)).Success);
        using (var ctx = CreateContext())
            Assert.DoesNotContain((await CreateService(ctx).GetStudentsAsync(teacher)).Data!, s => s.Id == student);

        // Revoked-then-regrant restores access on the same row.
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).GrantStudentStaffAccessAsync(admin, student,
                new GrantStudentStaffAccessModel { StaffProfileId = teacherProfile })).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).GetStudentAsync(teacher, student)).Success);
    }

    [Fact]
    public async Task GetStaffAccess_ListsActiveGrants_WithStaffDetails()
    {
        var district = SeedDistrict("D", "OH");
        var school = SeedSchool(district, "S", "OH");
        var admin = SeedUser("sa@x.com");
        SeedStaff(admin, district, school, OrgRoleIds.SchoolAdmin);
        var teacher = SeedUser("teach@x.com");
        var teacherProfile = SeedStaff(teacher, district, school, OrgRoleIds.Teacher);
        var student = SeedStudent(school);
        SeedAccess(student, teacher, AccessRole.Collaborator);
        // An inactive grant must not appear.
        var teacher2 = SeedUser("teach2@x.com");
        SeedStaff(teacher2, district, school, OrgRoleIds.Teacher);
        SeedAccess(student, teacher2, AccessRole.Viewer, isActive: false);

        using var ctx = CreateContext();
        var r = await CreateService(ctx).GetStudentStaffAccessAsync(admin, student);
        Assert.True(r.Success);
        Assert.Single(r.Data!);
        Assert.Equal(teacherProfile, r.Data![0].StaffProfileId);
        Assert.Equal("teach@x.com", r.Data[0].Email);
        Assert.Equal(AccessRole.Collaborator, r.Data[0].AccessRole);
    }

    public void Dispose() => _connection.Dispose();
}
