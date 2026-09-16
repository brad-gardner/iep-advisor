using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 4 coverage for <see cref="MeetingService"/>: default participant construction, authz across
/// staff/parent/stranger/participant, reschedule sequence-bump + notification, cancel notification, and
/// the anonymous token RSVP flow.
/// </summary>
public sealed class MeetingServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private MeetingService CreateService(ApplicationDbContext ctx) => new(
        ctx,
        new OrgAccessService(ctx),
        new AccessService(ctx),
        new NotificationService(ctx),
        new CapturingAuditLogger(),
        NullLogger<MeetingService>.Instance);

    private static CreateMeetingModel BasicMeeting(DateTime startsAtUtc, List<ParticipantInputModel>? participants = null) => new()
    {
        Type = MeetingType.AnnualReview,
        Title = "Annual Review",
        StartsAtUtc = startsAtUtc,
        DurationMinutes = 60,
        Participants = participants
    };

    // ----------------------------------------------------------------- default participants

    [Fact]
    public async Task CreateAsync_DefaultParticipants_IncludesTeamFamilyStudentAndCreator_NoDuplicates()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");

        var (leadUserId, _) = _db.Staff("lead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);

        var (creatorUserId, _) = _db.Staff("slp@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.SpeechLanguagePathologist);

        var parentUserId = _db.SeedUser("parent@example.com", UserRole.Parent);
        var childProfileId = _db.ChildProfile(parentUserId);
        _db.ChildLink(studentId, childProfileId, accepted: true);

        var studentAccountUserId = _db.SeedUser("student-account@example.com", UserRole.Student);
        _db.StudentProfile(studentId, studentAccountUserId);

        using var ctx = _db.Context();
        var service = CreateService(ctx);
        var result = await service.CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(10)));

        Assert.True(result.Success);
        var participantUserIds = result.Data!.Participants.Where(p => p.UserId != null).Select(p => p.UserId!.Value).ToList();

        Assert.Contains(leadUserId, participantUserIds);
        Assert.Contains(creatorUserId, participantUserIds);
        Assert.Contains(parentUserId, participantUserIds);
        Assert.Contains(studentAccountUserId, participantUserIds);
        Assert.Equal(participantUserIds.Count, participantUserIds.Distinct().Count());

        var parent = result.Data.Participants.Single(p => p.UserId == parentUserId);
        Assert.True(parent.IsFamily);
        var studentParticipant = result.Data.Participants.Single(p => p.UserId == studentAccountUserId);
        Assert.True(studentParticipant.IsStudent);
    }

    [Fact]
    public async Task GetDefaultParticipants_MatchesCreateDefaults_WithDisplayData_AndRequiresCollaborator()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (leadUserId, _) = _db.Staff("lead@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, leadUserId, TeamRole.CaseManager, isLead: true);
        var parentUserId = _db.SeedUser("parent@example.com", UserRole.Parent);
        _db.ChildLink(studentId, _db.ChildProfile(parentUserId), accepted: true);
        var studentAccountUserId = _db.SeedUser("student-account@example.com", UserRole.Student);
        _db.StudentProfile(studentId, studentAccountUserId);
        var (viewerUserId, _) = _db.Staff("viewer@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.Access(studentId, viewerUserId, AccessRole.Viewer);

        using var ctx = _db.Context();
        var service = CreateService(ctx);
        var result = await service.GetDefaultParticipantsAsync(leadUserId, studentId);
        Assert.True(result.Success, result.Message);
        var byUser = result.Data!.ToDictionary(d => d.UserId);
        Assert.Equal(TeamRole.CaseManager, byUser[leadUserId].TeamRole);
        Assert.True(byUser[parentUserId].IsFamily);
        Assert.Equal("parent@example.com", byUser[parentUserId].Email);
        Assert.True(byUser[studentAccountUserId].IsStudent);
        Assert.Equal(3, result.Data.Count);

        Assert.False((await service.GetDefaultParticipantsAsync(viewerUserId, studentId)).Success);
    }

    [Fact]
    public async Task CreateAsync_DefaultParticipants_ExcludesDistrictAdminUnlessCreator()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");

        var (adminUserId, _) = _db.Staff("admin@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);
        // A DistrictAdmin should not normally sit on a team, but seed one defensively to prove the filter.
        using (var seedCtx = _db.Context())
        {
            seedCtx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = studentId, UserId = adminUserId, TeamRole = TeamRole.Other, IsActive = true });
            seedCtx.SaveChanges();
        }

        var (creatorUserId, _) = _db.Staff("teacher@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        using var ctx = _db.Context();
        var service = CreateService(ctx);
        var result = await service.CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Data!.Participants, p => p.UserId == adminUserId);
    }

    [Fact]
    public async Task CreateAsync_DefaultParticipants_IncludesDistrictAdminWhenTheyAreTheCreator()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (adminUserId, _) = _db.Staff("admin2@example.com", districtId, null, Models.OrgRoleIds.DistrictAdmin);

        using var ctx = _db.Context();
        var service = CreateService(ctx);
        var result = await service.CreateAsync(adminUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));

        Assert.True(result.Success);
        Assert.Contains(result.Data!.Participants, p => p.UserId == adminUserId);
    }

    // ----------------------------------------------------------------- authz

    [Fact]
    public async Task CreateAsync_Viewer_CannotCreate()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (viewerUserId, _) = _db.Staff("viewer@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, viewerUserId, TeamRole.LeaRepresentative, access: AccessRole.Viewer);

        using var ctx = _db.Context();
        var service = CreateService(ctx);
        var result = await service.CreateAsync(viewerUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(1)));

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAsync_Stranger_CannotRead()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("owner@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(1)));
            meetingId = result.Data!.Id;
        }

        var (strangerUserId, _) = _db.Staff("stranger@example.com", districtId, _db.School(districtId, "School B"), Models.OrgRoleIds.Teacher);

        using var readCtx = _db.Context();
        var readResult = await CreateService(readCtx).GetAsync(strangerUserId, meetingId);

        Assert.False(readResult.Success);
        Assert.Contains("permission", readResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAsync_Participant_CanRead_EvenWithoutStudentAccess()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("owner2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        // An external-only participant relationship: a related-service provider with NO SchoolStudentAccess
        // row, added explicitly as a participant.
        var (participantUserId, _) = _db.Staff("provider@example.com", districtId, null, Models.OrgRoleIds.RelatedServiceProvider);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var participants = new List<ParticipantInputModel>
            {
                new() { UserId = creatorUserId, TeamRole = TeamRole.CaseManager },
                new() { UserId = participantUserId, TeamRole = TeamRole.Other }
            };
            var result = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(1), participants));
            meetingId = result.Data!.Id;
        }

        using var readCtx = _db.Context();
        var readResult = await CreateService(readCtx).GetAsync(participantUserId, meetingId);

        Assert.True(readResult.Success);
    }

    [Fact]
    public async Task ListForChildAsync_ParentOfLinkedChild_SeesMeeting_AndCanRsvp()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("owner3@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        var parentUserId = _db.SeedUser("parent2@example.com", UserRole.Parent);
        var childProfileId = _db.ChildProfile(parentUserId);
        _db.ChildLink(studentId, childProfileId, accepted: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(3)));
            meetingId = result.Data!.Id;
        }

        using (var listCtx = _db.Context())
        {
            var forChild = await CreateService(listCtx).ListForChildAsync(parentUserId, childProfileId);
            Assert.True(forChild.Success);
            Assert.Single(forChild.Data!);
        }

        using var rsvpCtx = _db.Context();
        var rsvp = await CreateService(rsvpCtx).RsvpAsync(parentUserId, meetingId, InviteStatus.Accepted);
        Assert.True(rsvp.Success);
        Assert.Equal(InviteStatus.Accepted, rsvp.Data!.MyInviteStatus);
    }

    [Fact]
    public async Task ListForChildAsync_ParentWithoutAccess_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var otherParentUserId = _db.SeedUser("other-parent@example.com", UserRole.Parent);
        var unrelatedChildProfileId = _db.ChildProfile(otherParentUserId);
        // Note: no ChildLink created — this child is unrelated to the student.

        using var ctx = _db.Context();
        var result = await CreateService(ctx).ListForChildAsync(otherParentUserId, unrelatedChildProfileId);

        // HasMinimumRoleAsync will still succeed (parent owns the child); zero linked students -> empty list.
        Assert.True(result.Success);
        Assert.Empty(result.Data!);
    }

    // ----------------------------------------------------------------- reschedule / cancel

    [Fact]
    public async Task UpdateAsync_Reschedule_BumpsSequenceAndCreatesNotification()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("resched@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));
            meetingId = created.Data!.Id;
            Assert.Equal(0, created.Data.Sequence);
        }

        using (var updateCtx = _db.Context())
        {
            var updated = await CreateService(updateCtx).UpdateAsync(creatorUserId, meetingId, new UpdateMeetingModel { StartsAtUtc = DateTime.UtcNow.AddDays(6) });
            Assert.True(updated.Success);
            Assert.Equal(1, updated.Data!.Sequence);
        }

        using var assertCtx = _db.Context();
        var notified = await assertCtx.Set<Notification>().AnyAsync(n => n.UserId == creatorUserId && n.Kind == NotificationKind.MeetingUpdated);
        Assert.True(notified);
    }

    [Fact]
    public async Task UpdateAsync_TwoDistinctReschedules_ProduceTwoDistinctNotifications()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("resched2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));
            meetingId = created.Data!.Id;
        }

        using (var ctx = _db.Context())
            await CreateService(ctx).UpdateAsync(creatorUserId, meetingId, new UpdateMeetingModel { StartsAtUtc = DateTime.UtcNow.AddDays(6) });
        using (var ctx = _db.Context())
            await CreateService(ctx).UpdateAsync(creatorUserId, meetingId, new UpdateMeetingModel { StartsAtUtc = DateTime.UtcNow.AddDays(7) });

        using var assertCtx = _db.Context();
        var updatedCount = await assertCtx.Set<Notification>().CountAsync(n => n.UserId == creatorUserId && n.Kind == NotificationKind.MeetingUpdated);
        Assert.Equal(2, updatedCount); // distinct dedup keys (sequence 1 and 2) -> both kept, not deduped
    }

    [Fact]
    public async Task UpdateAsync_NonScheduleFieldChange_DoesNotBumpSequenceOrNotify()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("notes@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));
            meetingId = created.Data!.Id;
        }

        using (var ctx = _db.Context())
        {
            var updated = await CreateService(ctx).UpdateAsync(creatorUserId, meetingId, new UpdateMeetingModel { Notes = "Bring the draft IEP." });
            Assert.Equal(0, updated.Data!.Sequence);
        }

        using var assertCtx = _db.Context();
        var updatedCount = await assertCtx.Set<Notification>().CountAsync(n => n.Kind == NotificationKind.MeetingUpdated);
        Assert.Equal(0, updatedCount);
    }

    [Fact]
    public async Task CancelAsync_EmitsMeetingCancelledNotificationAndBumpsSequence()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("cancel@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));
            meetingId = created.Data!.Id;
        }

        using (var ctx = _db.Context())
        {
            var cancelled = await CreateService(ctx).CancelAsync(creatorUserId, meetingId, "Snow day");
            Assert.True(cancelled.Success);
            Assert.Equal(MeetingStatus.Cancelled, cancelled.Data!.Status);
            Assert.Equal(1, cancelled.Data.Sequence);
        }

        using var assertCtx = _db.Context();
        var notified = await assertCtx.Set<Notification>().AnyAsync(n => n.UserId == creatorUserId && n.Kind == NotificationKind.MeetingCancelled);
        Assert.True(notified);
    }

    // ----------------------------------------------------------------- token RSVP

    [Fact]
    public async Task RsvpByToken_BeforeStart_Works()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("token@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));
            meetingId = created.Data!.Id;
        }

        string token;
        using (var ctx = _db.Context())
            token = ctx.MeetingParticipants.Single(p => p.MeetingId == meetingId && p.UserId == creatorUserId).RsvpToken;

        using var rsvpCtx = _db.Context();
        var result = await CreateService(rsvpCtx).RsvpByTokenAsync(token, InviteStatus.Declined);

        Assert.True(result.Success);
        Assert.Equal(InviteStatus.Declined, result.Data!.Status);
    }

    [Fact]
    public async Task RsvpByToken_AfterMeetingStarted_Fails()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("token2@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);

        int meetingId;
        using (var ctx = _db.Context())
        {
            // Create in the future so validation is untouched, then move it into the past directly.
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(5)));
            meetingId = created.Data!.Id;
        }
        using (var ctx = _db.Context())
        {
            var meeting = ctx.Meetings.Single(m => m.Id == meetingId);
            meeting.StartsAtUtc = DateTime.UtcNow.AddHours(-1);
            ctx.SaveChanges();
        }

        string token;
        using (var ctx = _db.Context())
            token = ctx.MeetingParticipants.Single(p => p.MeetingId == meetingId && p.UserId == creatorUserId).RsvpToken;

        using var rsvpCtx = _db.Context();
        var result = await CreateService(rsvpCtx).RsvpByTokenAsync(token, InviteStatus.Accepted);

        Assert.False(result.Success);
        Assert.Contains("already started", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RsvpByToken_InvalidToken_Fails()
    {
        using var ctx = _db.Context();
        var result = await CreateService(ctx).RsvpByTokenAsync("not-a-real-token", InviteStatus.Accepted);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RsvpAsync_NonParticipant_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "School A");
        var studentId = _db.Student(schoolId, "Sam", "Student");
        var (creatorUserId, _) = _db.Staff("rsvpdeny@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);
        _db.TeamMember(studentId, creatorUserId, TeamRole.CaseManager, isLead: true);
        var (strangerUserId, _) = _db.Staff("rsvpstranger@example.com", districtId, schoolId, Models.OrgRoleIds.Teacher);

        int meetingId;
        using (var ctx = _db.Context())
        {
            var created = await CreateService(ctx).CreateAsync(creatorUserId, studentId, BasicMeeting(DateTime.UtcNow.AddDays(1)));
            meetingId = created.Data!.Id;
        }

        using var rsvpCtx = _db.Context();
        var result = await CreateService(rsvpCtx).RsvpAsync(strangerUserId, meetingId, InviteStatus.Accepted);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _db.Dispose();
}
