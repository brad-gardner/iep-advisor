using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Covers <see cref="NotificationEmailService"/>'s drain query, meeting-kind ICS dispatch, and
/// retry/error-recording behavior (plan 4, decision 3).</summary>
public sealed class NotificationEmailServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();
    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    private NotificationEmailService CreateService(ApplicationDbContext ctx, IEmailService email)
        => new(ctx, email, new IcsBuilder(), EmptyConfig, NullLogger<NotificationEmailService>.Instance);

    private int SeedQueuedNotification(int userId, NotificationKind kind = NotificationKind.Generic, string? linkPath = null)
    {
        using var ctx = _db.Context();
        var n = new Notification
        {
            UserId = userId,
            Kind = kind,
            Title = "Title",
            Body = "Body",
            LinkPath = linkPath,
            DedupKey = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            EmailQueuedAt = DateTime.UtcNow
        };
        ctx.Set<Notification>().Add(n);
        ctx.SaveChanges();
        return n.Id;
    }

    [Fact]
    public async Task FindQueuedIdsAsync_ReturnsOnlyQueuedUnsentUnexhaustedRows()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("u1@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);

        var queuedId = SeedQueuedNotification(userId);
        using (var ctx = _db.Context())
        {
            ctx.Set<Notification>().Add(new Notification { UserId = userId, Kind = NotificationKind.Generic, Title = "t", Body = "b", DedupKey = "k-sent", CreatedAt = DateTime.UtcNow, EmailQueuedAt = DateTime.UtcNow, EmailSentAt = DateTime.UtcNow });
            ctx.Set<Notification>().Add(new Notification { UserId = userId, Kind = NotificationKind.Generic, Title = "t", Body = "b", DedupKey = "k-exhausted", CreatedAt = DateTime.UtcNow, EmailQueuedAt = DateTime.UtcNow, EmailAttempts = 3 });
            ctx.Set<Notification>().Add(new Notification { UserId = userId, Kind = NotificationKind.Generic, Title = "t", Body = "b", DedupKey = "k-not-queued", CreatedAt = DateTime.UtcNow });
            ctx.SaveChanges();
        }

        using var readCtx = _db.Context();
        var ids = await CreateService(readCtx, new CapturingEmailService()).FindQueuedIdsAsync();

        Assert.Equal(new[] { queuedId }, ids);
    }

    [Fact]
    public async Task ProcessNotificationAsync_Success_SetsEmailSentAt()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("u2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var id = SeedQueuedNotification(userId);

        var email = new CapturingEmailService();
        using var ctx = _db.Context();
        await CreateService(ctx, email).ProcessNotificationAsync(id);

        using var assertCtx = _db.Context();
        var row = await assertCtx.Set<Notification>().SingleAsync(n => n.Id == id);
        Assert.NotNull(row.EmailSentAt);
        Assert.Null(row.EmailError);
        Assert.Equal(1, email.GenericSendCount);
    }

    [Fact]
    public async Task ProcessNotificationAsync_EmailServiceThrows_RecordsTruncatedErrorAndIncrementsAttempts()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("u3@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var id = SeedQueuedNotification(userId);

        var email = new CapturingEmailService { ThrowMessage = new string('x', 900) };
        using var ctx = _db.Context();
        await CreateService(ctx, email).ProcessNotificationAsync(id);

        using var assertCtx = _db.Context();
        var row = await assertCtx.Set<Notification>().SingleAsync(n => n.Id == id);
        Assert.Null(row.EmailSentAt);
        Assert.Equal(1, row.EmailAttempts);
        Assert.NotNull(row.EmailError);
        Assert.Equal(500, row.EmailError!.Length);
    }

    [Fact]
    public async Task ProcessNotificationAsync_FailsThreeTimes_StopsBeingReturnedByFindQueuedIds()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("u4@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var id = SeedQueuedNotification(userId);

        var email = new CapturingEmailService { ThrowMessage = "boom" };
        for (var i = 0; i < 3; i++)
        {
            using var ctx = _db.Context();
            await CreateService(ctx, email).ProcessNotificationAsync(id);
        }

        using var assertCtx = _db.Context();
        var row = await assertCtx.Set<Notification>().SingleAsync(n => n.Id == id);
        Assert.Equal(3, row.EmailAttempts);
        Assert.Null(row.EmailSentAt);

        using var queryCtx = _db.Context();
        var stillQueued = await CreateService(queryCtx, email).FindQueuedIdsAsync();
        Assert.DoesNotContain(id, stillQueued);

        // A 4th attempt must be a no-op (re-verification guard) — no 4th attempt is recorded.
        using (var ctx = _db.Context())
            await CreateService(ctx, email).ProcessNotificationAsync(id);
        using var finalCtx = _db.Context();
        var finalRow = await finalCtx.Set<Notification>().SingleAsync(n => n.Id == id);
        Assert.Equal(3, finalRow.EmailAttempts);
    }

    [Fact]
    public async Task ProcessNotificationAsync_MeetingScheduled_SendsIcsAttachedInvitation_NotGeneric()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (userId, _) = _db.Staff("u5@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(3));
        _db.MeetingParticipant(meetingId, userId);
        var notificationId = SeedQueuedNotification(userId, NotificationKind.MeetingScheduled, $"/meetings/{meetingId}");

        var email = new CapturingEmailService();
        using var ctx = _db.Context();
        await CreateService(ctx, email).ProcessNotificationAsync(notificationId);

        Assert.Equal(1, email.InvitationSendCount);
        Assert.Equal(0, email.GenericSendCount);
        Assert.NotNull(email.LastIcsBytes);
        Assert.True(email.LastIcsBytes!.Length > 0);
    }

    [Fact]
    public async Task ProcessNotificationAsync_MeetingScheduled_NoMatchingParticipant_FallsBackToGeneric()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (userId, _) = _db.Staff("u6@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(3));
        // No participant row created for userId on this meeting -> the ICS flow can't resolve, must fall back.
        var notificationId = SeedQueuedNotification(userId, NotificationKind.MeetingScheduled, $"/meetings/{meetingId}");

        var email = new CapturingEmailService();
        using var ctx = _db.Context();
        await CreateService(ctx, email).ProcessNotificationAsync(notificationId);

        Assert.Equal(0, email.InvitationSendCount);
        Assert.Equal(1, email.GenericSendCount);

        using var assertCtx = _db.Context();
        var row = await assertCtx.Set<Notification>().SingleAsync(n => n.Id == notificationId);
        Assert.NotNull(row.EmailSentAt);
    }

    [Fact]
    public async Task ProcessNotificationAsync_RecipientHasNoEmail_RecordsErrorWithoutCallingEmailService()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        int userId;
        using (var ctx = _db.Context())
        {
            var u = new User { Email = "", PasswordHash = "x", FirstName = "No", LastName = "Email", Role = UserRole.Educator };
            ctx.Users.Add(u);
            ctx.SaveChanges();
            userId = u.Id;
        }
        var id = SeedQueuedNotification(userId);

        var email = new CapturingEmailService();
        using var ctx2 = _db.Context();
        await CreateService(ctx2, email).ProcessNotificationAsync(id);

        Assert.Equal(0, email.GenericSendCount);
        using var assertCtx = _db.Context();
        var row = await assertCtx.Set<Notification>().SingleAsync(n => n.Id == id);
        Assert.Equal(1, row.EmailAttempts);
        Assert.Contains("no email", row.EmailError, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingEmailService : TestSupport.TestEmailServiceBase
    {
        public int GenericSendCount { get; private set; }
        public int InvitationSendCount { get; private set; }
        public byte[]? LastIcsBytes { get; private set; }
        public string? ThrowMessage { get; set; }

        public override Task SendNotificationAsync(string toEmail, string title, string body, string linkUrl, CancellationToken ct = default)
        {
            if (ThrowMessage != null)
                throw new InvalidOperationException(ThrowMessage);
            GenericSendCount++;
            return Task.CompletedTask;
        }

        public override Task SendMeetingInvitationAsync(string toEmail, MeetingEmailModel model, byte[] ics, CancellationToken ct = default)
        {
            if (ThrowMessage != null)
                throw new InvalidOperationException(ThrowMessage);
            InvitationSendCount++;
            LastIcsBytes = ics;
            return Task.CompletedTask;
        }
    }

    public void Dispose() => _db.Dispose();
}
