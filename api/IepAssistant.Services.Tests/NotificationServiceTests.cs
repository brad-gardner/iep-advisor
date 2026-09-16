using System.Linq;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Covers <see cref="NotificationService"/>'s dedup window and read/unread mechanics (plan 4, decision 3).</summary>
public sealed class NotificationServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private NotificationService CreateService() => new(_db.Context());

    [Fact]
    public async Task NotifyAsync_SecondCallSameKeyWithin24h_IsDeduped()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingScheduled, "Title", "Body", "/meetings/1", "meeting-1-0-MeetingScheduled", emailImmediately: true);
        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingScheduled, "Title", "Body", "/meetings/1", "meeting-1-0-MeetingScheduled", emailImmediately: true);

        using var ctx = _db.Context();
        var count = ctx.Set<Notification>().Count(n => n.UserId == userId && n.Kind == NotificationKind.MeetingScheduled);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task NotifyAsync_DifferentDedupKey_IsNotDeduped()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingUpdated, "Title", "Body", "/meetings/1", "meeting-1-0-MeetingUpdated", emailImmediately: true);
        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingUpdated, "Title", "Body", "/meetings/1", "meeting-1-1-MeetingUpdated", emailImmediately: true);

        using var ctx = _db.Context();
        var count = ctx.Set<Notification>().Count(n => n.UserId == userId && n.Kind == NotificationKind.MeetingUpdated);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task NotifyAsync_SameKeyOutsideWindow_IsNotDeduped()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff3@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "Title", "Body", null, "same-key", emailImmediately: false);

        // Back-date the existing row past the 24h dedup window, then notify again with the same key.
        using (var ctx = _db.Context())
        {
            var existing = await ctx.Set<Notification>().SingleAsync(n => n.UserId == userId);
            existing.CreatedAt = DateTime.UtcNow.Add(-NotificationService.DedupWindow).AddMinutes(-1);
            await ctx.SaveChangesAsync();
        }

        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "Title", "Body", null, "same-key", emailImmediately: false);

        using var assertCtx = _db.Context();
        var count = assertCtx.Set<Notification>().Count(n => n.UserId == userId && n.DedupKey == "same-key");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task NotifyAsync_EmailImmediately_SetsEmailQueuedAt()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff4@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingCancelled, "Title", "Body", "/meetings/9", "k1", emailImmediately: true);

        using var ctx = _db.Context();
        var row = await ctx.Set<Notification>().SingleAsync(n => n.UserId == userId);
        Assert.NotNull(row.EmailQueuedAt);
    }

    [Fact]
    public async Task NotifyAsync_NotEmailImmediately_LeavesEmailQueuedAtNull()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff5@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "Title", "Body", null, "k2", emailImmediately: false);

        using var ctx = _db.Context();
        var row = await ctx.Set<Notification>().SingleAsync(n => n.UserId == userId);
        Assert.Null(row.EmailQueuedAt);
    }

    [Fact]
    public async Task GetForUserAsync_UnreadOnly_FiltersReadRows()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff6@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "A", "Body", null, "k3", emailImmediately: false);
        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "B", "Body", null, "k4", emailImmediately: false);

        var all = await service.GetForUserAsync(userId, unreadOnly: false, limit: 50);
        var toRead = all.Data!.Items.First();
        await service.MarkReadAsync(userId, toRead.Id);

        var unread = await service.GetForUserAsync(userId, unreadOnly: true, limit: 50);
        Assert.Single(unread.Data!.Items);
        Assert.DoesNotContain(unread.Data.Items, n => n.Id == toRead.Id);
        Assert.Equal(1, unread.Data.UnreadCount);
    }

    [Fact]
    public async Task MarkAllReadAsync_MarksEveryUnreadRowForThatUserOnly()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff7@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var (otherUserId, _) = _db.Staff("staff8@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "A", "Body", null, "k5", emailImmediately: false);
        await service.NotifyAsync(new[] { userId }, NotificationKind.Generic, "B", "Body", null, "k6", emailImmediately: false);
        await service.NotifyAsync(new[] { otherUserId }, NotificationKind.Generic, "C", "Body", null, "k7", emailImmediately: false);

        var result = await service.MarkAllReadAsync(userId);

        Assert.Equal(2, result.Data);
        var otherUnread = await service.GetForUserAsync(otherUserId, unreadOnly: true, limit: 50);
        Assert.Single(otherUnread.Data!.Items);
    }

    [Fact]
    public async Task GetFailuresAsync_ReturnsOnlyRowsWithAnEmailError()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var (userId, _) = _db.Staff("staff9@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var service = CreateService();

        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingReminder, "A", "Body", "/meetings/1", "k8", emailImmediately: true);
        await service.NotifyAsync(new[] { userId }, NotificationKind.MeetingReminder, "B", "Body", "/meetings/2", "k9", emailImmediately: true);

        using (var ctx = _db.Context())
        {
            var failed = await ctx.Set<Notification>().FirstAsync(n => n.DedupKey == "k8");
            failed.EmailError = "boom";
            await ctx.SaveChangesAsync();
        }

        var failures = await service.GetFailuresAsync(200);
        Assert.Single(failures.Data!);
        Assert.Equal("boom", failures.Data![0].EmailError);
    }

    public void Dispose() => _db.Dispose();
}
