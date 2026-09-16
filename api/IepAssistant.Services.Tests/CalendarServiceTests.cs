using System.Text;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Covers <see cref="CalendarService"/>'s feed token lifecycle and ICS export authorization (plan 4, decision 4).</summary>
public sealed class CalendarServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private CalendarService CreateService(ApplicationDbContext ctx)
    {
        var orgAccess = new OrgAccessService(ctx);
        var notifications = new NotificationService(ctx);
        var meetingService = new MeetingService(ctx, orgAccess, new AccessService(ctx), notifications, new CapturingAuditLogger(), Microsoft.Extensions.Logging.Abstractions.NullLogger<MeetingService>.Instance);
        var obligationService = new ObligationService(ctx, orgAccess);
        return new CalendarService(ctx, meetingService, obligationService, orgAccess, new IcsBuilder());
    }

    [Fact]
    public async Task GetOrCreateFeedAsync_FirstCall_IssuesAToken()
    {
        var userId = _db.SeedUser("feeduser@example.com", UserRole.Educator);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetOrCreateFeedAsync(userId);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Data!.Token));
        Assert.Equal(32, result.Data.Token.Length);
    }

    [Fact]
    public async Task GetOrCreateFeedAsync_SecondCall_ReturnsSameToken()
    {
        var userId = _db.SeedUser("feeduser2@example.com", UserRole.Educator);

        using var ctx1 = _db.Context();
        var first = await CreateService(ctx1).GetOrCreateFeedAsync(userId);

        using var ctx2 = _db.Context();
        var second = await CreateService(ctx2).GetOrCreateFeedAsync(userId);

        Assert.Equal(first.Data!.Token, second.Data!.Token);
    }

    [Fact]
    public async Task RegenerateFeedAsync_IssuesNewToken_AndRevokesTheOldOne()
    {
        var userId = _db.SeedUser("feeduser3@example.com", UserRole.Educator);

        using var ctx1 = _db.Context();
        var original = await CreateService(ctx1).GetOrCreateFeedAsync(userId);

        using var ctx2 = _db.Context();
        var regenerated = await CreateService(ctx2).RegenerateFeedAsync(userId);

        Assert.NotEqual(original.Data!.Token, regenerated.Data!.Token);

        using var feedCtx = _db.Context();
        var service = CreateService(feedCtx);

        var oldTokenResult = await service.GetFeedByTokenAsync(original.Data.Token);
        Assert.False(oldTokenResult.Success);

        var newTokenResult = await service.GetFeedByTokenAsync(regenerated.Data.Token);
        Assert.True(newTokenResult.Success);
    }

    [Fact]
    public async Task GetFeedByTokenAsync_InvalidToken_Fails()
    {
        using var ctx = _db.Context();
        var result = await CreateService(ctx).GetFeedByTokenAsync("not-a-real-token");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetFeedByTokenAsync_IncludesScheduledMeetingAndObligation()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (userId, _) = _db.Staff("feedstaff@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, userId, TeamRole.CaseManager, isLead: true);
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(2));
        _db.MeetingParticipant(meetingId, userId);
        using (var ctx = _db.Context())
        {
            ctx.SchoolStudents.Single(s => s.Id == studentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(20);
            ctx.SaveChanges();
        }

        using var feedCtx = _db.Context();
        var service = CreateService(feedCtx);
        var feed = await service.GetOrCreateFeedAsync(userId);

        using var readCtx = _db.Context();
        var icsResult = await CreateService(readCtx).GetFeedByTokenAsync(feed.Data!.Token);

        Assert.True(icsResult.Success);
        var ics = Encoding.UTF8.GetString(icsResult.Data!);
        Assert.Contains($"UID:meeting-{meetingId}@iep-advisor", ics);
        Assert.Contains("VALUE=DATE:", ics);
    }

    [Fact]
    public async Task GetMeetingIcsAsync_Participant_CanDownload()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("icscreator@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var meetingService = new MeetingService(ctx, new OrgAccessService(ctx), new AccessService(ctx), new NotificationService(ctx), new CapturingAuditLogger(), Microsoft.Extensions.Logging.Abstractions.NullLogger<MeetingService>.Instance);
            var created = await meetingService.CreateAsync(creatorUserId, studentId, new Models.CreateMeetingModel { Type = MeetingType.AnnualReview, Title = "AR", StartsAtUtc = DateTime.UtcNow.AddDays(3), DurationMinutes = 60 });
            meetingId = created.Data!.Id;
        }

        using var ctx2 = _db.Context();
        var result = await CreateService(ctx2).GetMeetingIcsAsync(creatorUserId, meetingId);

        Assert.True(result.Success);
        Assert.Contains($"UID:meeting-{meetingId}@iep-advisor", Encoding.UTF8.GetString(result.Data!));
    }

    [Fact]
    public async Task GetMeetingIcsAsync_Stranger_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("icscreator2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var meetingService = new MeetingService(ctx, new OrgAccessService(ctx), new AccessService(ctx), new NotificationService(ctx), new CapturingAuditLogger(), Microsoft.Extensions.Logging.Abstractions.NullLogger<MeetingService>.Instance);
            var created = await meetingService.CreateAsync(creatorUserId, studentId, new Models.CreateMeetingModel { Type = MeetingType.AnnualReview, Title = "AR", StartsAtUtc = DateTime.UtcNow.AddDays(3), DurationMinutes = 60 });
            meetingId = created.Data!.Id;
        }

        var (strangerUserId, _) = _db.Staff("icsstranger@example.com", districtId, _db.School(districtId, "School B"), Models.OrgRoleIds.Teacher);

        using var ctx2 = _db.Context();
        var result = await CreateService(ctx2).GetMeetingIcsAsync(strangerUserId, meetingId);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetFeedByTokenAsync_DistrictAdmin_ExcludesObligationsForStudentsWhereNotLead()
    {
        // todos/066: the anonymous, non-expiring feed token must never carry an admin's whole scope of
        // obligations — only ones where the admin is personally the lead case manager.
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (adminUserId, _) = _db.Staff("districtadmin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);

        var leadStudentId = _db.Student(schoolId, "Lead", "Student");
        _db.TeamMember(leadStudentId, adminUserId, TeamRole.CaseManager, isLead: true);
        using (var ctx = _db.Context())
        {
            ctx.SchoolStudents.Single(s => s.Id == leadStudentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(10);
            ctx.SaveChanges();
        }

        // A second student in the admin's district/scope where the admin is NOT the lead.
        var otherStudentId = _db.Student(schoolId, "Other", "Student");
        var (otherLeadUserId, _) = _db.Staff("otherlead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(otherStudentId, otherLeadUserId, TeamRole.CaseManager, isLead: true);
        using (var ctx = _db.Context())
        {
            ctx.SchoolStudents.Single(s => s.Id == otherStudentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(10);
            ctx.SaveChanges();
        }

        using var feedCtx = _db.Context();
        var feed = await CreateService(feedCtx).GetOrCreateFeedAsync(adminUserId);

        using var readCtx = _db.Context();
        var icsResult = await CreateService(readCtx).GetFeedByTokenAsync(feed.Data!.Token);

        Assert.True(icsResult.Success);
        var ics = Encoding.UTF8.GetString(icsResult.Data!);
        Assert.Contains("Lead", ics);
        Assert.DoesNotContain("Other", ics);

        // Sanity check: the authenticated GET /api/educator/obligations/mine equivalent keeps giving the
        // admin their full scope (both students) — only the anonymous feed is narrowed.
        var obligationService = new ObligationService(readCtx, new OrgAccessService(readCtx));
        var mine = await obligationService.GetMineAsync(adminUserId, null);
        Assert.Contains(mine.Data!, o => o.StudentName.Contains("Other"));
    }

    public void Dispose() => _db.Dispose();
}
