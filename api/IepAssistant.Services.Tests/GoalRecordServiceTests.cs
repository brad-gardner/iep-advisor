using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 7 phase 1: goals as first-class records projected at finalize (new rows Active, carried lineage
/// marked Carried on the prior record, dropped lineage Retired with the recorded reason or a default),
/// provider observations (value-or-note required), the insufficient-data trajectory rule, and status
/// updates (NotMet requires a reason).
/// </summary>
public sealed class GoalRecordServiceTests : IDisposable
{
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public GoalRecordServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new ImmutableVersionInterceptor())
            .Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private GoalRecordService CreateGoalService(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), new AccessService(ctx), NullLogger<GoalRecordService>.Instance);

    private AuthoredDocumentVersionService CreateAuthoredService(ApplicationDbContext ctx)
        => new(
            ctx,
            new OrgAccessService(ctx),
            new AccessService(ctx),
            new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance),
            new NoopBlobStorageFake(),
            _audit,
            CreateGoalService(ctx),
            NullLogger<AuthoredDocumentVersionService>.Instance);

    private sealed class NoopBlobStorageFake : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://fake.blob/{blobPath}");
        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
            => Task.FromResult($"https://fake.blob/{blobPath}?sas=token");
    }

    private sealed record Scenario(
        int StudentId, int TeacherId, int InstanceId,
        Guid GoalsFieldKey, Guid GoalCol, Guid BaselineCol, Guid DomainCol);

    private Scenario Seed(string prefix)
    {
        using var ctx = CreateContext();
        var teacher = new User { Email = $"{prefix}-t@example.com", PasswordHash = "x", FirstName = "Steph", LastName = "Case", Role = UserRole.Educator };
        ctx.Users.Add(teacher);
        ctx.SaveChanges();

        var district = new District { Name = prefix };
        ctx.Districts.Add(district);
        ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = prefix };
        ctx.Schools.Add(school);
        ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = teacher.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan", LastName = "Ellis" };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = teacher.Id, Role = AccessRole.Collaborator, IsActive = true });
        ctx.SaveChanges();

        var domainCol = Guid.NewGuid();
        var goalCol = Guid.NewGuid();
        var baselineCol = Guid.NewGuid();
        var goalsFieldKey = Guid.NewGuid();

        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { version } });
        ctx.SaveChanges();
        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id,
            SectionKey = Guid.NewGuid(),
            Title = "Goals",
            DisplayOrder = 0,
            Fields =
            {
                new TemplateField
                {
                    DocumentTemplateVersionId = version.Id,
                    FieldKey = goalsFieldKey,
                    FieldType = FieldType.Table,
                    Label = "Goals",
                    DisplayOrder = 0,
                    ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals,
                        (domainCol, FieldType.Text, "Domain", ColumnSemantics.Domain),
                        (goalCol, FieldType.Text, "Goal", ColumnSemantics.GoalText),
                        (baselineCol, FieldType.Text, "Baseline", ColumnSemantics.Baseline))
                }
            }
        });
        ctx.SaveChanges();

        var instance = new DocumentInstance
        {
            SchoolStudentId = student.Id,
            DocumentTypeId = IepTypeId,
            DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = "{}",
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();

        return new Scenario(student.Id, teacher.Id, instance.Id, goalsFieldKey, goalCol, baselineCol, domainCol);
    }

    private static string BuildGoalsJson(Scenario s, params (Guid RowId, string GoalText, string? Baseline)[] rows)
    {
        var rowsArray = rows.Select(r => new Dictionary<string, object?>
        {
            ["_rowId"] = r.RowId.ToString(),
            [s.GoalCol.ToString()] = r.GoalText,
            [s.BaselineCol.ToString()] = r.Baseline
        }).ToArray();
        var doc = new Dictionary<string, object> { [s.GoalsFieldKey.ToString()] = rowsArray };
        return JsonSerializer.Serialize(doc);
    }

    private async Task SetValuesAndFinalizeAsync(Scenario s, string valuesJson)
    {
        using (var ctx = CreateContext())
        {
            var instance = await ctx.DocumentInstances.FirstAsync(i => i.Id == s.InstanceId);
            instance.ValuesJson = valuesJson;
            await ctx.SaveChangesAsync();
        }
        using var finalizeCtx = CreateContext();
        var result = await CreateAuthoredService(finalizeCtx).FinalizeAsync(s.InstanceId, s.TeacherId);
        Assert.True(result.Success, result.Message);
    }

    // ---------------------------------------------------------------- Finalize projection

    [Fact]
    public async Task FinalizeAsync_ProjectsNewGoalRowAsActive()
    {
        var s = Seed(nameof(FinalizeAsync_ProjectsNewGoalRowAsActive));
        var rowId = Guid.NewGuid();

        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using var ctx = CreateContext();
        var record = await ctx.GoalRecords.SingleAsync(g => g.SchoolStudentId == s.StudentId);
        Assert.Equal(GoalRecordStatus.Active, record.Status);
        Assert.Equal(rowId, record.LineageId);
        Assert.Equal("Read at grade level", record.GoalText);
        Assert.Equal("42 wpm", record.Baseline);
    }

    [Fact]
    public async Task FinalizeAsync_CarriedLineage_PriorRecordMarkedCarried()
    {
        var s = Seed(nameof(FinalizeAsync_CarriedLineage_PriorRecordMarkedCarried));
        var rowId = Guid.NewGuid();

        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "55 wpm")));

        using var ctx = CreateContext();
        var records = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).OrderBy(g => g.AuthoredDocumentVersion.VersionNumber).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.Equal(GoalRecordStatus.Carried, records[0].Status);
        Assert.Equal(GoalRecordStatus.Active, records[1].Status);
        Assert.Equal(rowId, records[0].LineageId);
        Assert.Equal(rowId, records[1].LineageId);
    }

    [Fact]
    public async Task GetForStudentAsync_CarriedGoal_KeepsTheLineagesObservationsOnItsTrajectory()
    {
        var s = Seed(nameof(GetForStudentAsync_CarriedGoal_KeepsTheLineagesObservationsOnItsTrajectory));
        var rowId = Guid.NewGuid();
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using (var ctx = CreateContext())
        {
            var firstId = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).Select(g => g.Id).SingleAsync();
            Assert.True((await CreateGoalService(ctx).AddObservationAsync(s.TeacherId, firstId, new CreateGoalObservationModel { Value = 45m })).Success);
            Assert.True((await CreateGoalService(ctx).AddObservationAsync(s.TeacherId, firstId, new CreateGoalObservationModel { Value = 50m })).Success);
        }

        // Re-finalize (an annual review / amendment) carries the goal into a new record.
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "50 wpm")));

        using var verify = CreateContext();
        var result = await CreateGoalService(verify).GetForStudentAsync(s.TeacherId, s.StudentId);
        Assert.True(result.Success, result.Message);
        var goal = Assert.Single(result.Data!); // only the current record is listed …
        Assert.Equal(GoalRecordStatus.Active, goal.Status);
        Assert.Equal(2, goal.Observations.Count); // … but it keeps the lineage's progress history
        Assert.False(goal.Trajectory.InsufficientData);
        Assert.NotNull(goal.LastObservedAt);
    }

    [Fact]
    public async Task FinalizeAsync_DroppedLineageWithRecordedReason_PriorRecordRetiredWithReason()
    {
        var s = Seed(nameof(FinalizeAsync_DroppedLineageWithRecordedReason_PriorRecordRetiredWithReason));
        var rowId = Guid.NewGuid();

        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using (var ctx = CreateContext())
        {
            var retirement = await CreateGoalService(ctx).RecordRetirementAsync(s.TeacherId, s.InstanceId, new CreateGoalRetirementModel
            {
                LineageId = rowId,
                Reason = "Goal met early; replaced by a new target"
            });
            Assert.True(retirement.Success, retirement.Message);
        }

        // Second finalize: the goals table is now empty (row removed after the reason was recorded).
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s));

        using var readCtx = CreateContext();
        var record = await readCtx.GoalRecords.SingleAsync(g => g.SchoolStudentId == s.StudentId);
        Assert.Equal(GoalRecordStatus.Retired, record.Status);
        Assert.Equal("Goal met early; replaced by a new target", record.StatusReason);
    }

    [Fact]
    public async Task FinalizeAsync_DroppedLineageWithoutRecordedReason_PriorRecordRetiredWithDefaultReason()
    {
        var s = Seed(nameof(FinalizeAsync_DroppedLineageWithoutRecordedReason_PriorRecordRetiredWithDefaultReason));
        var rowId = Guid.NewGuid();

        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));
        // Second finalize drops the row with NO GoalRetirement recorded first — finalize must not block.
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s));

        using var ctx = CreateContext();
        var record = await ctx.GoalRecords.SingleAsync(g => g.SchoolStudentId == s.StudentId);
        Assert.Equal(GoalRecordStatus.Retired, record.Status);
        Assert.Equal("Removed from the document", record.StatusReason);
    }

    // ---------------------------------------------------------------- Observations

    [Fact]
    public async Task AddObservationAsync_NoValueOrNote_Fails()
    {
        var s = Seed(nameof(AddObservationAsync_NoValueOrNote_Fails));
        var rowId = Guid.NewGuid();
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using var ctx = CreateContext();
        var goalId = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).Select(g => g.Id).SingleAsync();

        var result = await CreateGoalService(ctx).AddObservationAsync(s.TeacherId, goalId, new CreateGoalObservationModel());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task AddObservationAsync_NoteOnly_Succeeds()
    {
        var s = Seed(nameof(AddObservationAsync_NoteOnly_Succeeds));
        var rowId = Guid.NewGuid();
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using var ctx = CreateContext();
        var goalId = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).Select(g => g.Id).SingleAsync();

        var result = await CreateGoalService(ctx).AddObservationAsync(s.TeacherId, goalId, new CreateGoalObservationModel { Note = "Made good progress today." });

        Assert.True(result.Success, result.Message);
    }

    [Fact]
    public async Task GetForStudentAsync_FewerThanTwoNumericObservations_TrajectoryIsInsufficientData()
    {
        var s = Seed(nameof(GetForStudentAsync_FewerThanTwoNumericObservations_TrajectoryIsInsufficientData));
        var rowId = Guid.NewGuid();
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        int goalId;
        using (var ctx = CreateContext())
        {
            goalId = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).Select(g => g.Id).SingleAsync();
            var addResult = await CreateGoalService(ctx).AddObservationAsync(s.TeacherId, goalId, new CreateGoalObservationModel { Value = 45m });
            Assert.True(addResult.Success, addResult.Message);
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateGoalService(ctx).GetForStudentAsync(s.TeacherId, s.StudentId);
            Assert.True(result.Success, result.Message);
            var goal = Assert.Single(result.Data!);
            Assert.True(goal.Trajectory.InsufficientData);
            Assert.Single(goal.Trajectory.Points);
        }

        // A second numeric point flips insufficient-data to false.
        using (var ctx = CreateContext())
        {
            var addResult = await CreateGoalService(ctx).AddObservationAsync(s.TeacherId, goalId, new CreateGoalObservationModel { Value = 50m });
            Assert.True(addResult.Success, addResult.Message);
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateGoalService(ctx).GetForStudentAsync(s.TeacherId, s.StudentId);
            var goal = Assert.Single(result.Data!);
            Assert.False(goal.Trajectory.InsufficientData);
            Assert.Equal(2, goal.Trajectory.Points.Count);
        }
    }

    // ---------------------------------------------------------------- Status updates

    [Fact]
    public async Task UpdateStatusAsync_NotMetWithoutReason_Fails()
    {
        var s = Seed(nameof(UpdateStatusAsync_NotMetWithoutReason_Fails));
        var rowId = Guid.NewGuid();
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using var ctx = CreateContext();
        var goalId = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).Select(g => g.Id).SingleAsync();

        var result = await CreateGoalService(ctx).UpdateStatusAsync(s.TeacherId, goalId, new UpdateGoalStatusModel { Status = GoalRecordStatus.NotMet });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateStatusAsync_NotMetWithReason_Succeeds()
    {
        var s = Seed(nameof(UpdateStatusAsync_NotMetWithReason_Succeeds));
        var rowId = Guid.NewGuid();
        await SetValuesAndFinalizeAsync(s, BuildGoalsJson(s, (rowId, "Read at grade level", "42 wpm")));

        using var ctx = CreateContext();
        var goalId = await ctx.GoalRecords.Where(g => g.SchoolStudentId == s.StudentId).Select(g => g.Id).SingleAsync();

        var result = await CreateGoalService(ctx).UpdateStatusAsync(s.TeacherId, goalId, new UpdateGoalStatusModel
        {
            Status = GoalRecordStatus.NotMet,
            Reason = "Progress stalled; team will revise the approach."
        });

        Assert.True(result.Success, result.Message);
        Assert.Equal(GoalRecordStatus.NotMet, result.Data!.Status);
        Assert.Equal("Progress stalled; team will revise the approach.", result.Data.StatusReason);
    }

    public void Dispose() => _connection.Dispose();
}
