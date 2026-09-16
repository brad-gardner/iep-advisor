using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Plan 6 deliverable A/E: deliberate whole-draft sharing. A share freezes the value-document — later
/// edits stay invisible to the family until a deliberate re-share, which computes a semantic diff;
/// withdraw and district policy gate the action; recipients are family + student; acknowledgement is
/// idempotent and visible to staff; a pending/revoked family link denies parent reads.
/// </summary>
public sealed class DraftSharingServiceTests : IDisposable
{
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public DraftSharingServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    internal sealed class FakeNotifications : INotificationService
    {
        public List<(List<int> UserIds, NotificationKind Kind, string Title, string Body, string? LinkPath, string DedupKey)> Calls { get; } = new();

        public Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, string title, string body, string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default)
        {
            Calls.Add((userIds.ToList(), kind, title, body, linkPath, dedupKey));
            return Task.CompletedTask;
        }

        public Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private (DraftSharingService Sharing, DraftResponseService Responses, FakeNotifications Notifications) Services(ApplicationDbContext ctx)
    {
        var org = new OrgAccessService(ctx);
        var access = new AccessService(ctx);
        var authoring = new TemplateAuthoringService(ctx, _audit, NullLogger<TemplateAuthoringService>.Instance);
        var notifications = new FakeNotifications();
        var responses = new DraftResponseService(ctx, access, org, notifications, NullLogger<DraftResponseService>.Instance);
        var sharing = new DraftSharingService(ctx, org, access, authoring, notifications, responses, _audit, NullLogger<DraftSharingService>.Instance);
        return (sharing, responses, notifications);
    }

    private sealed record Scenario(
        int InstanceId, int StudentId, int TeacherId, int ParentId, int ChildId, int StudentUserId,
        Guid GoalsKey, Guid GoalCol, Guid BaselineCol, Guid PlaafpKey, Guid RowId);

    private Scenario Seed(string prefix, bool policyEnabled = true, bool linkAccepted = true, bool familyLinked = true)
    {
        using var ctx = CreateContext();
        var teacher = new User { Email = $"{prefix}-t@example.com", PasswordHash = "x", FirstName = "Steph", LastName = "Case", Role = UserRole.Educator };
        var parent = new User { Email = $"{prefix}-p@example.com", PasswordHash = "x", FirstName = "Dana", LastName = "Parent", Role = UserRole.Parent };
        var studentUser = new User { Email = $"{prefix}-su@example.com", PasswordHash = "x", FirstName = "Jordan", LastName = "Ellis", Role = UserRole.Student };
        ctx.Users.AddRange(teacher, parent, studentUser);
        ctx.SaveChanges();

        var district = new District { Name = prefix, FamilyDraftSharingEnabled = policyEnabled };
        ctx.Districts.Add(district);
        ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = prefix };
        ctx.Schools.Add(school);
        ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = teacher.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan", LastName = "Ellis" };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = teacher.Id, Role = AccessRole.Collaborator, IsActive = true });

