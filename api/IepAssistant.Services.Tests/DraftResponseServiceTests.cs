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
/// Plan 6 deliverable A/E: per-item family responses. A parent may respond only on the currently Active
/// revision; staff resolve with a reply and/or a "resolved in the draft" flag (at least one required);
/// creating a response notifies the student's active team + the sharer, and resolving notifies the parent.
/// </summary>
public sealed class DraftResponseServiceTests : IDisposable
{
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DraftResponseServiceTests()
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
        public List<(List<int> UserIds, NotificationKind Kind, string? LinkPath)> Calls { get; } = new();

        public Task NotifyAsync(IEnumerable<int> userIds, NotificationKind kind, string title, string body, string? linkPath, string dedupKey, bool emailImmediately, CancellationToken ct = default)
        {
            Calls.Add((userIds.ToList(), kind, linkPath));
            return Task.CompletedTask;
        }

        public Task<ServiceResult<NotificationListModel>> GetForUserAsync(int userId, bool unreadOnly, int limit, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult> MarkReadAsync(int userId, int notificationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult<int>> MarkAllReadAsync(int userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ServiceResult<List<NotificationModel>>> GetFailuresAsync(int maxCount, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private (DraftResponseService Service, FakeNotifications Notifications) CreateService(ApplicationDbContext ctx)
    {
        var notifications = new FakeNotifications();
        return (new DraftResponseService(ctx, new AccessService(ctx), new OrgAccessService(ctx), notifications, NullLogger<DraftResponseService>.Instance), notifications);
    }

    private sealed record Scenario(int InstanceId, int StudentId, int TeacherId, int ParentId);

    private int SeedRevision(string prefix, SharedDraftStatus status, out Scenario scenario)
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
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan" };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = teacher.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = student.Id, UserId = teacher.Id, TeamRole = TeamRole.CaseManager, IsLead = true, IsActive = true });
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = teacher.Id, Role = AccessRole.Collaborator, IsActive = true });

        var child = new ChildProfile { UserId = parent.Id, FirstName = "Jordan" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.ChildLinks.Add(new ChildLink { ChildProfileId = child.Id, SchoolStudentId = student.Id, IsActive = true, AcceptedAt = DateTime.UtcNow, LinkedAt = DateTime.UtcNow, InviteExpiresAt = DateTime.UtcNow.AddDays(14) });
        ctx.SaveChanges();

        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { version } });
        ctx.SaveChanges();

        var instance = new DocumentInstance
        {
            SchoolStudentId = student.Id, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft, ValuesJson = "{}", RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();

        var revision = new SharedDraftRevision
        {
            DocumentInstanceId = instance.Id, RevisionNumber = 1, ValuesJson = "{}",
            DocumentTemplateVersionId = version.Id, SharedByUserId = teacher.Id, SharedAt = DateTime.UtcNow, Status = status
        };
        ctx.SharedDraftRevisions.Add(revision);
        ctx.SaveChanges();

        scenario = new Scenario(instance.Id, student.Id, teacher.Id, parent.Id);
        return revision.Id;
    }

    [Fact]
    public async Task Create_OnActiveRevision_Succeeds_AndNotifiesTeamAndSharer()
    {
        var revisionId = SeedRevision("active", SharedDraftStatus.Active, out var s);
        using var ctx = CreateContext();
        var (service, notifications) = CreateService(ctx);

        var result = await service.CreateAsync(s.ParentId, revisionId, new CreateDraftResponseModel { Kind = DraftResponseKind.Question, Text = "Can we add OT?" }, default);

        Assert.True(result.Success, result.Message);
        Assert.Equal(DraftResponseStatus.Open, result.Data!.Status);
        Assert.Contains(notifications.Calls, c => c.Kind == NotificationKind.ResponseReceived && c.UserIds.Contains(s.TeacherId));
    }

    [Fact]
    public async Task Create_OnNonActiveRevision_Fails()
    {
        var supersededId = SeedRevision("superseded", SharedDraftStatus.Superseded, out var s1);
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var result = await service.CreateAsync(s1.ParentId, supersededId, new CreateDraftResponseModel { Kind = DraftResponseKind.Agree, Text = "OK" }, default);
            Assert.False(result.Success);
            Assert.Contains("active", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        var withdrawnId = SeedRevision("withdrawn", SharedDraftStatus.Withdrawn, out var s2);
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var result = await service.CreateAsync(s2.ParentId, withdrawnId, new CreateDraftResponseModel { Kind = DraftResponseKind.Agree, Text = "OK" }, default);
            Assert.False(result.Success);
        }
    }

    [Fact]
    public async Task Resolve_RequiresAReplyOrTheResolvedInDraftFlag()
    {
        var revisionId = SeedRevision("resolve", SharedDraftStatus.Active, out var s);
        int responseId;
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var created = await service.CreateAsync(s.ParentId, revisionId, new CreateDraftResponseModel { Kind = DraftResponseKind.ChangeRequest, Text = "Please add OT." }, default);
            Assert.True(created.Success, created.Message);
            responseId = created.Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var empty = await service.ResolveAsync(s.TeacherId, responseId, new ResolveDraftResponseModel(), default);
            Assert.False(empty.Success);
            Assert.Contains("reply", empty.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var (service, notifications) = CreateService(ctx);
            var resolved = await service.ResolveAsync(s.TeacherId, responseId, new ResolveDraftResponseModel { ResolvedInDraft = true }, default);
            Assert.True(resolved.Success, resolved.Message);
            Assert.Equal(DraftResponseStatus.Resolved, resolved.Data!.Status);
            var resolvedNote = Assert.Single(notifications.Calls, c => c.Kind == NotificationKind.DraftResponseResolved && c.UserIds.Contains(s.ParentId));
            Assert.StartsWith("/children/", resolvedNote.LinkPath); // child-scoped parent route
            Assert.EndsWith($"/shared-drafts/{resolved.Data.RevisionId}", resolvedNote.LinkPath);
        }
    }

    [Fact]
    public async Task Resolve_WithOnlyAStaffReply_Succeeds()
    {
        var revisionId = SeedRevision("reply", SharedDraftStatus.Active, out var s);
        int responseId;
        using (var ctx = CreateContext())
        {
            var (service, _) = CreateService(ctx);
            var created = await service.CreateAsync(s.ParentId, revisionId, new CreateDraftResponseModel { Kind = DraftResponseKind.Question, Text = "Why 30 minutes?" }, default);
            responseId = created.Data!.Id;
        }

        using var ctx2 = CreateContext();
        var (service2, _) = CreateService(ctx2);
        var resolved = await service2.ResolveAsync(s.TeacherId, responseId, new ResolveDraftResponseModel { StaffReply = "Based on the evaluation results." }, default);
        Assert.True(resolved.Success, resolved.Message);
        Assert.Equal("Based on the evaluation results.", resolved.Data!.StaffReply);
    }

    public void Dispose() => _connection.Dispose();
}
