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
/// P6b coverage for educator AI assist + IEP-scoped chat. Uses a hand-written fake
/// <see cref="IClaudeClient"/> that returns a canned string and captures the last request so we can
/// assert what context the prompt builders folded in. Real SQLite in-memory engine, same fixture
/// shape as <see cref="IepDraftServiceTests"/>.
/// </summary>
public sealed class IepAssistServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly FakeClaudeClient _claude = new();
    private readonly CapturingAuditLogger _audit = new();

    public IepAssistServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private IepAssistService CreateService(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), _claude, _audit, NullLogger<IepAssistService>.Instance);

    // ---------------------------------------------------------------- Fake Claude

    private sealed class FakeClaudeClient : IClaudeClient
    {
        public string? CannedResponse { get; set; } = "  CANNED SUGGESTION  ";
        public ClaudeCompletionRequest? LastRequest { get; private set; }

        /// <summary>When set, CompleteAsync throws this kind instead of returning a response.</summary>
        public ClaudeFailureKind? ThrowKind { get; set; }

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            if (ThrowKind is { } kind)
                throw new ClaudeApiException(kind);
            return Task.FromResult(CannedResponse);
        }
    }

    // ---------------------------------------------------------------- Seed helpers

    private sealed record SchoolScenario(int SchoolId, int CollaboratorUserId, int StudentId);

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

        ctx.StaffProfiles.Add(new StaffProfile { UserId = user.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
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

    private int SeedAdditionalUser(int studentId, int schoolId, string emailPrefix, AccessRole role)
    {
        using var ctx = CreateContext();
        var user = new User { Email = $"{emailPrefix}@example.com", PasswordHash = "x", FirstName = "Co", LastName = "U", Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var districtId = ctx.Schools.Where(s => s.Id == schoolId).Select(s => s.DistrictId).Single();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = user.Id, DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher });
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

    private int CreateDraft(SchoolScenario s)
    {
        using var ctx = CreateContext();
        var draft = new IepDraft { SchoolStudentId = s.StudentId, Status = IepDraftStatus.Draft, Title = "2025 Annual", CreatedById = s.CollaboratorUserId };
        ctx.IepDrafts.Add(draft);
        ctx.SaveChanges();
        return draft.Id;
    }

    private int AddGoal(int draftId, string goalText)
    {
        using var ctx = CreateContext();
        var goal = new IepDraftGoal { IepDraftId = draftId, Domain = "Reading", GoalText = goalText, LineageId = Guid.NewGuid() };
        ctx.IepDraftGoals.Add(goal);
        ctx.SaveChanges();
        return goal.Id;
    }

    // ---------------------------------------------------------------- Goal assist

    [Fact]
    public async Task AssistGoal_ReturnsCannedSuggestion_AndIncludesGoalTextAndMeasurableInPrompt()
    {
        var s = SeedSchoolWithStudent("goal-ok");
        var draftId = CreateDraft(s);
        var goalId = AddGoal(draftId, "Read 80 words per minute");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AssistGoalAsync(s.CollaboratorUserId, draftId, goalId, AssistKind.Rewrite);

        Assert.True(result.Success);
        Assert.Equal("CANNED SUGGESTION", result.Data!.Suggestion); // trimmed

        var req = _claude.LastRequest!;
        Assert.Contains("Read 80 words per minute", req.UserText);          // prompt includes the goal text
        Assert.Contains("measurable", req.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IEP", req.SystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1024, req.MaxTokens); // call sites choose the token budget, never the model
    }

    [Fact]
    public async Task AssistGoal_GoalNotInDraft_ReturnsNotFound()
    {
        var s = SeedSchoolWithStudent("goal-other");
        var draftId = CreateDraft(s);

        // Goal belongs to a *different* draft on the same student.
        var otherDraftId = CreateDraft(s);
        var goalInOther = AddGoal(otherDraftId, "elsewhere");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AssistGoalAsync(s.CollaboratorUserId, draftId, goalInOther, AssistKind.Improve);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Access

    [Fact]
    public async Task AssistGoal_AsViewer_PermissionFailure()
    {
        var s = SeedSchoolWithStudent("view-collab");
        var draftId = CreateDraft(s);
        var goalId = AddGoal(draftId, "Read 80 wpm");
        var viewerId = SeedAdditionalUser(s.StudentId, s.SchoolId, "view-viewer", AccessRole.Viewer);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AssistGoalAsync(viewerId, draftId, goalId, AssistKind.Rewrite);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssistGoal_CrossSchool_PermissionFailure()
    {
        var schoolA = SeedSchoolWithStudent("xs-a");
        var schoolB = SeedSchoolWithStudent("xs-b");
        var draftInA = CreateDraft(schoolA);
        var goalInA = AddGoal(draftInA, "A goal");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AssistGoalAsync(schoolB.CollaboratorUserId, draftInA, goalInA, AssistKind.Rewrite);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Chat

    [Fact]
    public async Task Chat_ReturnsCannedReply_AndIncludesDraftContextAndLatestMessage()
    {
        var s = SeedSchoolWithStudent("chat-ok");
        var draftId = CreateDraft(s);
        AddGoal(draftId, "Decode multisyllabic words");

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = "Is the reading goal measurable?" }
        };

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ChatAsync(s.CollaboratorUserId, draftId, messages);

        Assert.True(result.Success);
        Assert.Equal("CANNED SUGGESTION", result.Data!.Reply);

        var req = _claude.LastRequest!;
        Assert.Contains("Decode multisyllabic words", req.SystemPrompt); // draft context folded into system prompt
        Assert.Contains("Is the reading goal measurable?", req.UserText); // latest user message folded into user text
    }

    [Fact]
    public async Task Chat_RecordsViewAudit()
    {
        var s = SeedSchoolWithStudent("chat-audit");
        var draftId = CreateDraft(s);
        _audit.Entries.Clear();

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ChatAsync(s.CollaboratorUserId, draftId,
            new List<ChatMessage> { new() { Role = "user", Content = "hi" } });

        Assert.True(result.Success);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.View, entry.Action);
        Assert.Equal("IepDraft", entry.ResourceType);
        Assert.Equal(draftId, entry.ResourceId);
    }

    // ---------------------------------------------------------------- Claude unavailable

    [Fact]
    public async Task AssistGoal_ClaudeReturnsNull_ReturnsTemporarilyUnavailable()
    {
        var s = SeedSchoolWithStudent("null-goal");
        var draftId = CreateDraft(s);
        var goalId = AddGoal(draftId, "Read 80 wpm");
        _claude.CannedResponse = null;

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AssistGoalAsync(s.CollaboratorUserId, draftId, goalId, AssistKind.Rewrite);

        Assert.False(result.Success);
        Assert.Contains("temporarily unavailable", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chat_ClaudeReturnsNull_ReturnsTemporarilyUnavailable()
    {
        var s = SeedSchoolWithStudent("null-chat");
        var draftId = CreateDraft(s);
        _claude.CannedResponse = null;

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ChatAsync(s.CollaboratorUserId, draftId,
            new List<ChatMessage> { new() { Role = "user", Content = "hi" } });

        Assert.False(result.Success);
        Assert.Contains("temporarily unavailable", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _connection.Dispose();

    // ---------------------------------------------------------------- Claude failure handling

    [Fact]
    public async Task AssistGoal_ClaudeFails_ReturnsUnavailable_NotAnException()
    {
        // This call site had NO try/catch. That was harmless only while the configured model was
        // retired and the feature unreachable; now that it points at a live model, an outage here
        // would otherwise escape as an uncaught 500.
        var s = SeedSchoolWithStudent("assist-fails");
        var draftId = CreateDraft(s);
        var goalId = AddGoal(draftId, "Read 80 words per minute");
        _claude.ThrowKind = ClaudeFailureKind.Transient;

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AssistGoalAsync(s.CollaboratorUserId, draftId, goalId, AssistKind.Rewrite);

        Assert.False(result.Success);
        Assert.Equal("AI assist is temporarily unavailable.", result.Message);
    }

    [Fact]
    public async Task Chat_ClaudeFails_ReturnsUnavailable_NotAnException()
    {
        var s = SeedSchoolWithStudent("chat-fails");
        var draftId = CreateDraft(s);
        _claude.ThrowKind = ClaudeFailureKind.Configuration;

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ChatAsync(
            s.CollaboratorUserId,
            draftId,
            new List<ChatMessage> { new() { Role = "user", Content = "How do I word this goal?" } },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("AI assist is temporarily unavailable.", result.Message);
    }
}
