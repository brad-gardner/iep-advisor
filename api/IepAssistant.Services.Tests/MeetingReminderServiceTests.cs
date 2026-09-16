using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Covers <see cref="MeetingReminderService"/>'s T-7d/T-1d/T-1h windows and idempotence (plan 4, decision 6).</summary>
public sealed class MeetingReminderServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private MeetingReminderService CreateService(Domain.Data.ApplicationDbContext ctx)
        => new(ctx, new NotificationService(ctx), NullLogger<MeetingReminderService>.Instance);

    private (int districtId, int schoolId, int studentId, int userId) SeedMeetingParticipant()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (userId, _) = _db.Staff("participant@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        return (districtId, schoolId, studentId, userId);
    }

    [Fact]
    public async Task RunOnceAsync_MeetingSoon_SendsEveryReminderWhoseWindowHasOpened()
    {
        // A meeting only 30 minutes away has already crossed the 7-day, 1-day AND 1-hour thresholds — a
        // first (or resumed-after-downtime) run catches up on all of them at once rather than only firing
        // the most-imminent one, since a window that already passed can never be "waited for" later.
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddMinutes(30));
        _db.MeetingParticipant(meetingId, userId);

        using var ctx = _db.Context();
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        using var assertCtx = _db.Context();
        var offsets = assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId).AsEnumerable().Select(r => r.Offset).ToHashSet();
        Assert.Equal(new HashSet<ReminderOffset> { ReminderOffset.SevenDays, ReminderOffset.OneDay, ReminderOffset.OneHour }, offsets);

        var notificationCount = await assertCtx.Set<Notification>().CountAsync(n => n.UserId == userId && n.Kind == NotificationKind.MeetingReminder);
        Assert.Equal(3, notificationCount);
    }

    [Fact]
    public async Task RunOnceAsync_WithinSevenDayAndOneDayWindows_ButNotOneHour()
    {
        // 12 hours out is within both the 7-day AND the 1-day window, but NOT the 1-hour window.
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddHours(12));
        _db.MeetingParticipant(meetingId, userId);

        using var ctx = _db.Context();
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        using var assertCtx = _db.Context();
        var offsets = assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId).AsEnumerable().Select(r => r.Offset).ToHashSet();
        Assert.Equal(new HashSet<ReminderOffset> { ReminderOffset.SevenDays, ReminderOffset.OneDay }, offsets);
    }

    [Fact]
    public async Task RunOnceAsync_TooFarInTheFuture_SendsNoReminderYet()
    {
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(10));
        _db.MeetingParticipant(meetingId, userId);

        using var ctx = _db.Context();
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        using var assertCtx = _db.Context();
        Assert.Empty(assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId));
    }

    [Fact]
    public async Task RunOnceAsync_RunTwice_IsIdempotent_OneRowPerOffset()
    {
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddMinutes(30));
        _db.MeetingParticipant(meetingId, userId);

        var utcNow = DateTime.UtcNow;
        using (var ctx = _db.Context())
            await CreateService(ctx).RunOnceAsync(utcNow);
        using (var ctx = _db.Context())
            await CreateService(ctx).RunOnceAsync(utcNow);

        using var assertCtx = _db.Context();
        var reminders = assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId).ToList();
        Assert.Equal(3, reminders.Count); // one per offset (SevenDays/OneDay/OneHour), not doubled by the second run

        var notificationCount = await assertCtx.Set<Notification>().CountAsync(n => n.UserId == userId && n.Kind == NotificationKind.MeetingReminder);
        Assert.Equal(3, notificationCount);
    }

    [Fact]
    public async Task RunOnceAsync_CancelledMeeting_SendsNoReminder()
    {
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddMinutes(30), status: MeetingStatus.Cancelled);
        _db.MeetingParticipant(meetingId, userId);

        using var ctx = _db.Context();
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        using var assertCtx = _db.Context();
        Assert.Empty(assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId));
    }

    [Fact]
    public async Task RunOnceAsync_PastMeeting_SendsNoReminder()
    {
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddMinutes(-5));
        _db.MeetingParticipant(meetingId, userId);

        using var ctx = _db.Context();
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        using var assertCtx = _db.Context();
        Assert.Empty(assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId));
    }

    [Fact]
    public async Task RunOnceAsync_ExternalParticipantWithNoUser_IsSkipped()
    {
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddMinutes(30));
        _db.MeetingParticipant(meetingId, userId: null, externalName: "External Guest", externalEmail: "guest@example.com");

        using var ctx = _db.Context();
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        using var assertCtx = _db.Context();
        Assert.Empty(assertCtx.MeetingReminders.Where(r => r.MeetingId == meetingId));
    }

    [Fact]
    public async Task RunOnceAsync_TenMeetingsAlreadyFullySent_UsesBatchedQueriesRegardlessOfMeetingCount()
    {
        // todos/069: participants + already-sent reminders must be loaded once per run (not once per
        // (meeting, offset)), and a pair that is already fully sent must cost nothing beyond that —
        // otherwise 10 meetings x 3 open offsets would keep re-querying both every 15-minute tick forever.
        var (_, _, studentId, userId) = SeedMeetingParticipant();
        var meetingIds = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddMinutes(30 + i));
            _db.MeetingParticipant(meetingId, userId);
            meetingIds.Add(meetingId);
        }
        using (var seedCtx = _db.Context())
        {
            foreach (var meetingId in meetingIds)
            {
                foreach (var offset in new[] { ReminderOffset.SevenDays, ReminderOffset.OneDay, ReminderOffset.OneHour })
                    seedCtx.MeetingReminders.Add(new MeetingReminder { MeetingId = meetingId, UserId = userId, Offset = offset, SentAt = DateTime.UtcNow });
            }
            seedCtx.SaveChanges();
        }

        var counter = new DbActivityCounter();
        using var ctx = _db.Context(counter);
        await CreateService(ctx).RunOnceAsync(DateTime.UtcNow);

        // Candidates + participants + already-sent reminders = 3 SELECTs total, independent of meeting
        // count, and zero further work since every (meeting, offset) pair is already complete.
        Assert.True(counter.Queries <= 4, $"Queries = {counter.Queries}");
    }

    // ----------------------------------------------------------------- DbUpdateException classification (todos/053)

    [Fact]
    public void IsReminderUniqueIndexCollision_MatchesOnlyTheExpectedIndexName()
    {
        var sqlServer = new DbUpdateException("update failed",
            new InvalidOperationException("Violation of UNIQUE KEY constraint 'IX_MeetingReminders_MeetingId_UserId_Offset'. Cannot insert duplicate key"));
        var unrelated = new DbUpdateException("update failed",
            new InvalidOperationException("SQLite Error 19: 'FOREIGN KEY constraint failed'"));

        Assert.True(MeetingReminderService.IsReminderUniqueIndexCollision(sqlServer));
        Assert.False(MeetingReminderService.IsReminderUniqueIndexCollision(unrelated));
    }

    [Fact]
    public async Task IsReminderUniqueIndexCollision_RecognisesARealSqliteDuplicate()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var meetingId = _db.Meeting(studentId, leadUserId, DateTime.UtcNow.AddDays(1));

        using var ctx = _db.Context();
        ctx.MeetingReminders.Add(new MeetingReminder { MeetingId = meetingId, UserId = leadUserId, Offset = ReminderOffset.OneDay, SentAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();
        ctx.MeetingReminders.Add(new MeetingReminder { MeetingId = meetingId, UserId = leadUserId, Offset = ReminderOffset.OneDay, SentAt = DateTime.UtcNow });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        Assert.True(MeetingReminderService.IsReminderUniqueIndexCollision(ex), ex.InnerException?.Message);
    }

    public void Dispose() => _db.Dispose();
}
