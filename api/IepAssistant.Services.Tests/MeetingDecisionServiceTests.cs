using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 7, decision 3: decisions require a Held/Continued meeting to record; the proposed-edits list for
/// a draft instance includes decisions from meetings linked to that instance directly AND from any
/// meeting for the same student within the last 60 days (never older, never another student); marking a
/// decision applied stamps <c>AppliedAt</c> — the edit itself is always made by a human, never automated.
/// </summary>
public sealed class MeetingDecisionServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private MeetingDecisionService CreateService(ApplicationDbContext ctx) => new(ctx, new OrgAccessService(ctx));

    /// <summary>A minimal Draft DocumentInstance (no template validation needed for these tests).</summary>
    private int SeedMinimalInstance(int studentId, int docTypeId = 1)
    {
        using var ctx = _db.Context();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        var template = new DocumentTemplate { DocumentTypeId = docTypeId, Name = "T", Versions = { version } };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();

        var instance = new DocumentInstance
        {
            SchoolStudentId = studentId,
            DocumentTypeId = docTypeId,
            DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = "{}"
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();
        return instance.Id;
    }

    [Fact]
    public async Task Create_MeetingNotHeldOrContinued_IsRejected()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId);
        var (userId, _) = _db.Staff("cm@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(1), status: MeetingStatus.Scheduled);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).CreateAsync(userId, meetingId, new CreateMeetingDecisionModel
        {
            Text = "Add OT services",
            Outcome = MeetingDecisionOutcome.Agreed
        });
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ProposedEdits_IncludesLinkedInstance_AndSameStudentWithin60Days_ExcludesOlderOrOtherStudent()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId, "Sam");
        var otherStudentId = _db.Student(schoolId, "Other");
        var (userId, _) = _db.Staff("cm3@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);
        _db.Access(otherStudentId, userId, AccessRole.Collaborator);

        var instanceId = SeedMinimalInstance(studentId);

        var linkedMeetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(-10), status: MeetingStatus.Held);
        using (var ctx = _db.Context())
        {
            var m = ctx.Meetings.Single(m => m.Id == linkedMeetingId);
            m.DocumentInstanceId = instanceId;
            ctx.SaveChanges();
        }

        var recentMeetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(-30), status: MeetingStatus.Held);
        var oldMeetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(-90), status: MeetingStatus.Held);
        var otherStudentMeetingId = _db.Meeting(otherStudentId, userId, DateTime.UtcNow.AddDays(-1), status: MeetingStatus.Held);

        using (var ctx = _db.Context())
        {
            var service = CreateService(ctx);
            Assert.True((await service.CreateAsync(userId, linkedMeetingId, new CreateMeetingDecisionModel { Text = "Linked decision", Outcome = MeetingDecisionOutcome.Agreed })).Success);
            Assert.True((await service.CreateAsync(userId, recentMeetingId, new CreateMeetingDecisionModel { Text = "Recent decision", Outcome = MeetingDecisionOutcome.Deferred })).Success);
            Assert.True((await service.CreateAsync(userId, oldMeetingId, new CreateMeetingDecisionModel { Text = "Old decision", Outcome = MeetingDecisionOutcome.Disagreed })).Success);
            Assert.True((await service.CreateAsync(userId, otherStudentMeetingId, new CreateMeetingDecisionModel { Text = "Other student decision", Outcome = MeetingDecisionOutcome.Agreed })).Success);
        }

        using var readCtx = _db.Context();
        var proposed = await CreateService(readCtx).GetProposedEditsForInstanceAsync(userId, instanceId);
        Assert.True(proposed.Success, proposed.Message);
        var texts = proposed.Data!.Select(p => p.Text).ToList();

        Assert.Contains("Linked decision", texts);
        Assert.Contains("Recent decision", texts);
        Assert.DoesNotContain("Old decision", texts);
        Assert.DoesNotContain("Other student decision", texts);
    }

    [Fact]
    public async Task MarkApplied_SetsAppliedAt()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId);
        var (userId, _) = _db.Staff("cm4@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);
        var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(-1), status: MeetingStatus.Held);

        int decisionId;
        using (var ctx = _db.Context())
        {
            var result = await CreateService(ctx).CreateAsync(userId, meetingId, new CreateMeetingDecisionModel { Text = "Adjust goal", Outcome = MeetingDecisionOutcome.Agreed });
            Assert.True(result.Success, result.Message);
            decisionId = result.Data!.Id;
            Assert.Null(result.Data!.AppliedAt);
        }

        using (var ctx = _db.Context())
        {
            var applied = await CreateService(ctx).MarkAppliedAsync(userId, decisionId);
            Assert.True(applied.Success, applied.Message);
            Assert.NotNull(applied.Data!.AppliedAt);
        }

        using var readCtx = _db.Context();
        Assert.NotNull(readCtx.MeetingDecisions.Single(d => d.Id == decisionId).AppliedAt);
    }

    public void Dispose() => _db.Dispose();
}
