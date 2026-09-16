using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 6 deliverable A/E: cached, plain-language explanation of a whole shared revision. One Claude call
/// per revision — cached forever, never regenerated; item citations resolve only against ids that were
/// actually rendered into the draft (unknown ids dropped); usage is billed to the district, never the
/// parent's subscription.
/// </summary>
public sealed class DraftExplanationServiceTests : IDisposable
{
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly FakeClaudeClient _claude = new();

    public DraftExplanationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    internal sealed class FakeClaudeClient : IClaudeClient
    {
        public string? CannedResponse { get; set; }
        public ClaudeCompletionRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            CallCount++;
            return Task.FromResult(CannedResponse);
        }
    }

    private DraftExplanationService CreateService(ApplicationDbContext ctx) =>
        new(ctx, new AccessService(ctx), _claude, NullLogger<DraftExplanationService>.Instance);

    private sealed record Scenario(int RevisionId, int DistrictId, int ParentId, int ChildId, Guid GoalsKey, Guid GoalCol, string RowId);

    private Scenario Seed(string prefix, string? goalText = "Read better")
    {
        using var ctx = CreateContext();
        var teacher = new User { Email = $"{prefix}-t@example.com", PasswordHash = "x", FirstName = "T", LastName = "E", Role = UserRole.Educator };
        var parent = new User { Email = $"{prefix}-p@example.com", PasswordHash = "x", FirstName = "Dana", LastName = "Parent", Role = UserRole.Parent };
        ctx.Users.AddRange(teacher, parent);
        ctx.SaveChanges();

        var district = new District { Name = prefix };
        ctx.Districts.Add(district);
        ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = prefix };
        ctx.Schools.Add(school);
        ctx.SaveChanges();
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan" };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = parent.Id, FirstName = "Jordan" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.ChildLinks.Add(new ChildLink { ChildProfileId = child.Id, SchoolStudentId = student.Id, IsActive = true, AcceptedAt = DateTime.UtcNow, LinkedAt = DateTime.UtcNow, InviteExpiresAt = DateTime.UtcNow.AddDays(14) });
        ctx.SaveChanges();

        var goals = Guid.NewGuid();
        var goalCol = Guid.NewGuid();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { version } });
        ctx.SaveChanges();
        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id, SectionKey = Guid.NewGuid(), Title = "Goals", DisplayOrder = 0,
            Fields = { new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = goals, FieldType = FieldType.Table, Label = "Goals", DisplayOrder = 0,
                ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals, (goalCol, FieldType.Text, "Goal", ColumnSemantics.GoalText)) } }
        });
        ctx.SaveChanges();

        var rowId = Guid.NewGuid();
        var values = new Dictionary<string, object>
        {
            [goals.ToString()] = new[] { new Dictionary<string, object> { ["_rowId"] = rowId.ToString(), [goalCol.ToString()] = goalText! } }
        };
        var instance = new DocumentInstance
        {
            SchoolStudentId = student.Id, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft, ValuesJson = JsonSerializer.Serialize(values), RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();

        var revision = new SharedDraftRevision
        {
            DocumentInstanceId = instance.Id, RevisionNumber = 1, ValuesJson = instance.ValuesJson,
            DocumentTemplateVersionId = version.Id, SharedByUserId = teacher.Id, SharedAt = DateTime.UtcNow, Status = SharedDraftStatus.Active
        };
        ctx.SharedDraftRevisions.Add(revision);
        ctx.SaveChanges();

        return new Scenario(revision.Id, district.Id, parent.Id, child.Id, goals, goalCol, rowId.ToString());
    }

    [Fact]
    public async Task GetOrGenerate_CallsClaudeOnce_ThenServesFromCache()
    {
        var s = Seed("explain");
        _claude.CannedResponse = $$"""
        {"sections": [{"title": "Goals", "explanation": "This section lists what your child will work on."}],
         "items": [{"id": "F:{{s.GoalsKey}}|R:{{s.RowId}}", "explanation": "This goal is about reading better."}]}
        """;

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).GetOrGenerateAsync(s.ParentId, s.RevisionId, default);
            Assert.True(result.Success, result.Message);
            Assert.Single(result.Data!.Sections);
            Assert.Single(result.Data.Items);
            Assert.Equal(s.GoalsKey, result.Data.Items[0].FieldKey);
            Assert.Equal(s.RowId, result.Data.Items[0].RowId);
            Assert.NotEmpty(result.Data.Disclaimer);
        }
        Assert.Equal(1, _claude.CallCount);

        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx).GetOrGenerateAsync(s.ParentId, s.RevisionId, default);
            Assert.True(second.Success, second.Message);
            Assert.Single(second.Data!.Items);
        }
        Assert.Equal(1, _claude.CallCount); // cached — no second Claude call

        using (var ctx = CreateContext())
        {
            Assert.Single(ctx.SharedDraftExplanations);
            var usage = Assert.Single(ctx.UsageRecords);
            Assert.Equal("draft_explanation", usage.OperationType);
            Assert.Equal(s.DistrictId, usage.DistrictId);
            Assert.Equal(s.ChildId, usage.ChildProfileId);
        }
    }

    [Fact]
    public async Task GetOrGenerate_DropsUnknownItemIds()
    {
        var s = Seed("unknown");
        _claude.CannedResponse = """
        {"sections": [], "items": [{"id": "F:11111111-1111-1111-1111-111111111111|R:bogus", "explanation": "forged"}]}
        """;

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetOrGenerateAsync(s.ParentId, s.RevisionId, default);
        // Nothing usable was produced (the only item referenced an unknown id) — service reports unavailable
        // rather than caching an empty/garbage explanation.
        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);

        using var verify = CreateContext();
        Assert.Empty(verify.SharedDraftExplanations);
    }

    [Fact]
    public async Task GetOrGenerate_ClaudeFailure_ReturnsUnavailable_NoPartialCache()
    {
        var s = Seed("fail");
        _claude.CannedResponse = null;

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetOrGenerateAsync(s.ParentId, s.RevisionId, default);
        Assert.False(result.Success);
        Assert.Contains("unavailable", result.Message, StringComparison.OrdinalIgnoreCase);

        using var verify = CreateContext();
        Assert.Empty(verify.SharedDraftExplanations);
        Assert.Empty(verify.UsageRecords);
    }

    [Fact]
    public async Task GetOrGenerate_DeniesAParentWithNoAcceptedLink()
    {
        var s = Seed("deny");
        using var ctx = CreateContext();
        var stranger = new User { Email = "stranger@example.com", PasswordHash = "x", FirstName = "No", LastName = "Access", Role = UserRole.Parent };
        ctx.Users.Add(stranger);
        ctx.SaveChanges();

        var result = await CreateService(ctx).GetOrGenerateAsync(stranger.Id, s.RevisionId, default);
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _connection.Dispose();
}
