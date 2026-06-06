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
/// P4a coverage for structured IEP authoring: LineageId stability across updates (and freshness
/// for re-added entities), last-write-wins stamping on the entity AND the parent draft, the
/// SchoolStudentAccess (Viewer reads / Collaborator+ mutates, cross-school rejection) gate, and
/// full-draft GET ordering. Real SQLite in-memory engine (same pattern as EducatorServiceTests).
/// </summary>
public sealed class IepDraftServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public IepDraftServiceTests()
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

    private readonly CapturingAuditLogger _audit = new();

    private IepDraftService CreateService(ApplicationDbContext ctx)
        => new(ctx, _audit, NullLogger<IepDraftService>.Instance);

    // ---------------------------------------------------------------- Seed helpers

    private sealed record SchoolScenario(int SchoolId, int CollaboratorUserId, int StudentId);

    /// <summary>
    /// Seeds a district/school, a TeacherProfile for an educator user, a student in that school,
    /// and an active SchoolStudentAccess with the given role for that educator.
    /// </summary>
    private SchoolScenario SeedSchoolWithStudent(string emailPrefix, AccessRole role = AccessRole.Collaborator)
    {
        using var ctx = CreateContext();

        var user = new User { Email = $"{emailPrefix}@example.com", PasswordHash = "x", FirstName = "Ed", LastName = "U", Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var district = new District { Name = $"{emailPrefix} District" };
        ctx.Districts.Add(district);
        ctx.SaveChanges();

        var school = new School { DistrictId = district.Id, Name = $"{emailPrefix} School" };
        ctx.Schools.Add(school);
        ctx.SaveChanges();

        ctx.TeacherProfiles.Add(new TeacherProfile { UserId = user.Id, SchoolId = school.Id });
        ctx.SaveChanges();

        var student = new SchoolStudent { SchoolId = school.Id, FirstName = "Sam", IsActive = true };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();

        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id,
            UserId = user.Id,
            Role = role,
            IsActive = true
        });
        ctx.SaveChanges();

        return new SchoolScenario(school.Id, user.Id, student.Id);
    }

    /// <summary>Adds a second user with the given access role on an existing student.</summary>
    private int SeedAdditionalUser(int studentId, int schoolId, string emailPrefix, AccessRole role)
    {
        using var ctx = CreateContext();
        var user = new User { Email = $"{emailPrefix}@example.com", PasswordHash = "x", FirstName = "Co", LastName = "U", Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        ctx.TeacherProfiles.Add(new TeacherProfile { UserId = user.Id, SchoolId = schoolId });
        ctx.SaveChanges();

        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = studentId,
            UserId = user.Id,
            Role = role,
            IsActive = true
        });
        ctx.SaveChanges();
        return user.Id;
    }

    private async Task<int> CreateDraftAsync(SchoolScenario s)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateDraftAsync(s.CollaboratorUserId, s.StudentId, "2025 Annual");
        Assert.True(result.Success);
        return result.Data!.Id;
    }

    // ---------------------------------------------------------------- LineageId

    [Fact]
    public async Task UpdateGoal_KeepsSameLineageId_AcrossUpdates()
    {
        var s = SeedSchoolWithStudent("lineage");
        var draftId = await CreateDraftAsync(s);

        Guid lineageId;
        int goalId;
        using (var ctx = CreateContext())
        {
            var add = await CreateService(ctx).AddGoalAsync(s.CollaboratorUserId, draftId, new UpsertIepDraftGoalModel
            {
                Domain = "Reading",
                GoalText = "Read 80 wpm"
            });
            Assert.True(add.Success);
            lineageId = add.Data!.LineageId;
            goalId = add.Data.Id;
            Assert.NotEqual(Guid.Empty, lineageId);
        }

        using (var ctx = CreateContext())
        {
            var upd = await CreateService(ctx).UpdateGoalAsync(s.CollaboratorUserId, draftId, goalId, new UpsertIepDraftGoalModel
            {
                Domain = "Reading",
                GoalText = "Read 100 wpm"
            });
            Assert.True(upd.Success);
            Assert.Equal(lineageId, upd.Data!.LineageId); // stable across update
            Assert.Equal("Read 100 wpm", upd.Data.GoalText);
        }
    }

    [Fact]
    public async Task ReAddedGoal_GetsDifferentLineageId()
    {
        var s = SeedSchoolWithStudent("readd");
        var draftId = await CreateDraftAsync(s);

        Guid firstLineage;
        int firstId;
        using (var ctx = CreateContext())
        {
            var add = await CreateService(ctx).AddGoalAsync(s.CollaboratorUserId, draftId, new UpsertIepDraftGoalModel { GoalText = "G1" });
            firstLineage = add.Data!.LineageId;
            firstId = add.Data.Id;
        }

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).DeleteGoalAsync(s.CollaboratorUserId, draftId, firstId)).Success);

        using (var ctx = CreateContext())
        {
            var readd = await CreateService(ctx).AddGoalAsync(s.CollaboratorUserId, draftId, new UpsertIepDraftGoalModel { GoalText = "G1 again" });
            Assert.True(readd.Success);
            Assert.NotEqual(firstLineage, readd.Data!.LineageId); // fresh lineage, never reuses dropped
        }
    }

    // ---------------------------------------------------------------- Last-write-wins

    [Fact]
    public async Task TwoSequentialUpdatesByDifferentUsers_LastWriterWins_OnGoalAndDraft()
    {
        var s = SeedSchoolWithStudent("lww");
        var secondUser = SeedAdditionalUser(s.StudentId, s.SchoolId, "lww-co", AccessRole.Collaborator);
        var draftId = await CreateDraftAsync(s);

        int goalId;
        using (var ctx = CreateContext())
        {
            var add = await CreateService(ctx).AddGoalAsync(s.CollaboratorUserId, draftId, new UpsertIepDraftGoalModel { GoalText = "v0" });
            goalId = add.Data!.Id;
        }

        // User 1 edits.
        using (var ctx = CreateContext())
            await CreateService(ctx).UpdateGoalAsync(s.CollaboratorUserId, draftId, goalId, new UpsertIepDraftGoalModel { GoalText = "v1" });

        // User 2 edits last -> wins.
        using (var ctx = CreateContext())
            await CreateService(ctx).UpdateGoalAsync(secondUser, draftId, goalId, new UpsertIepDraftGoalModel { GoalText = "v2-final" });

        using (var ctx = CreateContext())
        {
            var goal = ctx.IepDraftGoals.Single(g => g.Id == goalId);
            Assert.Equal("v2-final", goal.GoalText);
            Assert.Equal(secondUser, goal.LastEditedByUserId);
            Assert.NotNull(goal.LastEditedAt);

            var draft = ctx.IepDrafts.Single(d => d.Id == draftId);
            Assert.Equal(secondUser, draft.LastEditedByUserId); // parent stamp reflects last writer
            Assert.NotNull(draft.LastEditedAt);
        }
    }

    // ---------------------------------------------------------------- Access

    [Fact]
    public async Task UserWithoutAccess_RejectedForGetAndMutate()
    {
        var s = SeedSchoolWithStudent("noaccess");
        var draftId = await CreateDraftAsync(s);

        // A user with no SchoolStudentAccess at all.
        int strangerId;
        using (var ctx = CreateContext())
        {
            var stranger = new User { Email = "stranger@example.com", PasswordHash = "x", FirstName = "S", LastName = "T", Role = UserRole.Educator };
            ctx.Users.Add(stranger);
            ctx.SaveChanges();
            strangerId = stranger.Id;
        }

        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetDraftAsync(strangerId, draftId);
            Assert.False(get.Success);
            Assert.Contains("permission", get.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var mutate = await CreateService(ctx).AddGoalAsync(strangerId, draftId, new UpsertIepDraftGoalModel { GoalText = "x" });
            Assert.False(mutate.Success);
            Assert.Contains("permission", mutate.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task EducatorFromAnotherSchool_Rejected()
    {
        var schoolA = SeedSchoolWithStudent("schoolA");
        var schoolB = SeedSchoolWithStudent("schoolB");
        var draftInA = await CreateDraftAsync(schoolA);

        // Educator in school B tries to read / mutate a draft owned by school A's student.
        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetDraftAsync(schoolB.CollaboratorUserId, draftInA);
            Assert.False(get.Success);
            Assert.Contains("permission", get.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var mutate = await CreateService(ctx).AddSectionAsync(schoolB.CollaboratorUserId, draftInA, new UpsertIepDraftSectionModel
            {
                SectionKind = IepSectionKind.PresentLevels
            });
            Assert.False(mutate.Success);
        }
    }

    [Fact]
    public async Task ViewerRole_CanGet_ButCannotMutate()
    {
        var s = SeedSchoolWithStudent("vieweronly", AccessRole.Collaborator);
        var draftId = await CreateDraftAsync(s);
        var viewerId = SeedAdditionalUser(s.StudentId, s.SchoolId, "viewer", AccessRole.Viewer);

        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetDraftAsync(viewerId, draftId);
            Assert.True(get.Success); // Viewer+ can read
        }

        using (var ctx = CreateContext())
        {
            var mutate = await CreateService(ctx).AddGoalAsync(viewerId, draftId, new UpsertIepDraftGoalModel { GoalText = "nope" });
            Assert.False(mutate.Success); // mutation requires Collaborator+
            Assert.Contains("permission", mutate.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CreateDraft_AsViewer_Rejected()
    {
        // Viewer-only access on the student -> cannot create a draft (Collaborator+ required).
        var s = SeedSchoolWithStudent("create-collab", AccessRole.Collaborator);
        var viewerId = SeedAdditionalUser(s.StudentId, s.SchoolId, "create-viewer", AccessRole.Viewer);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateDraftAsync(viewerId, s.StudentId, "x");
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Full-draft GET

    [Fact]
    public async Task GetDraft_ReturnsAllFiveCollections_OrderedByDisplayOrder()
    {
        var s = SeedSchoolWithStudent("fulldraft");
        var draftId = await CreateDraftAsync(s);
        var u = s.CollaboratorUserId;

        using (var ctx = CreateContext())
        {
            var svc = CreateService(ctx);
            // Two of each child type; DisplayOrder should auto-increment 0,1.
            await svc.AddSectionAsync(u, draftId, new UpsertIepDraftSectionModel { SectionKind = IepSectionKind.PresentLevels, RichText = "S0" });
            await svc.AddSectionAsync(u, draftId, new UpsertIepDraftSectionModel { SectionKind = IepSectionKind.Placement, RichText = "S1" });
            await svc.AddGoalAsync(u, draftId, new UpsertIepDraftGoalModel { GoalText = "G0" });
            await svc.AddGoalAsync(u, draftId, new UpsertIepDraftGoalModel { GoalText = "G1" });
            await svc.AddServiceLineAsync(u, draftId, new UpsertIepDraftServiceLineModel { ServiceType = "SL0" });
            await svc.AddServiceLineAsync(u, draftId, new UpsertIepDraftServiceLineModel { ServiceType = "SL1" });
            await svc.AddAccommodationAsync(u, draftId, new UpsertIepDraftAccommodationModel { Text = "A0" });
            await svc.AddAccommodationAsync(u, draftId, new UpsertIepDraftAccommodationModel { Text = "A1" });
            await svc.AddTransitionItemAsync(u, draftId, new UpsertIepDraftTransitionItemModel { PostsecondaryGoalArea = "T0" });
            await svc.AddTransitionItemAsync(u, draftId, new UpsertIepDraftTransitionItemModel { PostsecondaryGoalArea = "T1" });
        }

        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetDraftAsync(u, draftId);
            Assert.True(get.Success);
            var d = get.Data!;

            Assert.Equal(2, d.Sections.Count);
            Assert.Equal(2, d.Goals.Count);
            Assert.Equal(2, d.ServiceLines.Count);
            Assert.Equal(2, d.Accommodations.Count);
            Assert.Equal(2, d.TransitionItems.Count);

            // Ordered by DisplayOrder ascending.
            Assert.Equal(new[] { 0, 1 }, d.Sections.Select(x => x.DisplayOrder).ToArray());
            Assert.Equal("S0", d.Sections[0].RichText);
            Assert.Equal("G0", d.Goals[0].GoalText);
            Assert.Equal("SL0", d.ServiceLines[0].ServiceType);
            Assert.Equal("A0", d.Accommodations[0].Text);
            Assert.Equal("T0", d.TransitionItems[0].PostsecondaryGoalArea);
        }
    }

    [Fact]
    public async Task ListDrafts_AsViewer_Succeeds()
    {
        var s = SeedSchoolWithStudent("list-collab");
        await CreateDraftAsync(s);
        var viewerId = SeedAdditionalUser(s.StudentId, s.SchoolId, "list-viewer", AccessRole.Viewer);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ListDraftsAsync(viewerId, s.StudentId);
        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    // ---------------------------------------------------------------- Audit (P6a)

    [Fact]
    public async Task GetDraft_RecordsOneViewAuditEntry()
    {
        var s = SeedSchoolWithStudent("audit-view");
        var draftId = await CreateDraftAsync(s);

        _audit.Entries.Clear();
        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetDraftAsync(s.CollaboratorUserId, draftId);
            Assert.True(get.Success);
        }

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.View, entry.Action);
        Assert.Equal(s.CollaboratorUserId, entry.ActorUserId);
        Assert.Equal("IepDraft", entry.ResourceType);
        Assert.Equal(draftId, entry.ResourceId);
    }

    [Fact]
    public async Task UpdateGoal_RecordsOneEditAuditEntry()
    {
        var s = SeedSchoolWithStudent("audit-edit");
        var draftId = await CreateDraftAsync(s);

        int goalId;
        using (var ctx = CreateContext())
        {
            var add = await CreateService(ctx).AddGoalAsync(s.CollaboratorUserId, draftId, new UpsertIepDraftGoalModel { GoalText = "G" });
            goalId = add.Data!.Id;
        }

        _audit.Entries.Clear();
        using (var ctx = CreateContext())
        {
            var upd = await CreateService(ctx).UpdateGoalAsync(s.CollaboratorUserId, draftId, goalId, new UpsertIepDraftGoalModel { GoalText = "G2" });
            Assert.True(upd.Success);
        }

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Edit, entry.Action);
        Assert.Equal(s.CollaboratorUserId, entry.ActorUserId);
        Assert.Equal("IepDraft", entry.ResourceType);
        Assert.Equal(draftId, entry.ResourceId);
    }

    public void Dispose() => _connection.Dispose();
}
