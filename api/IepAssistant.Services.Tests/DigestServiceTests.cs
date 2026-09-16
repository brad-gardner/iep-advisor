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

/// <summary>Covers <see cref="DigestService"/>'s content (DueSoon + Overdue + meetings in 7 days) and
/// per-date idempotence (plan 4, decision 3).</summary>
public sealed class DigestServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();
    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    private DigestService CreateService(ApplicationDbContext ctx, IEmailService email)
        => new(ctx, new ObligationService(ctx, new OrgAccessService(ctx)), email, EmptyConfig, NullLogger<DigestService>.Instance);

    [Fact]
    public async Task RunForDateAsync_IncludesDueSoonOverdueAndMeetingsInNext7Days()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");

        var overdueStudentId = _db.Student(schoolId, "Overdue", "Student");
        var dueSoonStudentId = _db.Student(schoolId, "DueSoon", "Student");
        var upcomingStudentId = _db.Student(schoolId, "Upcoming", "Student");
        var (leadUserId, _) = _db.Staff("digestlead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher, first: "Lee", last: "Case");
        _db.TeamMember(overdueStudentId, leadUserId, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(dueSoonStudentId, leadUserId, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(upcomingStudentId, leadUserId, TeamRole.CaseManager, isLead: true);

        using (var ctx = _db.Context())
        {
            ctx.SchoolStudents.Single(s => s.Id == overdueStudentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-5);
            ctx.SchoolStudents.Single(s => s.Id == dueSoonStudentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(10);
            ctx.SchoolStudents.Single(s => s.Id == upcomingStudentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(200);
            ctx.SaveChanges();
        }

        var meetingId = _db.Meeting(overdueStudentId, leadUserId, DateTime.UtcNow.AddDays(3));
        _db.MeetingParticipant(meetingId, leadUserId);
        // A meeting outside the 7-day window must not appear in the digest.
        var farMeetingId = _db.Meeting(dueSoonStudentId, leadUserId, DateTime.UtcNow.AddDays(20));
        _db.MeetingParticipant(farMeetingId, leadUserId);

        var email = new CapturingDigestEmailService();
        using var ctx2 = _db.Context();
        await CreateService(ctx2, email).RunForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.NotNull(email.LastModel);
        Assert.Equal(2, email.LastModel!.Obligations.Count); // overdue + duesoon, not the far-future one
        Assert.Contains(email.LastModel.Obligations, o => o.Status == ObligationStatus.Overdue);
        Assert.Contains(email.LastModel.Obligations, o => o.Status == ObligationStatus.DueSoon);
        Assert.Single(email.LastModel.UpcomingMeetings); // only the 3-day-out meeting, not the 20-day-out one

        using var assertCtx = _db.Context();
        var notification = await assertCtx.Set<Notification>().SingleAsync(n => n.UserId == leadUserId && n.Kind == NotificationKind.ObligationDigest);
        Assert.NotNull(notification.EmailSentAt);
        Assert.Null(notification.EmailError);
    }

    [Fact]
    public async Task RunForDateAsync_NoObligationsOrMeetings_SendsNothing()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Quiet", "Student");
        var (leadUserId, _) = _db.Staff("quiet@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);
        // No dates set (Unknown, not DueSoon/Overdue) and no meetings -> nothing to report.

        var email = new CapturingDigestEmailService();
        using var ctx = _db.Context();
        await CreateService(ctx, email).RunForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Null(email.LastModel);
        using var assertCtx = _db.Context();
        Assert.False(await assertCtx.Set<Notification>().AnyAsync(n => n.UserId == leadUserId));
    }

    [Fact]
    public async Task RunForDateAsync_CalledTwiceForSameDate_IsIdempotent()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("twice@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);
        using (var ctx = _db.Context())
        {
            ctx.SchoolStudents.Single(s => s.Id == studentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
            ctx.SaveChanges();
        }

        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var email = new CapturingDigestEmailService();
        using (var ctx = _db.Context())
            await CreateService(ctx, email).RunForDateAsync(date);
        using (var ctx = _db.Context())
            await CreateService(ctx, email).RunForDateAsync(date);

        using var assertCtx = _db.Context();
        var count = await assertCtx.Set<Notification>().CountAsync(n => n.UserId == leadUserId && n.Kind == NotificationKind.ObligationDigest);
        Assert.Equal(1, count);
        Assert.Equal(1, email.SendCount);
    }

    [Fact]
    public async Task RunForDateAsync_EmailServiceThrows_RecordsEmailErrorOnTheNotification()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("failing@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);
        using (var ctx = _db.Context())
        {
            ctx.SchoolStudents.Single(s => s.Id == studentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
            ctx.SaveChanges();
        }

        var email = new CapturingDigestEmailService { ThrowOnSend = true };
        using var ctx2 = _db.Context();
        await CreateService(ctx2, email).RunForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        using var assertCtx = _db.Context();
        var notification = await assertCtx.Set<Notification>().SingleAsync(n => n.UserId == leadUserId && n.Kind == NotificationKind.ObligationDigest);
        Assert.Null(notification.EmailSentAt);
        Assert.NotNull(notification.EmailError);
    }

    [Fact]
    public async Task RunForDateAsync_TwentyStaffWithContent_UsesBatchedQueriesNotPerStaffRoundTrips()
    {
        // todos/067: obligations, meetings and the dedup check must each be one query for the whole run,
        // not one (or several) per staff user — otherwise 20 staff would be ~40-140 round trips.
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");

        for (var i = 0; i < 20; i++)
        {
            var (leadUserId, _) = _db.Staff($"digeststaff{i}@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
            var studentId = _db.Student(schoolId, "Student", i.ToString());
            _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);
            using (var ctx = _db.Context())
            {
                ctx.SchoolStudents.Single(s => s.Id == studentId).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
                ctx.SaveChanges();
            }
            var meetingId = _db.Meeting(studentId, leadUserId, DateTime.UtcNow.AddDays(2));
            _db.MeetingParticipant(meetingId, leadUserId);
        }

        var counter = new DbActivityCounter();
        var email = new CapturingDigestEmailService();
        using var ctx2 = _db.Context(counter);
        await CreateService(ctx2, email).RunForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        Assert.Equal(20, email.SendCount);
        // staff ids + dedup check + obligations + meetings + user lookup = 5 SELECTs total, regardless of
        // staff count.
        Assert.True(counter.Queries < 10, $"Queries = {counter.Queries}");
        // One batched insert (notification rows) + one batched update (email outcomes), not one pair per user.
        Assert.True(counter.SaveChanges <= 3, $"SaveChanges = {counter.SaveChanges}");
    }

    [Fact]
    public async Task RunForDateAsync_SchoolAdminDigest_IncludesWholeSchoolScope_NotJustLedStudents()
    {
        // Pass-2 review: the batched digest must keep GetMineAsync's admin superset — an admin sees
        // overdue obligations for students they do not personally case-manage.
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var otherSchoolId = _db.School(districtId, "School B");
        var (adminUserId, _) = _db.Staff("admin@example.com", districtId, schoolId, Models.OrgRoleIds.SchoolAdmin);
        var (teacherUserId, _) = _db.Staff("teacher@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        var ledByTeacher = _db.Student(schoolId, "Led", "ByTeacher");
        _db.TeamMember(ledByTeacher, teacherUserId, TeamRole.CaseManager, isLead: true);
        var elsewhere = _db.Student(otherSchoolId, "Other", "School");
        using (var ctx = _db.Context())
        {
            foreach (var id in new[] { ledByTeacher, elsewhere })
                ctx.SchoolStudents.Single(s => s.Id == id).AnnualReviewDueDate = DateTime.UtcNow.Date.AddDays(-1);
            ctx.SaveChanges();
        }

        var email = new CapturingDigestEmailService();
        using var ctx2 = _db.Context();
        await CreateService(ctx2, email).RunForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow));

        // Teacher (lead) and admin (scope) both get a digest; the admin's covers the school, not district.
        Assert.Equal(2, email.SendCount);
        Assert.Single(ctx2.Notifications.Where(n => n.UserId == adminUserId && n.Kind == NotificationKind.ObligationDigest));
        var adminDigest = email.ByRecipient["admin@example.com"];
        Assert.Contains(adminDigest.Obligations, o => o.StudentName == "Led ByTeacher");
        Assert.DoesNotContain(adminDigest.Obligations, o => o.StudentName == "Other School");
    }

    private sealed class CapturingDigestEmailService : TestSupport.TestEmailServiceBase
    {
        public DigestEmailModel? LastModel { get; private set; }
        public Dictionary<string, DigestEmailModel> ByRecipient { get; } = new();
        public int SendCount { get; private set; }
        public bool ThrowOnSend { get; set; }

        public override Task SendDigestAsync(string toEmail, DigestEmailModel model, CancellationToken ct = default)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("simulated ACS failure");
            LastModel = model;
            ByRecipient[toEmail] = model;
            SendCount++;
            return Task.CompletedTask;
        }
    }

    public void Dispose() => _db.Dispose();
}
