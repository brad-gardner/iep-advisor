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

    public void Dispose() => _db.Dispose();
}
