using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P2 coverage: the read-only district audit-log viewer (<see cref="AuditLogQueryService"/>). Entries
/// are actor-scoped to the caller's district (SchoolAdmin: own-school actors only, deactivated staff
/// included, district-admin actors excluded from a SchoolAdmin's view); filters cover single actor,
/// action, date range, and the student resource-ID expansion; pagination is keyset on Id DESC; and
/// enrichment renders stable fallbacks for deleted resources / absent users. Uses a real SQLite
/// in-memory engine, same pattern as <see cref="DistrictServiceTests"/>.
/// </summary>
public sealed class AuditLogQueryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public AuditLogQueryServiceTests()
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

    private AuditLogQueryService CreateService(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx));

    // ----------------------------------------------------------------- seed helpers

    private int SeedDistrict(string name)
    {
        using var ctx = CreateContext();
        var d = new District { Name = name };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedSchool(int districtId, string name, bool isActive = true)
    {
        using var ctx = CreateContext();
        var s = new School { DistrictId = districtId, Name = name, IsActive = isActive };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private int SeedUser(string email, string firstName = "F", string? lastName = "L", UserRole role = UserRole.Educator)
    {
        using var ctx = CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FirstName = firstName, LastName = lastName ?? string.Empty, Role = role };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return u.Id;
    }

    private int SeedStaff(string email, int districtId, int? schoolId, int orgRoleId, bool isActive = true,
        string firstName = "F", string? lastName = "L")
    {
        var userId = SeedUser(email, firstName, lastName);
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

    private int SeedStudent(int schoolId, string firstName = "Sam", string? lastName = "Student", bool isActive = true)
    {
        using var ctx = CreateContext();
        var s = new SchoolStudent { SchoolId = schoolId, FirstName = firstName, LastName = lastName, IsActive = isActive };
        ctx.SchoolStudents.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private int SeedDraft(int schoolStudentId)
    {
        using var ctx = CreateContext();
        var d = new IepDraft { SchoolStudentId = schoolStudentId };
        ctx.IepDrafts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedVersion(int schoolStudentId, int sourceDraftId, int versionNumber)
    {
        using var ctx = CreateContext();
        var v = new IepVersion
        {
            SchoolStudentId = schoolStudentId,
            SourceDraftId = sourceDraftId,
            VersionNumber = versionNumber,
            FinalizedByUserId = 1,
            FinalizedAt = DateTime.UtcNow
        };
        ctx.IepVersions.Add(v);
        ctx.SaveChanges();
        return v.Id;
    }

    private int SeedAudit(AuditAction action, int actorUserId, string resourceType, int resourceId,
        int? recipientUserId = null, DateTime? createdAt = null)
    {
        using var ctx = CreateContext();
        var row = new AccessAuditLog
        {
            Action = action,
            ActorUserId = actorUserId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            RecipientUserId = recipientUserId,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        ctx.AccessAuditLogs.Add(row);
        ctx.SaveChanges();
        return row.Id;
    }

    // ================================================================= Actor scoping

    [Fact]
    public async Task Query_DistrictAdmin_ReturnsAllDistrictActors_IncludingDeactivatedAndDistrictAdmin()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var activeTeacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var deactivatedTeacher = SeedStaff("gone@x.com", districtId, schoolId, OrgRoleIds.Teacher, isActive: false);

        // Foreign district actor must never bleed in.
        var otherDistrict = SeedDistrict("Other");
        var foreignSchool = SeedSchool(otherDistrict, "F");
        var foreign = SeedStaff("foreign@x.com", otherDistrict, foreignSchool, OrgRoleIds.Teacher);

        var student = SeedStudent(schoolId);
        var aDa = SeedAudit(AuditAction.View, da, "SchoolStudent", student);
        var aActive = SeedAudit(AuditAction.View, activeTeacher, "SchoolStudent", student);
        var aDeactivated = SeedAudit(AuditAction.Edit, deactivatedTeacher, "SchoolStudent", student);
        SeedAudit(AuditAction.View, foreign, "SchoolStudent", student); // foreign — excluded

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery());

        Assert.True(result.Success);
        var ids = result.Data!.Entries.Select(e => e.Id).ToList();
        Assert.Contains(aDa, ids);
        Assert.Contains(aActive, ids);
        Assert.Contains(aDeactivated, ids); // deactivated actor's history is included
        Assert.Equal(3, ids.Count); // foreign row excluded
    }

    [Fact]
    public async Task Query_SchoolAdmin_OwnSchoolActorsOnly_IncludesDeactivated_ExcludesDistrictAdminActor()
    {
        var districtId = SeedDistrict("D");
        var schoolA = SeedSchool(districtId, "A");
        var schoolB = SeedSchool(districtId, "B");

        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var schoolAdmin = SeedStaff("sa@x.com", districtId, schoolA, OrgRoleIds.SchoolAdmin);
        var teacherA = SeedStaff("ta@x.com", districtId, schoolA, OrgRoleIds.Teacher);
        var deactivatedA = SeedStaff("goneA@x.com", districtId, schoolA, OrgRoleIds.Teacher, isActive: false);
        var teacherB = SeedStaff("tb@x.com", districtId, schoolB, OrgRoleIds.Teacher);

        var studentA = SeedStudent(schoolA);
        var aSchoolAdmin = SeedAudit(AuditAction.View, schoolAdmin, "SchoolStudent", studentA);
        var aTeacherA = SeedAudit(AuditAction.View, teacherA, "SchoolStudent", studentA);
        var aDeactivatedA = SeedAudit(AuditAction.Edit, deactivatedA, "SchoolStudent", studentA);
        SeedAudit(AuditAction.View, teacherB, "SchoolStudent", studentA); // other school — excluded
        SeedAudit(AuditAction.View, da, "SchoolStudent", studentA);       // district-admin actor — excluded

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(schoolAdmin, new AuditLogQuery());

        Assert.True(result.Success);
        var ids = result.Data!.Entries.Select(e => e.Id).ToList();
        Assert.Equal(new[] { aSchoolAdmin, aTeacherA, aDeactivatedA }.OrderBy(i => i), ids.OrderBy(i => i));
    }

    [Fact]
    public async Task Query_StaffUserIdFilter_RestrictsToSingleActor()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher1 = SeedStaff("t1@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var teacher2 = SeedStaff("t2@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        var student = SeedStudent(schoolId);
        var wanted = SeedAudit(AuditAction.View, teacher1, "SchoolStudent", student);
        SeedAudit(AuditAction.View, teacher2, "SchoolStudent", student);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { StaffUserId = teacher1 });

        Assert.True(result.Success);
        var entry = Assert.Single(result.Data!.Entries);
        Assert.Equal(wanted, entry.Id);
        Assert.Equal(teacher1, entry.ActorUserId);
    }

    // ================================================================= Student resource-ID expansion

    [Fact]
    public async Task Query_StudentFilter_MatchesStudentDraftAndVersionResourceRows()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        var target = SeedStudent(schoolId, firstName: "Target");
        var draft = SeedDraft(target);
        var version = SeedVersion(target, draft, 1);

        var other = SeedStudent(schoolId, firstName: "Other");
        var otherDraft = SeedDraft(other);

        // Rows that SHOULD match the target-student filter.
        var onStudent = SeedAudit(AuditAction.View, teacher, "SchoolStudent", target);
        var onDraft = SeedAudit(AuditAction.Edit, teacher, "IepDraft", draft);
        var onVersion = SeedAudit(AuditAction.Finalize, teacher, "IepVersion", version);

        // Rows that should NOT match (a different student's student/draft rows).
        SeedAudit(AuditAction.View, teacher, "SchoolStudent", other);
        SeedAudit(AuditAction.Edit, teacher, "IepDraft", otherDraft);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { StudentId = target });

        Assert.True(result.Success);
        var ids = result.Data!.Entries.Select(e => e.Id).OrderBy(i => i).ToList();
        Assert.Equal(new[] { onStudent, onDraft, onVersion }.OrderBy(i => i), ids);
    }

    // ================================================================= Action + date-range filters

    [Fact]
    public async Task Query_ActionFilter_ParsesCaseInsensitively_AndRestricts()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var student = SeedStudent(schoolId);

        var viewRow = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student);
        SeedAudit(AuditAction.Edit, teacher, "SchoolStudent", student);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { Action = "view" });

        Assert.True(result.Success);
        var entry = Assert.Single(result.Data!.Entries);
        Assert.Equal(viewRow, entry.Id);
        Assert.Equal("View", entry.Action);
    }

    [Fact]
    public async Task Query_InvalidAction_Fails400()
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { Action = "Frobnicate" });

        Assert.False(result.Success);
        // Not a permission/not-found message → controller maps to 400.
        Assert.DoesNotContain("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Query_DateRange_IsInclusiveOfBothBounds()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var student = SeedStudent(schoolId);

        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var before = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student, createdAt: from.AddSeconds(-1));
        var atFrom = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student, createdAt: from);          // inclusive
        var inside = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student, createdAt: from.AddDays(10));
        var atTo = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student, createdAt: to);              // inclusive
        var after = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student, createdAt: to.AddSeconds(1));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { FromUtc = from, ToUtc = to });

        Assert.True(result.Success);
        var ids = result.Data!.Entries.Select(e => e.Id).OrderBy(i => i).ToList();
        Assert.Equal(new[] { atFrom, inside, atTo }.OrderBy(i => i), ids);
        Assert.DoesNotContain(before, ids);
        Assert.DoesNotContain(after, ids);
    }

    // ================================================================= Keyset pagination

    [Fact]
    public async Task Query_KeysetPaging_IsStableAcrossPages_WithCorrectNextCursor()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var student = SeedStudent(schoolId);

        var ids = new List<int>();
        for (var i = 0; i < 5; i++)
            ids.Add(SeedAudit(AuditAction.View, teacher, "SchoolStudent", student));
        ids.Sort();                 // ascending by Id (insertion order)
        var descending = ids.AsEnumerable().Reverse().ToList(); // Id DESC = service order

        using var ctx = CreateContext();
        var service = CreateService(ctx);

        // Page 1
        var page1 = await service.QueryAsync(da, new AuditLogQuery { PageSize = 2 });
        Assert.True(page1.Success);
        Assert.Equal(new[] { descending[0], descending[1] }, page1.Data!.Entries.Select(e => e.Id));
        Assert.Equal(descending[1], page1.Data.NextCursor);

        // Page 2
        var page2 = await service.QueryAsync(da, new AuditLogQuery { PageSize = 2, Cursor = page1.Data.NextCursor });
        Assert.True(page2.Success);
        Assert.Equal(new[] { descending[2], descending[3] }, page2.Data!.Entries.Select(e => e.Id));
        Assert.Equal(descending[3], page2.Data.NextCursor);

        // Page 3 (final, partial) — cursor exhausted → null.
        var page3 = await service.QueryAsync(da, new AuditLogQuery { PageSize = 2, Cursor = page2.Data.NextCursor });
        Assert.True(page3.Success);
        Assert.Equal(new[] { descending[4] }, page3.Data!.Entries.Select(e => e.Id));
        Assert.Null(page3.Data.NextCursor);
    }

    [Fact]
    public async Task Query_ExactlyFullLastPage_ReturnsNullNextCursor()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var student = SeedStudent(schoolId);
        SeedAudit(AuditAction.View, teacher, "SchoolStudent", student);
        SeedAudit(AuditAction.View, teacher, "SchoolStudent", student);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { PageSize = 2 });

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Entries.Count);
        Assert.Null(result.Data.NextCursor); // no extra row beyond the page
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Query_InvalidPageSize_Fails400(int pageSize)
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { PageSize = pageSize });

        Assert.False(result.Success);
        Assert.DoesNotContain("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Query_NegativeCursor_Fails400()
    {
        var districtId = SeedDistrict("D");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { Cursor = -1 });

        Assert.False(result.Success);
        Assert.DoesNotContain("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Query_PageSizeAboveMax_IsCappedAtOneHundred()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var student = SeedStudent(schoolId);

        // 101 rows; a request for 500 must return at most 100 with a next cursor.
        for (var i = 0; i < 101; i++)
            SeedAudit(AuditAction.View, teacher, "SchoolStudent", student);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery { PageSize = 500 });

        Assert.True(result.Success);
        Assert.Equal(100, result.Data!.Entries.Count);
        Assert.NotNull(result.Data.NextCursor);
    }

    // ================================================================= Enrichment

    [Fact]
    public async Task Query_Enrichment_PopulatesActorResourceAndShareRecipientNames()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher, firstName: "Terry", lastName: "Teacher");
        var parent = SeedUser("parent@x.com", firstName: "Pat", lastName: "Parent", role: UserRole.Parent);

        var student = SeedStudent(schoolId, firstName: "Stella", lastName: "Student");
        var draft = SeedDraft(student);
        var version = SeedVersion(student, draft, 1);

        var studentRow = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student);
        var draftRow = SeedAudit(AuditAction.Edit, teacher, "IepDraft", draft);
        var versionRow = SeedAudit(AuditAction.Finalize, teacher, "IepVersion", version);
        var shareRow = SeedAudit(AuditAction.Share, teacher, "SchoolStudent", student, recipientUserId: parent);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery());

        Assert.True(result.Success);
        var byId = result.Data!.Entries.ToDictionary(e => e.Id);

        Assert.Equal("Terry Teacher", byId[studentRow].ActorName);
        Assert.Equal("Stella Student", byId[studentRow].ResourceDisplayName);
        Assert.Equal("IEP draft for Stella Student", byId[draftRow].ResourceDisplayName);
        Assert.Equal("IEP version for Stella Student", byId[versionRow].ResourceDisplayName);

        Assert.Equal(parent, byId[shareRow].RecipientUserId);
        Assert.Equal("Pat Parent", byId[shareRow].RecipientName);
    }

    [Fact]
    public async Task Query_Enrichment_UnresolvableReferences_RenderFallbacks_NoThrow()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var da = SeedStaff("da@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        // A draft + student we will delete after recording the audit rows.
        var student = SeedStudent(schoolId, firstName: "Ghost", lastName: "Gone");
        var draft = SeedDraft(student);

        var draftRow = SeedAudit(AuditAction.Edit, teacher, "IepDraft", draft);
        var studentRow = SeedAudit(AuditAction.View, teacher, "SchoolStudent", student);
        // Share to a user id that does not exist → "Unknown user".
        var shareRow = SeedAudit(AuditAction.Share, teacher, "SchoolStudent", student, recipientUserId: 999999);

        using (var del = CreateContext())
        {
            del.IepDrafts.Remove(del.IepDrafts.Single(d => d.Id == draft));
            del.SchoolStudents.Remove(del.SchoolStudents.Single(s => s.Id == student));
            del.SaveChanges();
        }

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(da, new AuditLogQuery());

        Assert.True(result.Success);
        var byId = result.Data!.Entries.ToDictionary(e => e.Id);

        Assert.Equal($"Draft #{draft}", byId[draftRow].ResourceDisplayName);   // draft gone
        Assert.Equal("Deleted student", byId[studentRow].ResourceDisplayName); // student gone
        Assert.Equal("Unknown user", byId[shareRow].RecipientName);            // recipient absent
    }

    // ================================================================= 403 matrix

    [Fact]
    public async Task Query_Teacher_Denied()
    {
        var districtId = SeedDistrict("D");
        var schoolId = SeedSchool(districtId, "S");
        var teacher = SeedStaff("t@x.com", districtId, schoolId, OrgRoleIds.Teacher);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(teacher, new AuditLogQuery());

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Query_NonStaffUser_Denied()
    {
        // Parents/students have no StaffProfile → permission failure (maps to 403).
        var parent = SeedUser("parent@x.com", role: UserRole.Parent);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).QueryAsync(parent, new AuditLogQuery());

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _connection.Dispose();
}