        var child = new ChildProfile { UserId = parent.Id, FirstName = "Jordan" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        if (familyLinked)
        {
            ctx.ChildLinks.Add(new ChildLink
            {
                ChildProfileId = child.Id,
                SchoolStudentId = student.Id,
                IsActive = true,
                AcceptedAt = linkAccepted ? DateTime.UtcNow : null,
                LinkedAt = linkAccepted ? DateTime.UtcNow : null,
                InviteExpiresAt = DateTime.UtcNow.AddDays(14)
            });
            ctx.StudentProfiles.Add(new StudentProfile { UserId = studentUser.Id, SchoolStudentId = student.Id });
        }
        ctx.SaveChanges();

        var plaafp = Guid.NewGuid();
        var goals = Guid.NewGuid();
        var goalCol = Guid.NewGuid();
        var baselineCol = Guid.NewGuid();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { version } });
        ctx.SaveChanges();
        ctx.TemplateSections.AddRange(
            new TemplateSection
            {
                DocumentTemplateVersionId = version.Id, SectionKey = Guid.NewGuid(), Title = "Profile", DisplayOrder = 0,
                Fields = { new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = plaafp, FieldType = FieldType.RichText, Label = "Present Levels", DisplayOrder = 0, ConfigJson = TemplateGraphBuilder.RichTextConfig(FieldSemantics.PresentLevels) } }
            },
            new TemplateSection
            {
                DocumentTemplateVersionId = version.Id, SectionKey = Guid.NewGuid(), Title = "Goals", DisplayOrder = 1,
                Fields = { new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = goals, FieldType = FieldType.Table, Label = "Goals", DisplayOrder = 0,
                    ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals,
                        (goalCol, FieldType.Text, "Goal", ColumnSemantics.GoalText),
                        (baselineCol, FieldType.Text, "Baseline", ColumnSemantics.Baseline)) } }
            });
        ctx.SaveChanges();

        var rowId = Guid.NewGuid();
        var values = new Dictionary<string, object>
        {
            [plaafp.ToString()] = "Reads 42 wpm.",
            [goals.ToString()] = new[] { new Dictionary<string, object> { ["_rowId"] = rowId.ToString(), [goalCol.ToString()] = "Read better", [baselineCol.ToString()] = "42 wpm" } }
        };
        var instance = new DocumentInstance
        {
            SchoolStudentId = student.Id, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft, ValuesJson = JsonSerializer.Serialize(values), RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();

        return new Scenario(instance.Id, student.Id, teacher.Id, parent.Id, child.Id, studentUser.Id, goals, goalCol, baselineCol, plaafp, rowId);
    }

    [Fact]
    public async Task Share_FreezesSnapshot_EditsAfterAreInvisible_ReshareShowsChange()
    {
        var s = Seed("share");
        FakeNotifications notifications;
        SharedDraftRevisionModel firstShare;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            notifications = services.Notifications;
            var result = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, "Please review", default);
            Assert.True(result.Success, result.Message);
            firstShare = result.Data!;
        }

        Assert.Equal(1, firstShare.RevisionNumber);
        Assert.Equal(SharedDraftStatus.Active, firstShare.Status);
        Assert.Null(firstShare.ChangeSummary); // first share — nothing to diff against
        Assert.Contains(notifications.Calls, c => c.Kind == NotificationKind.DraftShared && c.UserIds.Contains(s.ParentId));
        Assert.Contains(notifications.Calls, c => c.Kind == NotificationKind.DraftShared && c.UserIds.Contains(s.StudentUserId));
        // Links use the child-scoped parent route keyed on the revision ID (not the per-instance number).
        var parentShare = notifications.Calls.First(c => c.Kind == NotificationKind.DraftShared && c.UserIds.Contains(s.ParentId));
        Assert.Equal($"/children/{s.ChildId}/shared-drafts/{firstShare.Id}", parentShare.LinkPath);

        // Staff edits the live instance after sharing.
        using (var editCtx = CreateContext())
        {
            var instance = editCtx.DocumentInstances.Single(i => i.Id == s.InstanceId);
            var values = JsonNode.Parse(instance.ValuesJson)!.AsObject();
            values[s.GoalsKey.ToString()] = new JsonArray(new JsonObject
            {
                ["_rowId"] = s.RowId.ToString(),
                [s.GoalCol.ToString()] = "Read MUCH better",
                [s.BaselineCol.ToString()] = "50 wpm"
            });
            instance.ValuesJson = values.ToJsonString();
            editCtx.SaveChanges();
        }

        // The parent's frozen view still shows the ORIGINAL text.
        using (var readCtx = CreateContext())
        {
            var services = Services(readCtx);
            var detail = await services.Sharing.GetForParentAsync(s.ParentId, firstShare.Id, default);
            Assert.True(detail.Success, detail.Message);
            Assert.Contains("Read better", detail.Data!.ValuesJson);
            Assert.DoesNotContain("MUCH better", detail.Data.ValuesJson);
        }

        // Re-share: the previous revision is Superseded, and the new one carries the change summary.
        SharedDraftRevisionModel reshare;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var result = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(result.Success, result.Message);
            reshare = result.Data!;
        }
        Assert.Equal(2, reshare.RevisionNumber);
        Assert.NotNull(reshare.ChangeSummary);
        Assert.Contains(reshare.ChangeSummary!.ChangedRows, r => r.RowId == s.RowId.ToString());

        using (var verifyCtx = CreateContext())
        {
            var previous = verifyCtx.SharedDraftRevisions.Single(r => r.Id == firstShare.Id);
            Assert.Equal(SharedDraftStatus.Superseded, previous.Status);
        }
    }

    [Fact]
    public async Task Share_WhenPolicyDisabled_RefusesButPreviewStillListsRecipients()
    {
        var s = Seed("policyoff", policyEnabled: false);
        using var ctx = CreateContext();
        var services = Services(ctx);

        var shareResult = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
        Assert.False(shareResult.Success);
        Assert.Contains("disabled", shareResult.Message, StringComparison.OrdinalIgnoreCase);

        var preview = await services.Sharing.PreviewRecipientsAsync(s.TeacherId, s.InstanceId, default);
        Assert.True(preview.Success, preview.Message);
        Assert.False(preview.Data!.PolicyEnabled);
        Assert.NotEmpty(preview.Data.Recipients);
    }

    [Fact]
    public async Task PreviewRecipients_ListsFamilyAndStudent_AndTracksLastShareState()
    {
        var s = Seed("preview");
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var preview = await services.Sharing.PreviewRecipientsAsync(s.TeacherId, s.InstanceId, default);
            Assert.True(preview.Success, preview.Message);
            Assert.Equal(2, preview.Data!.Recipients.Count);
            Assert.Contains(preview.Data.Recipients, r => r.UserId == s.ParentId && r.Relationship == "Parent");
            Assert.Contains(preview.Data.Recipients, r => r.UserId == s.StudentUserId && r.Relationship == "Student");
            Assert.Null(preview.Data.LastSharedAt);
            Assert.Null(preview.Data.WillSupersedeRevision);
        }

        int revisionNumber;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(share.Success, share.Message);
            revisionNumber = share.Data!.RevisionNumber;
        }

        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var preview2 = await services.Sharing.PreviewRecipientsAsync(s.TeacherId, s.InstanceId, default);
            Assert.NotNull(preview2.Data!.LastSharedAt);
            Assert.Equal(revisionNumber, preview2.Data.WillSupersedeRevision);
        }
    }

    [Fact]
    public async Task Withdraw_MarksWithdrawn_NotifiesRecipients_AndRejectsASecondWithdraw()
    {
        var s = Seed("withdraw");
        int revisionId;
        FakeNotifications notifications;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            notifications = services.Notifications;
            var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(share.Success, share.Message);
            revisionId = share.Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var withdrawn = await services.Sharing.WithdrawAsync(s.TeacherId, s.InstanceId, revisionId, default);
            Assert.True(withdrawn.Success, withdrawn.Message);
            Assert.Equal(SharedDraftStatus.Withdrawn, withdrawn.Data!.Status);
            Assert.Contains(services.Notifications.Calls, c => c.Kind == NotificationKind.DraftShared && c.Title.Contains("withdrawn", StringComparison.OrdinalIgnoreCase));

            var again = await services.Sharing.WithdrawAsync(s.TeacherId, s.InstanceId, revisionId, default);
            Assert.False(again.Success);
        }

        // History stays readable to the family — the revision is flagged, not hidden.
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var detail = await services.Sharing.GetForParentAsync(s.ParentId, revisionId, default);
            Assert.True(detail.Success, detail.Message);
            Assert.Equal(SharedDraftStatus.Withdrawn, detail.Data!.Status);
            Assert.NotNull(detail.Data.WithdrawnAt);

            var list = await services.Sharing.ListForParentAsync(s.ParentId, s.ChildId, default);
            Assert.True(list.Success, list.Message);
            Assert.Contains(list.Data!, r => r.Id == revisionId && r.Status == SharedDraftStatus.Withdrawn);
        }
    }

    [Fact]
    public async Task Lists_AttributeAcknowledgementsAndOpenCounts_ToTheRightRevision()
    {
        var s = Seed("lists");
        int rev1, rev2;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            rev1 = (await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default)).Data!.Id;
            // Parent responds twice on rev 1 and acknowledges it, then rev 2 supersedes it.
            Assert.True((await services.Responses.CreateAsync(s.ParentId, rev1, new CreateDraftResponseModel { Kind = DraftResponseKind.Question, Text = "Why?" }, default)).Success);
            Assert.True((await services.Responses.CreateAsync(s.ParentId, rev1, new CreateDraftResponseModel { Kind = DraftResponseKind.Comment, Text = "Ok." }, default)).Success);
            Assert.True((await services.Sharing.AcknowledgeAsync(s.ParentId, rev1, default)).Success);
            rev2 = (await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, "again", default)).Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var staff = await services.Sharing.ListForInstanceAsync(s.TeacherId, s.InstanceId, default);
            Assert.True(staff.Success, staff.Message);
            var staffRev1 = Assert.Single(staff.Data!, r => r.Id == rev1);
            var staffRev2 = Assert.Single(staff.Data!, r => r.Id == rev2);
            Assert.Equal(2, staffRev1.OpenResponseCount);
            Assert.Single(staffRev1.Acknowledgements);
            Assert.Equal(0, staffRev2.OpenResponseCount);
            Assert.Empty(staffRev2.Acknowledgements);

            var parent = await services.Sharing.ListForParentAsync(s.ParentId, s.ChildId, default);
            Assert.True(parent.Success, parent.Message);
            Assert.NotNull(Assert.Single(parent.Data!, r => r.Id == rev1).AcknowledgedAt);
            Assert.Null(Assert.Single(parent.Data!, r => r.Id == rev2).AcknowledgedAt);
            Assert.Equal(2, parent.Data!.Single(r => r.Id == rev1).OpenResponseCount);
        }
    }

    [Fact]
    public async Task Share_SchoolOnlyStudent_RefusesWithNoRecipients_ButPreviewStillWorks()
    {
        var s = Seed("school-only", familyLinked: false);
        using var ctx = CreateContext();
        var services = Services(ctx);

        var preview = await services.Sharing.PreviewRecipientsAsync(s.TeacherId, s.InstanceId, default);
        Assert.True(preview.Success, preview.Message);
        Assert.Empty(preview.Data!.Recipients);

        var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
        Assert.False(share.Success);
        Assert.Contains("no family recipients", share.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(services.Notifications.Calls);
    }

    [Fact]
    public async Task Acknowledge_IsIdempotent_AndVisibleToStaff()
    {
        var s = Seed("ack");
        int revisionId;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(share.Success, share.Message);
            revisionId = share.Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var ack1 = await services.Sharing.AcknowledgeAsync(s.ParentId, revisionId, default);
            Assert.True(ack1.Success, ack1.Message);
            Assert.NotNull(ack1.Data!.AcknowledgedAt);
        }

        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var ack2 = await services.Sharing.AcknowledgeAsync(s.ParentId, revisionId, default);
            Assert.True(ack2.Success, ack2.Message);
        }

        using (var ctx = CreateContext())
        {
            Assert.Equal(1, ctx.DraftAcknowledgements.Count()); // idempotent upsert, not a duplicate row
        }

        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var staffList = await services.Sharing.ListForInstanceAsync(s.TeacherId, s.InstanceId, default);
            Assert.True(staffList.Success, staffList.Message);
            var rev = staffList.Data!.Single(r => r.Id == revisionId);
            Assert.Single(rev.Acknowledgements);
            Assert.Null(rev.AcknowledgedAt); // staff view never carries "this caller's" stamp
        }
    }

    [Fact]
    public async Task Parent_WithPendingLink_CannotReadTheSharedDraft()
    {
        var s = Seed("pending", linkAccepted: false);
        int revisionId;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(share.Success, share.Message); // the student account alone is a valid recipient
            revisionId = share.Data!.Id;
        }

        using var readCtx = CreateContext();
        var readServices = Services(readCtx);
        var denied = await readServices.Sharing.GetForParentAsync(s.ParentId, revisionId, default);
        Assert.False(denied.Success);
        Assert.Contains("permission", denied.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parent_RevokedLink_CannotReadTheSharedDraft()
    {
        var s = Seed("revoked");
        int revisionId;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(share.Success, share.Message);
            revisionId = share.Data!.Id;

            var link = ctx.ChildLinks.Single(l => l.SchoolStudentId == s.StudentId);
            link.IsActive = false;
            ctx.SaveChanges();
        }

        using var readCtx = CreateContext();
        var readServices = Services(readCtx);
        var denied = await readServices.Sharing.GetForParentAsync(s.ParentId, revisionId, default);
        Assert.False(denied.Success);
        Assert.Contains("permission", denied.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parent_UnknownRevision_ReturnsNotFoundStyleMessage()
    {
        var s = Seed("nf");
        using var ctx = CreateContext();
        var services = Services(ctx);
        var result = await services.Sharing.GetForParentAsync(s.ParentId, 999_999, default);
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetForParent_StaysWithinAQueryBudget()
    {
        var s = Seed("bounds");
        int revisionId;
        using (var ctx = CreateContext())
        {
            var services = Services(ctx);
            var share = await services.Sharing.ShareAsync(s.TeacherId, s.InstanceId, null, default);
            Assert.True(share.Success, share.Message);
            revisionId = share.Data!.Id;
        }

        var counter = new DbActivityCounter();
        using var ctx2 = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).AddInterceptors(counter).Options);
        var boundedServices = Services(ctx2);
        var result = await boundedServices.Sharing.GetForParentAsync(s.ParentId, revisionId, default);
        Assert.True(result.Success, result.Message);
        Assert.True(counter.Queries <= 8, $"GetForParentAsync issued {counter.Queries} queries");
    }

    public void Dispose() => _connection.Dispose();
}
