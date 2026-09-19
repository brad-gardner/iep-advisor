using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Pilot-gates plan, phase 2, decision 3: parent purge deletes the parent's own uploaded-document
/// graph + blobs and their User row, while leaving every school-owned record untouched; staff purge
/// deactivates/anonymises the User row IN PLACE (never deleted) so every school record that references
/// it by id keeps working.
/// </summary>
public sealed class AccountPurgeServiceTests : IDisposable
{
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public AccountPurgeServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class RecordingBlobStorage : IBlobStorageService
    {
        public List<string> DeletedPaths { get; } = new();
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default) => Task.FromResult(blobPath);
        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) { DeletedPaths.Add(blobPath); return Task.CompletedTask; }
        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null) => Task.FromResult(blobPath);
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public List<(AuditAction Action, int ActorUserId, string ResourceType, int ResourceId)> Entries { get; } = new();
        public void Record(AuditAction action, int actorUserId, string resourceType, int resourceId, int? recipientUserId = null)
            => Entries.Add((action, actorUserId, resourceType, resourceId));
    }

    private sealed class RecordingStripe : IStripeAccountCleanup
    {
        public List<string> CancelledSubscriptions { get; } = new();
        public List<string> DeletedCustomers { get; } = new();
        public bool Fail { get; set; }
        public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("stripe down");
            CancelledSubscriptions.Add(subscriptionId); return Task.CompletedTask;
        }
        public Task DeleteCustomerAsync(string customerId, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("stripe down");
            DeletedCustomers.Add(customerId); return Task.CompletedTask;
        }
    }

    private readonly RecordingStripe _stripe = new();

    private AccountPurgeService CreateService(ApplicationDbContext ctx, IBlobStorageService blobs, RecordingAuditLogger audit) =>
        new(ctx, blobs, audit, _stripe, NullLogger<AccountPurgeService>.Instance);

    [Fact]
    public async Task PurgeAsync_Parent_DeletesChildGraphAndBlobs_AndTheUserRow_ButLeavesSchoolRecords()
    {
        int parentId, childId, schoolStudentId, documentInstanceId;
        using (var ctx = CreateContext())
        {
            var parent = new User { Email = "parent@example.com", PasswordHash = "x", FirstName = "Pat", LastName = "Parent", Role = UserRole.Parent, DeletionRequestedAt = DateTime.UtcNow.AddDays(-31), StripeCustomerId = "cus_123", StripeSubscriptionId = "sub_456" };
            ctx.Users.Add(parent);
            ctx.SaveChanges();
            parentId = parent.Id;

            var child = new ChildProfile { UserId = parentId, FirstName = "Kid" };
            ctx.ChildProfiles.Add(child);
            ctx.SaveChanges();
            childId = child.Id;

            var iepDoc = new IepDocument { ChildProfileId = childId, BlobUri = "iep/blob1.pdf", FileName = "iep.pdf" };
            ctx.IepDocuments.Add(iepDoc);
            ctx.SaveChanges();
            child.CurrentIepDocumentId = iepDoc.Id;
            ctx.SaveChanges();

            var section = new IepSection { IepDocumentId = iepDoc.Id, SectionType = "goals", DisplayOrder = 0 };
            ctx.IepSections.Add(section);
            ctx.SaveChanges();
            ctx.Goals.Add(new Goal { IepSectionId = section.Id, GoalText = "Read better" });

            ctx.EtrDocuments.Add(new EtrDocument { ChildProfileId = childId, BlobUri = "etr/blob2.pdf" });
            ctx.ProgressReports.Add(new ProgressReport { ChildProfileId = childId, IepDocumentId = iepDoc.Id, BlobUri = "pr/blob3.pdf" });
            ctx.ParentAdvocacyGoals.Add(new ParentAdvocacyGoal { ChildProfileId = childId, GoalText = "advocate" });
            // Linked to the IEP doc on purpose: the JournalEntry -> IepDocument FK is NoAction, so the purge must delete it first.
            ctx.JournalEntries.Add(new JournalEntry { ChildProfileId = childId, OccurredOn = new DateOnly(2026, 9, 1), Tag = JournalTag.Incident, ContentMarkdown = "sent home early", LinkedIepDocumentId = iepDoc.Id });
            ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = childId, UserId = parentId, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
            ctx.UsageRecords.Add(new UsageRecord { UserId = parentId, ChildProfileId = childId, OperationType = "analysis" });
            ctx.SaveChanges();

            // ---- School-owned data that must survive the purge untouched ----
            var district = new District { Name = "D1" };
            ctx.Districts.Add(district);
            ctx.SaveChanges();
            var school = new School { DistrictId = district.Id, Name = "S1" };
            ctx.Schools.Add(school);
            ctx.SaveChanges();
            var schoolStudent = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Kid" };
            ctx.SchoolStudents.Add(schoolStudent);
            ctx.SaveChanges();
            schoolStudentId = schoolStudent.Id;

            var templateVersion = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
            ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { templateVersion } });
            ctx.SaveChanges();

            var instance = new DocumentInstance
            {
                SchoolStudentId = schoolStudentId, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = templateVersion.Id,
                Status = DocumentInstanceStatus.Draft, ValuesJson = "{}", RowVersion = Guid.NewGuid().ToByteArray()
            };
            ctx.DocumentInstances.Add(instance);
            ctx.SaveChanges();
            documentInstanceId = instance.Id;
        }

        var blobs = new RecordingBlobStorage();
        var audit = new RecordingAuditLogger();
        using (var ctx = CreateContext())
        {
            var service = CreateService(ctx, blobs, audit);
            await service.PurgeAsync(parentId);
        }

        using var verify = CreateContext();
        Assert.Null(verify.Users.Find(parentId));
        Assert.Empty(verify.ChildProfiles.Where(c => c.Id == childId));
        Assert.Empty(verify.IepDocuments.ToList());
        Assert.Empty(verify.IepSections.ToList());
        Assert.Empty(verify.Goals.ToList());
        Assert.Empty(verify.EtrDocuments.ToList());
        Assert.Empty(verify.ProgressReports.ToList());
        Assert.Empty(verify.ParentAdvocacyGoals.ToList());
        Assert.Empty(verify.JournalEntries.ToList());
        Assert.Empty(verify.ChildAccesses.ToList());
        Assert.Empty(verify.UsageRecords.ToList());

        Assert.Equal(3, blobs.DeletedPaths.Count);
        Assert.Contains("iep/blob1.pdf", blobs.DeletedPaths);
        Assert.Contains("etr/blob2.pdf", blobs.DeletedPaths);
        Assert.Contains("pr/blob3.pdf", blobs.DeletedPaths);

        Assert.Contains(audit.Entries, e => e.Action == AuditAction.Delete && e.ResourceType == "User" && e.ResourceId == parentId);

        // School-owned records: untouched.
        Assert.NotNull(verify.SchoolStudents.Find(schoolStudentId));
        Assert.NotNull(verify.DocumentInstances.Find(documentInstanceId));
    }

    [Fact]
    public async Task PurgeAsync_Parent_AnonymisesDraftResponses_AndDeletesParentDraftNotes()
    {
        int parentId, revisionId;
        using (var ctx = CreateContext())
        {
            var teacher = new User { Email = "t@example.com", PasswordHash = "x", FirstName = "T", LastName = "E", Role = UserRole.Educator };
            var parent = new User { Email = "parent2@example.com", PasswordHash = "x", FirstName = "Pat", LastName = "Parent", Role = UserRole.Parent, DeletionRequestedAt = DateTime.UtcNow.AddDays(-31) };
            ctx.Users.AddRange(teacher, parent);
            ctx.SaveChanges();
            parentId = parent.Id;

            var district = new District { Name = "D2" };
            ctx.Districts.Add(district); ctx.SaveChanges();
            var school = new School { DistrictId = district.Id, Name = "S2" };
            ctx.Schools.Add(school); ctx.SaveChanges();
            var schoolStudent = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Kid2" };
            ctx.SchoolStudents.Add(schoolStudent); ctx.SaveChanges();

            var templateVersion = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
            ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { templateVersion } });
            ctx.SaveChanges();

            var instance = new DocumentInstance
            {
                SchoolStudentId = schoolStudent.Id, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = templateVersion.Id,
                Status = DocumentInstanceStatus.Draft, ValuesJson = "{}", RowVersion = Guid.NewGuid().ToByteArray()
            };
            ctx.DocumentInstances.Add(instance); ctx.SaveChanges();

            var revision = new SharedDraftRevision
            {
                DocumentInstanceId = instance.Id, RevisionNumber = 1, ValuesJson = "{}",
                DocumentTemplateVersionId = templateVersion.Id, SharedByUserId = teacher.Id, SharedAt = DateTime.UtcNow, Status = SharedDraftStatus.Active
            };
            ctx.SharedDraftRevisions.Add(revision);
            ctx.SaveChanges();
            revisionId = revision.Id;

            ctx.DraftResponses.Add(new DraftResponse { SharedDraftRevisionId = revisionId, ParentUserId = parentId, Kind = DraftResponseKind.Agree, Text = "Looks good" });
            ctx.ParentDraftNotes.Add(new ParentDraftNote { SharedDraftRevisionId = revisionId, ParentUserId = parentId, Question = "Why?", Answer = "Because" });
            ctx.SaveChanges();
        }

        var blobs = new RecordingBlobStorage();
        var audit = new RecordingAuditLogger();
        using (var ctx = CreateContext())
        {
            var service = CreateService(ctx, blobs, audit);
            await service.PurgeAsync(parentId);
        }

        using var verify = CreateContext();
        Assert.Null(verify.Users.Find(parentId));
        Assert.Empty(verify.ParentDraftNotes.ToList()); // deleted outright

        var response = verify.DraftResponses.Single(r => r.SharedDraftRevisionId == revisionId);
        Assert.NotEqual(parentId, response.ParentUserId); // repointed to the sentinel, not deleted
        var sentinel = verify.Users.Find(response.ParentUserId)!;
        Assert.Equal("Deleted", sentinel.FirstName);
        Assert.NotNull(verify.SharedDraftRevisions.Find(revisionId)); // school-owned; untouched
    }

    [Fact]
    public async Task PurgeAsync_Staff_DeactivatesAndAnonymises_ButKeepsTheRow_AndKeepsVersions()
    {
        int staffUserId, versionId, schoolStudentId;
        using (var ctx = CreateContext())
        {
            var staff = new User { Email = "staff@example.com", PasswordHash = "x", FirstName = "Sam", LastName = "Staff", Role = UserRole.Educator, DeletionRequestedAt = DateTime.UtcNow.AddDays(-31) };
            ctx.Users.Add(staff);
            ctx.SaveChanges();
            staffUserId = staff.Id;

            var district = new District { Name = "D3" };
            ctx.Districts.Add(district); ctx.SaveChanges();
            var school = new School { DistrictId = district.Id, Name = "S3" };
            ctx.Schools.Add(school); ctx.SaveChanges();
            ctx.StaffProfiles.Add(new StaffProfile { UserId = staffUserId, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher, IsActive = true });
            ctx.UserRecoveryCodes.Add(new UserRecoveryCode { UserId = staffUserId, CodeHash = "hash" });
            var schoolStudent = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Kid3" };
            ctx.SchoolStudents.Add(schoolStudent);
            ctx.SaveChanges();
            schoolStudentId = schoolStudent.Id;

            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = schoolStudentId, UserId = staffUserId, TeamRole = TeamRole.CaseManager });
            ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = schoolStudentId, UserId = staffUserId, Role = AccessRole.Owner, IsActive = true });

            var templateVersion = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
            ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { templateVersion } });
            ctx.SaveChanges();

            var authoredVersion = new AuthoredDocumentVersion
            {
                SchoolStudentId = schoolStudentId, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = templateVersion.Id,
                VersionNumber = 1, ValuesJson = "{}", FinalizedByUserId = staffUserId, FinalizedAt = DateTime.UtcNow
            };
            ctx.AuthoredDocumentVersions.Add(authoredVersion);
            ctx.SaveChanges();
            versionId = authoredVersion.Id;
        }

        var blobs = new RecordingBlobStorage();
        var audit = new RecordingAuditLogger();
        using (var ctx = CreateContext())
        {
            var service = CreateService(ctx, blobs, audit);
            await service.PurgeAsync(staffUserId);
        }

        using var verify = CreateContext();
        var user = verify.Users.Find(staffUserId);
        Assert.NotNull(user); // the row is kept, not deleted
        Assert.Equal("Former", user!.FirstName);
        Assert.Equal("Staff", user.LastName);
        Assert.StartsWith("deleted+", user.Email);
        Assert.False(user.IsActive);
        Assert.Null(user.DeletionRequestedAt); // marks the purge complete; stops re-processing

        var staffProfile = verify.StaffProfiles.Single(p => p.UserId == staffUserId);
        Assert.False(staffProfile.IsActive);
        Assert.Empty(verify.StudentTeamMembers.Where(m => m.UserId == staffUserId));
        Assert.Empty(verify.SchoolStudentAccesses.Where(a => a.UserId == staffUserId));
        Assert.Empty(verify.UserRecoveryCodes.Where(c => c.UserId == staffUserId)); // MFA recovery codes are credentials too

        // School records referencing this staff member by id keep working, unchanged.
        var version = verify.AuthoredDocumentVersions.Find(versionId);
        Assert.NotNull(version);
        Assert.Equal(staffUserId, version!.FinalizedByUserId);

        Assert.Contains(audit.Entries, e => e.Action == AuditAction.Delete && e.ResourceType == "User" && e.ResourceId == staffUserId);
    }

    [Fact]
    public async Task PurgeAsync_NotYetEligible_IsANoOp()
    {
        int userId;
        using (var ctx = CreateContext())
        {
            var parent = new User { Email = "recent@example.com", PasswordHash = "x", FirstName = "P", LastName = "R", Role = UserRole.Parent, DeletionRequestedAt = DateTime.UtcNow.AddDays(-5) };
            ctx.Users.Add(parent);
            ctx.SaveChanges();
            userId = parent.Id;
        }

        var blobs = new RecordingBlobStorage();
        var audit = new RecordingAuditLogger();
        using (var ctx = CreateContext())
        {
            var service = CreateService(ctx, blobs, audit);
            await service.PurgeAsync(userId);
        }

        using var verify = CreateContext();
        Assert.NotNull(verify.Users.Find(userId)); // still there — only 5 days in, not eligible
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task PurgeAsync_Parent_CancelsStripeFirst_AndAbortsWhenStripeFails()
    {
        int parentId;
        using (var ctx = CreateContext())
        {
            var parent = new User { Email = "billed@example.com", PasswordHash = "x", FirstName = "B", LastName = "P", Role = UserRole.Parent, DeletionRequestedAt = DateTime.UtcNow.AddDays(-31), StripeCustomerId = "cus_A", StripeSubscriptionId = "sub_A" };
            ctx.Users.Add(parent); ctx.SaveChanges(); parentId = parent.Id;
        }

        // Stripe down: the local row must survive so the next cycle retries — a billed customer is never orphaned.
        _stripe.Fail = true;
        using (var ctx = CreateContext())
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(ctx, new RecordingBlobStorage(), new RecordingAuditLogger()).PurgeAsync(parentId));
        using (var ctx = CreateContext())
            Assert.NotNull(ctx.Users.Find(parentId));

        _stripe.Fail = false;
        using (var ctx = CreateContext())
            await CreateService(ctx, new RecordingBlobStorage(), new RecordingAuditLogger()).PurgeAsync(parentId);
        Assert.Equal(new[] { "sub_A" }, _stripe.CancelledSubscriptions);
        Assert.Equal(new[] { "cus_A" }, _stripe.DeletedCustomers);
        using (var ctx = CreateContext())
            Assert.Null(ctx.Users.Find(parentId));
    }

    [Fact]
    public async Task PurgeAsync_Parent_ABlobDeleteFailure_AbortsBeforeAnyRowIsDeleted()
    {
        int parentId, childId;
        using (var ctx = CreateContext())
        {
            var parent = new User { Email = "blobfail@example.com", PasswordHash = "x", FirstName = "B", LastName = "F", Role = UserRole.Parent, DeletionRequestedAt = DateTime.UtcNow.AddDays(-31) };
            ctx.Users.Add(parent); ctx.SaveChanges(); parentId = parent.Id;
            var child = new ChildProfile { UserId = parentId, FirstName = "Kid" };
            ctx.ChildProfiles.Add(child); ctx.SaveChanges(); childId = child.Id;
            ctx.IepDocuments.Add(new IepDocument { ChildProfileId = childId, BlobUri = "iep/keep.pdf", FileName = "iep.pdf" });
            ctx.SaveChanges();
        }

        var blobs = new FailingBlobStorage();
        using (var ctx = CreateContext())
            await Assert.ThrowsAsync<IOException>(() => CreateService(ctx, blobs, new RecordingAuditLogger()).PurgeAsync(parentId));

        using var verify = CreateContext();
        Assert.NotNull(verify.Users.Find(parentId));
        Assert.Single(verify.IepDocuments.Where(d => d.ChildProfileId == childId)); // the row still points at the blob, so a retry can find it
    }

    private sealed class FailingBlobStorage : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default) => Task.FromResult(blobPath);
        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => throw new IOException("storage unreachable");
        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null) => Task.FromResult(blobPath);
    }

    public void Dispose() => _connection.Dispose();
}
