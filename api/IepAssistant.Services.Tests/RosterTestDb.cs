using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Tests;

/// <summary>
/// SQLite in-memory database + org seed builders shared by the plan-3 roster/team/import tests. Each
/// test class owns one instance (fresh schema via EnsureCreated, dropped when the connection closes).
/// Builders take explicit arguments so a test reads top-to-bottom without a mystery fixture.
/// </summary>
public sealed class RosterTestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public RosterTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = Context();
        ctx.Database.EnsureCreated();
    }

    public ApplicationDbContext Context() => new(_options);

    public int District(string name = "District", string? stateCode = "OH")
    {
        using var ctx = Context();
        var d = new District { Name = name, StateCode = stateCode };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    public int School(int districtId, string name, string? stateCode = null, bool isActive = true)
    {
        using var ctx = Context();
        var s = new School { DistrictId = districtId, Name = name, StateCode = stateCode, IsActive = isActive };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    /// <summary>Creates a User + StaffProfile; returns (userId, staffProfileId).</summary>
    public (int UserId, int ProfileId) Staff(string email, int districtId, int? schoolId, int orgRoleId, string first = "Staff", string last = "Member", bool isActive = true, string? title = null)
    {
        using var ctx = Context();
        var user = new User { Email = email, PasswordHash = "x", FirstName = first, LastName = last, Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        var p = new StaffProfile { UserId = user.Id, DistrictId = districtId, SchoolId = schoolId, OrgRoleId = orgRoleId, IsActive = isActive, Title = title };
        ctx.StaffProfiles.Add(p);
        ctx.SaveChanges();
        return (user.Id, p.Id);
    }

    public int Student(int schoolId, string first = "Stu", string? last = "Dent", string? externalId = null,
        GradeLevel? grade = null, StudentStatus status = StudentStatus.Active, DateTime? dob = null, string? stateCode = null)
    {
        using var ctx = Context();
        var districtId = ctx.Schools.Where(s => s.Id == schoolId).Select(s => s.DistrictId).Single();
        var s = new SchoolStudent
        {
            SchoolId = schoolId,
            DistrictId = districtId,
            FirstName = first,
            LastName = last,
            ExternalStudentId = externalId,
            GradeLevel = grade,
            Status = status,
            DateOfBirth = dob,
            StateCode = stateCode
        };
        ctx.SchoolStudents.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    public int Access(int studentId, int userId, AccessRole role = AccessRole.Collaborator, bool isActive = true)
    {
        using var ctx = Context();
        var a = new SchoolStudentAccess { SchoolStudentId = studentId, UserId = userId, Role = role, IsActive = isActive };
        ctx.SchoolStudentAccesses.Add(a);
        ctx.SaveChanges();
        return a.Id;
    }

    /// <summary>Seeds a team row (+ matching access row); a lead also mirrors to CaseManagerUserId.</summary>
    public int TeamMember(int studentId, int userId, TeamRole role, bool isLead = false, AccessRole? access = null)
    {
        using var ctx = Context();
        var m = new StudentTeamMember { SchoolStudentId = studentId, UserId = userId, TeamRole = role, IsLead = isLead, IsActive = true };
        ctx.StudentTeamMembers.Add(m);
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = studentId, UserId = userId, Role = access ?? role.DefaultAccessRole(), IsActive = true });
        if (isLead)
            ctx.SchoolStudents.Single(s => s.Id == studentId).CaseManagerUserId = userId;
        ctx.SaveChanges();
        return m.Id;
    }

    public void Dispose() => _connection.Dispose();
}
