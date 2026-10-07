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
/// Plan 6 deliverable A/E: post-meeting family summary. Drafting/sending is gated on the meeting being
/// Held/Continued; a family participant sees only a Sent summary (never an in-progress draft); usage is
/// billed to the district.
/// </summary>
public sealed class MeetingSummaryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public MeetingSummaryServiceTests()
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
        public string? CannedResponse { get; set; } = "The team met and discussed Jordan's reading goal.";
        public ClaudeCompletionRequest? LastRequest { get; private set; }
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(CannedResponse);
        }
    }

    internal sealed class FakeNotifications : INotificationService
    {
        public List<(List<int> UserIds, NotificationKind Kind)> Calls { get; } = new();
        public Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, string title, string body, string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default)
        {
            Calls.Add((userIds.ToList(), kind));
            return Task.CompletedTask;
        }
        public Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private FakeClaudeClient _claude = new();

    private (MeetingSummaryService Service, FakeNotifications Notifications) CreateService(ApplicationDbContext ctx)
    {
        var notifications = new FakeNotifications();
        return (new MeetingSummaryService(ctx, new OrgAccessService(ctx), _claude, notifications, _audit, TestSupport.TestLocalizers.Ai(), NullLogger<MeetingSummaryService>.Instance), notifications);
    }

    private sealed record Scenario(int MeetingId, int DistrictId, int ChildId, int TeacherId, int ParentId, int StudentId);

    private Scenario Seed(string prefix, MeetingStatus status)
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
        ctx.StaffProfiles.Add(new StaffProfile { UserId = teacher.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan" };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = teacher.Id, Role = AccessRole.Collaborator, IsActive = true });

        var child = new ChildProfile { UserId = parent.Id, FirstName = "Jordan" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.ChildLinks.Add(new ChildLink { ChildProfileId = child.Id, SchoolStudentId = student.Id, IsActive = true, AcceptedAt = DateTime.UtcNow, LinkedAt = DateTime.UtcNow, InviteExpiresAt = DateTime.UtcNow.AddDays(14) });
        ctx.SaveChanges();

        var meeting = new Meeting
        {
            SchoolStudentId = student.Id, Type = MeetingType.AnnualReview, Title = "Annual Review",
            StartsAtUtc = DateTime.UtcNow.AddDays(-1), TimeZoneId = "America/New_York", DurationMinutes = 60,
            Status = status, CreatedByUserId = teacher.Id
        };
        ctx.Meetings.Add(meeting);
        ctx.SaveChanges();
        ctx.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = parent.Id, TeamRole = TeamRole.Other, IsFamily = true, RsvpToken = Guid.NewGuid().ToString("N") });
        ctx.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = teacher.Id, TeamRole = TeamRole.CaseManager, RsvpToken = Guid.NewGuid().ToString("N") });
        ctx.SaveChanges();

        return new Scenario(meeting.Id, district.Id, child.Id, teacher.Id, parent.Id, student.Id);
    }

    [Fact]
    public async Task Draft_FailsWhenMeetingIsNotHeldOrContinued()
    {
        var s = Seed("scheduled", MeetingStatus.Scheduled);
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);

        var result = await service.DraftAsync(s.TeacherId, s.MeetingId, default);
        Assert.False(result.Success);
        Assert.Contains("Held", result.Message);
    }

    [Fact]
    public async Task Draft_Edit_Send_Flow_GatesOnHeld_AndNotifiesFamily()
    {
        var s = Seed("flow", MeetingStatus.Held);

        int summaryId;
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var drafted = await service.DraftAsync(s.TeacherId, s.MeetingId, default);
            Assert.True(drafted.Success, drafted.Message);
            Assert.Equal(MeetingSummaryStatus.Draft, drafted.Data!.Status);
            Assert.Equal("The team met and discussed Jordan's reading goal.", drafted.Data.Body);
            summaryId = drafted.Data.Id;
        }

        using (var ctx = CreateContext())
        {
            var usage = Assert.Single(ctx.UsageRecords);
            Assert.Equal("meeting_summary", usage.OperationType);
            Assert.Equal(s.DistrictId, usage.DistrictId);
            Assert.Equal(s.ChildId, usage.ChildProfileId);
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var edited = await service.UpdateAsync(s.TeacherId, s.MeetingId, "Edited: the team discussed reading and OT.", default);
            Assert.True(edited.Success, edited.Message);
            Assert.Equal("Edited: the team discussed reading and OT.", edited.Data!.Body);
            Assert.NotNull(edited.Data.EditedAt);
        }

        using (var ctx = CreateContext())
        {
            var (service, notifications) = CreateService(ctx);
            var sent = await service.SendAsync(s.TeacherId, s.MeetingId, default);
            Assert.True(sent.Success, sent.Message);
            Assert.Equal(MeetingSummaryStatus.Sent, sent.Data!.Status);
            Assert.NotNull(sent.Data.SentAt);
            Assert.Contains(notifications.Calls, c => c.Kind == NotificationKind.MeetingSummarySent && c.UserIds.Contains(s.ParentId));

            // Sending again, or editing/re-drafting after Send, is rejected.
            // Validation (400), not Conflict (409): matches main's status for "already been sent"
            // (multilingual plan 2026-10-06 phase 3 review fix).
            var sentAgain = await service.SendAsync(s.TeacherId, s.MeetingId, default);
            Assert.False(sentAgain.Success);
            Assert.Equal(ServiceErrorKind.Validation, sentAgain.ErrorKind);
            var editAfterSend = await service.UpdateAsync(s.TeacherId, s.MeetingId, "Too late", default);
            Assert.False(editAfterSend.Success);
            Assert.Equal(ServiceErrorKind.Validation, editAfterSend.ErrorKind);
            var draftAfterSend = await service.DraftAsync(s.TeacherId, s.MeetingId, default);
            Assert.False(draftAfterSend.Success);
            Assert.Equal(ServiceErrorKind.Validation, draftAfterSend.ErrorKind);
        }
    }

    [Fact]
    public async Task Send_WithoutADraft_Fails()
    {
        var s = Seed("nodraft", MeetingStatus.Held);
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);
        var result = await service.SendAsync(s.TeacherId, s.MeetingId, default);
        Assert.False(result.Success);
        // Validation (400), not Conflict (409): matches main's status for "No draft summary exists yet"
        // (multilingual plan 2026-10-06 phase 3 review fix).
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Get_FamilyParticipantSeesOnlyASentSummary_StaffSeesTheDraft()
    {
        var s = Seed("visibility", MeetingStatus.Held);
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var drafted = await service.DraftAsync(s.TeacherId, s.MeetingId, default);
            Assert.True(drafted.Success, drafted.Message);
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var staffRead = await service.GetAsync(s.TeacherId, s.MeetingId, default);
            Assert.True(staffRead.Success, staffRead.Message);
            Assert.Equal(MeetingSummaryStatus.Draft, staffRead.Data!.Status);

            var familyRead = await service.GetAsync(s.ParentId, s.MeetingId, default);
            Assert.False(familyRead.Success); // draft in progress — hidden from the family
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var sent = await service.SendAsync(s.TeacherId, s.MeetingId, default);
            Assert.True(sent.Success, sent.Message);
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var familyRead = await service.GetAsync(s.ParentId, s.MeetingId, default);
            Assert.True(familyRead.Success, familyRead.Message);
            Assert.Equal(MeetingSummaryStatus.Sent, familyRead.Data!.Status);
        }
    }

    // ------------------------------------------------------------------ multilingual plan phase 3

    // The summary is family-facing, so it must follow the FAMILY's saved preference — never the drafting
    // staff member's own ambient request culture (multilingual plan 2026-10-06 phase 3 review fix).

    [Fact]
    public async Task Draft_FamilyParticipantPrefersSpanish_GeneratesSpanish_RegardlessOfStaffCulture()
    {
        var s = Seed("lang-es", MeetingStatus.Held);
        using (var ctx = CreateContext())
        {
            var parent = ctx.Users.Single(u => u.Id == s.ParentId);
            parent.PreferredLanguage = "es";
            ctx.SaveChanges();
        }

        using var ctx2 = CreateContext();
        var (service, _) = CreateService(ctx2);

        // The staff member's own ambient culture is left at the default (English) here — the family's
        // saved preference, not the requester's, must still drive the generated language.
        var result = await service.DraftAsync(s.TeacherId, s.MeetingId, default);

        Assert.True(result.Success, result.Message);
        Assert.Equal("es", result.Data!.GeneratedLanguage);
        Assert.Contains("Spanish", _claude.LastRequest!.SystemPrompt);

        var entity = ctx2.MeetingSummaries.Single(m => m.MeetingId == s.MeetingId);
        Assert.Equal("es", entity.Language);
    }

    [Fact]
    public async Task Draft_StaffPrefersSpanishButNoFamilyParticipantDoes_GeneratesEnglish()
    {
        var s = Seed("lang-staff-es", MeetingStatus.Held);
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);

        ServiceResult<FamilyMeetingSummaryModel> result;
        // The DRAFTING STAFF member's ambient culture is Spanish, but the family participant (seeded with
        // no PreferredLanguage) is not — the summary must still come back in English.
        using (IepAssistant.Services.Localization.CultureScope.For("es"))
            result = await service.DraftAsync(s.TeacherId, s.MeetingId, default);

        Assert.True(result.Success, result.Message);
        Assert.Equal("en", result.Data!.GeneratedLanguage);
        Assert.DoesNotContain("RESPONSE LANGUAGE", _claude.LastRequest!.SystemPrompt, StringComparison.OrdinalIgnoreCase);

        var entity = ctx.MeetingSummaries.Single(m => m.MeetingId == s.MeetingId);
        Assert.Equal("en", entity.Language);
    }

    [Fact]
    public async Task Draft_UnderEnglishCulture_CapturesEnglish_NeverAppendsResponseLanguageLine()
    {
        var s = Seed("lang-en", MeetingStatus.Held);
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);

        var result = await service.DraftAsync(s.TeacherId, s.MeetingId, default);

        Assert.True(result.Success, result.Message);
        Assert.Equal("en", result.Data!.GeneratedLanguage);
        Assert.DoesNotContain("RESPONSE LANGUAGE", _claude.LastRequest!.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Draft_NotHeld_UnderSpanishCulture_MessageIsSpanish_ErrorKindIsValidation()
    {
        var s = Seed("lang-notheld", MeetingStatus.Scheduled);
        using var ctx = CreateContext();
        var (service, _) = CreateService(ctx);

        ServiceResult<FamilyMeetingSummaryModel> result;
        using (IepAssistant.Services.Localization.CultureScope.For("es"))
            result = await service.DraftAsync(s.TeacherId, s.MeetingId, default);

        Assert.False(result.Success);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Equal(
            "Esta reunión debe estar en estado Realizada o Continuada antes de poder crear un resumen para la familia.",
            result.Message);
    }

    public void Dispose() => _connection.Dispose();
}
