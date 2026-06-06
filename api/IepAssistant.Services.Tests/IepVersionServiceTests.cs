using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using QuestPDF.Infrastructure;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P5a coverage: LineageId carry-forward (carry/add/drop), VersionNumber increment per student,
/// immutability interceptor (version content immutable; IepVersionPdf mutable), the IepDraftService
/// edit-freeze while Status=Finalizing, re-finalize creating a new version, and parent visibility
/// via an active accepted ChildLink. Real SQLite in-memory engine (serializable transactions work)
/// WITH the <see cref="ImmutableVersionInterceptor"/> wired into the options so the immutability
/// test actually exercises it.
/// </summary>
public sealed class IepVersionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    // QuestPDF requires the license to be set once before any headless rendering.
    static IepVersionServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ---- Hand-written blob fakes (no Azure dependency) ----
    private sealed class SuccessBlobStorageFake : IBlobStorageService
    {
        public string? LastBlobPath { get; private set; }
        public byte[]? LastBytes { get; private set; }

        public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            LastBlobPath = blobPath;
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            LastBytes = ms.ToArray();
            return $"https://fake.blob/{blobPath}";
        }

        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(LastBytes ?? Array.Empty<byte>()));

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
            => Task.FromResult($"https://fake.blob/{blobPath}?sas=token");
    }

    private sealed class FailingBlobStorageFake : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("blob upload exploded");

        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("download exploded");

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
            => Task.FromResult($"https://fake.blob/{blobPath}");
    }

    private IepVersionPdfService CreatePdfService(ApplicationDbContext ctx, IBlobStorageService blob)
        => new(ctx, blob, NullLogger<IepVersionPdfService>.Instance);

    public IepVersionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new ImmutableVersionInterceptor())
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private readonly CapturingAuditLogger _audit = new();

    private ApplicationDbContext CreateContext() => new(_options);
    private IepVersionService CreateVersionService(ApplicationDbContext ctx, IBlobStorageService? blob = null)
        => new(ctx, new AccessService(ctx), blob ?? new SuccessBlobStorageFake(), _audit, NullLogger<IepVersionService>.Instance);
    private IepDraftService CreateDraftService(ApplicationDbContext ctx)
        => new(ctx, _audit, NullLogger<IepDraftService>.Instance);

    // ---------------------------------------------------------------- Seed helpers

    private sealed record Scenario(int SchoolId, int EducatorUserId, int StudentId);

    private Scenario SeedSchoolWithStudent(string prefix)
    {
        using var ctx = CreateContext();

        var user = new User { Email = $"{prefix}@example.com", PasswordHash = "x", FirstName = "Ed", LastName = "U", Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var district = new District { Name = $"{prefix} District" };
        ctx.Districts.Add(district);
        ctx.SaveChanges();

        var school = new School { DistrictId = district.Id, Name = $"{prefix} School" };
        ctx.Schools.Add(school);
        ctx.SaveChanges();

        ctx.TeacherProfiles.Add(new TeacherProfile { UserId = user.Id, SchoolId = school.Id });
        ctx.SaveChanges();

        var student = new SchoolStudent { SchoolId = school.Id, FirstName = "Sam", IsActive = true };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();

        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id,
            UserId = user.Id,
            Role = AccessRole.Collaborator,
            IsActive = true
        });
        ctx.SaveChanges();

        return new Scenario(school.Id, user.Id, student.Id);
    }

    private async Task<int> CreateDraftAsync(Scenario s)
    {
        using var ctx = CreateContext();
        var r = await CreateDraftService(ctx).CreateDraftAsync(s.EducatorUserId, s.StudentId, "Annual");
        Assert.True(r.Success);
        return r.Data!.Id;
    }

    private async Task<int> AddGoalAsync(Scenario s, int draftId, string text)
    {
        using var ctx = CreateContext();
        var r = await CreateDraftService(ctx).AddGoalAsync(s.EducatorUserId, draftId, new UpsertIepDraftGoalModel { GoalText = text });
        Assert.True(r.Success);
        return r.Data!.Id;
    }

    private async Task<IepVersionSummaryModel> FinalizeAsync(Scenario s, int draftId)
    {
        using var ctx = CreateContext();
        var r = await CreateVersionService(ctx).FinalizeAsync(s.EducatorUserId, draftId, null);
        Assert.True(r.Success, r.Message);
        return r.Data!;
    }

    // ---------------------------------------------------------------- LineageId carry-forward

    [Fact]
    public async Task Finalize_CarriesGoalLineageIds_AddDropAcrossVersions()
    {
        var s = SeedSchoolWithStudent("lineage");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G1");
        var goal2Id = await AddGoalAsync(s, draftId, "G2");

        // Capture the draft goal lineage ids.
        Guid g1Lineage, g2Lineage;
        using (var ctx = CreateContext())
        {
            var draftGoals = ctx.IepDraftGoals.Where(g => g.IepDraftId == draftId).OrderBy(g => g.Id).ToList();
            g1Lineage = draftGoals[0].LineageId;
            g2Lineage = draftGoals[1].LineageId;
        }

        // v1: both goals, same lineage ids.
        var v1 = await FinalizeAsync(s, draftId);
        using (var ctx = CreateContext())
        {
            var v1Lineages = ctx.IepVersionGoals.Where(g => g.IepVersionId == v1.Id).Select(g => g.LineageId).ToList();
            Assert.Equal(2, v1Lineages.Count);
            Assert.Contains(g1Lineage, v1Lineages);
            Assert.Contains(g2Lineage, v1Lineages);
        }

        // Add a 3rd goal, finalize -> v2 has 3, with a NEW lineage for the added one.
        await AddGoalAsync(s, draftId, "G3");
        Guid g3Lineage;
        using (var ctx = CreateContext())
            g3Lineage = ctx.IepDraftGoals.Where(g => g.IepDraftId == draftId).OrderBy(g => g.Id).Last().LineageId;

        var v2 = await FinalizeAsync(s, draftId);
        using (var ctx = CreateContext())
        {
            var v2Lineages = ctx.IepVersionGoals.Where(g => g.IepVersionId == v2.Id).Select(g => g.LineageId).ToList();
            Assert.Equal(3, v2Lineages.Count);
            Assert.Contains(g1Lineage, v2Lineages);
            Assert.Contains(g2Lineage, v2Lineages);
            Assert.Contains(g3Lineage, v2Lineages);
        }

        // Delete goal 2 in the draft, finalize -> v3 omits that lineage.
        using (var ctx = CreateContext())
            Assert.True((await CreateDraftService(ctx).DeleteGoalAsync(s.EducatorUserId, draftId, goal2Id)).Success);

        var v3 = await FinalizeAsync(s, draftId);
        using (var ctx = CreateContext())
        {
            var v3Lineages = ctx.IepVersionGoals.Where(g => g.IepVersionId == v3.Id).Select(g => g.LineageId).ToList();
            Assert.Equal(2, v3Lineages.Count);
            Assert.DoesNotContain(g2Lineage, v3Lineages);
            Assert.Contains(g1Lineage, v3Lineages);
            Assert.Contains(g3Lineage, v3Lineages);
        }
    }

    // ---------------------------------------------------------------- VersionNumber

    [Fact]
    public async Task Finalize_IncrementsVersionNumberPerStudent()
    {
        var s = SeedSchoolWithStudent("vnum");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");

        Assert.Equal(1, (await FinalizeAsync(s, draftId)).VersionNumber);
        Assert.Equal(2, (await FinalizeAsync(s, draftId)).VersionNumber);
        Assert.Equal(3, (await FinalizeAsync(s, draftId)).VersionNumber);
    }

    // ---------------------------------------------------------------- Re-finalize (no-change allowed)

    [Fact]
    public async Task Finalize_NoChange_StillCreatesNewVersion()
    {
        var s = SeedSchoolWithStudent("refinal");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");

        var v1 = await FinalizeAsync(s, draftId);
        var v2 = await FinalizeAsync(s, draftId); // identical content

        Assert.NotEqual(v1.Id, v2.Id);
        Assert.Equal(v1.VersionNumber + 1, v2.VersionNumber);

        // Draft is left editable (Status back to Draft) after finalize.
        using var ctx = CreateContext();
        Assert.Equal(IepDraftStatus.Draft, ctx.IepDrafts.Single(d => d.Id == draftId).Status);
    }

    [Fact]
    public async Task VersionNumber_DuplicatePerStudent_IsRejectedByUniqueIndex()
    {
        var s = SeedSchoolWithStudent("uniquever");
        var draftId = await CreateDraftAsync(s);
        var v1 = await FinalizeAsync(s, draftId);

        // The DB-enforced backstop: a second IepVersion with the same (SchoolStudentId, VersionNumber)
        // must be rejected even if the serializable transaction were bypassed.
        using var ctx = CreateContext();
        ctx.IepVersions.Add(new IepVersion
        {
            SchoolStudentId = s.StudentId,
            SourceDraftId = draftId,
            VersionNumber = v1.VersionNumber, // duplicate
            DocumentType = IepDocumentType.Iep,
            FinalizedByUserId = s.EducatorUserId,
            FinalizedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
    }

    // ---------------------------------------------------------------- Immutability interceptor

    [Fact]
    public async Task VersionGoal_Update_ThrowsImmutable()
    {
        var s = SeedSchoolWithStudent("immut-upd");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");
        var v = await FinalizeAsync(s, draftId);

        using var ctx = CreateContext();
        var goal = ctx.IepVersionGoals.First(g => g.IepVersionId == v.Id);
        goal.GoalText = "tampered";
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VersionGoal_Delete_ThrowsImmutable()
    {
        var s = SeedSchoolWithStudent("immut-del");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");
        var v = await FinalizeAsync(s, draftId);

        using var ctx = CreateContext();
        var goal = ctx.IepVersionGoals.First(g => g.IepVersionId == v.Id);
        ctx.IepVersionGoals.Remove(goal);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task VersionPdf_Update_IsAllowed()
    {
        var s = SeedSchoolWithStudent("pdf-mut");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");
        var v = await FinalizeAsync(s, draftId);

        using var ctx = CreateContext();
        var pdf = ctx.IepVersionPdfs.Single(p => p.IepVersionId == v.Id);
        pdf.RenderStatus = PdfRenderStatus.Rendered;
        pdf.BlobUri = "https://blob/x.pdf";
        pdf.RenderedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync(); // no throw

        Assert.Equal(PdfRenderStatus.Rendered, ctx.IepVersionPdfs.Single(p => p.IepVersionId == v.Id).RenderStatus);
    }

    // ---------------------------------------------------------------- Edit-freeze

    [Fact]
    public async Task DraftMutation_RejectedWhileFinalizing()
    {
        var s = SeedSchoolWithStudent("freeze");
        var draftId = await CreateDraftAsync(s);
        var goalId = await AddGoalAsync(s, draftId, "G");

        // Simulate the in-progress finalize window.
        using (var ctx = CreateContext())
        {
            var draft = ctx.IepDrafts.Single(d => d.Id == draftId);
            draft.Status = IepDraftStatus.Finalizing;
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var r = await CreateDraftService(ctx).UpdateGoalAsync(s.EducatorUserId, draftId, goalId, new UpsertIepDraftGoalModel { GoalText = "nope" });
            Assert.False(r.Success);
            Assert.Contains("being finalized", r.Message!, StringComparison.OrdinalIgnoreCase);
        }

        // FinalizeAsync itself refuses to start a second finalize while already Finalizing.
        using (var ctx = CreateContext())
        {
            var r = await CreateVersionService(ctx).FinalizeAsync(s.EducatorUserId, draftId, null);
            Assert.False(r.Success);
            Assert.Contains("already being finalized", r.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- Parent visibility

    [Fact]
    public async Task ParentLinked_CanListAndGetVersions_UnlinkedCannot()
    {
        var s = SeedSchoolWithStudent("parent");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");
        var v = await FinalizeAsync(s, draftId);

        // Seed a parent + child profile, an Owner ChildAccess, and an active accepted ChildLink.
        int parentUserId, childId, strangerUserId, strangerChildId;
        using (var ctx = CreateContext())
        {
            var parent = new User { Email = "linked-parent@example.com", PasswordHash = "x", FirstName = "P", LastName = "A", Role = UserRole.Parent };
            ctx.Users.Add(parent);
            ctx.SaveChanges();
            parentUserId = parent.Id;

            var child = new ChildProfile { UserId = parent.Id, FirstName = "Kid", IsActive = true };
            ctx.ChildProfiles.Add(child);
            ctx.SaveChanges();
            childId = child.Id;

            ctx.ChildAccesses.Add(new ChildAccess
            {
                ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner,
                IsActive = true, AcceptedAt = DateTime.UtcNow
            });
            ctx.ChildLinks.Add(new ChildLink
            {
                ChildProfileId = child.Id, SchoolStudentId = s.StudentId,
                IsActive = true, AcceptedAt = DateTime.UtcNow, LinkedAt = DateTime.UtcNow
            });
            ctx.SaveChanges();

            // A stranger parent with their own unlinked child.
            var stranger = new User { Email = "stranger-parent@example.com", PasswordHash = "x", FirstName = "S", LastName = "T", Role = UserRole.Parent };
            ctx.Users.Add(stranger);
            ctx.SaveChanges();
            strangerUserId = stranger.Id;
            var strangerChild = new ChildProfile { UserId = stranger.Id, FirstName = "Other", IsActive = true };
            ctx.ChildProfiles.Add(strangerChild);
            ctx.SaveChanges();
            strangerChildId = strangerChild.Id;
            ctx.ChildAccesses.Add(new ChildAccess
            {
                ChildProfileId = strangerChild.Id, UserId = stranger.Id, Role = AccessRole.Owner,
                IsActive = true, AcceptedAt = DateTime.UtcNow
            });
            ctx.SaveChanges();
        }

        // Linked parent sees the version in the list...
        using (var ctx = CreateContext())
        {
            var list = await CreateVersionService(ctx).ListForChildAsync(parentUserId, childId);
            Assert.True(list.Success);
            Assert.Single(list.Data!);
            Assert.Equal(v.Id, list.Data![0].Id);
        }

        // ...and can GET the full version.
        using (var ctx = CreateContext())
        {
            var get = await CreateVersionService(ctx).GetVersionAsync(parentUserId, v.Id);
            Assert.True(get.Success, get.Message);
            Assert.Single(get.Data!.Goals);
        }

        // Unlinked stranger parent sees nothing and is rejected on GET.
        using (var ctx = CreateContext())
        {
            var list = await CreateVersionService(ctx).ListForChildAsync(strangerUserId, strangerChildId);
            Assert.True(list.Success);
            Assert.Empty(list.Data!);
        }
        using (var ctx = CreateContext())
        {
            var get = await CreateVersionService(ctx).GetVersionAsync(strangerUserId, v.Id);
            Assert.False(get.Success);
            Assert.Contains("permission", get.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- P5b: PDF render

    [Fact]
    public async Task RenderAsync_Success_MarksRenderedWithBlobChecksumAndBytes()
    {
        var s = SeedSchoolWithStudent("pdf-ok");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "Improve reading fluency");
        var v = await FinalizeAsync(s, draftId);

        var blob = new SuccessBlobStorageFake();
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, blob).RenderAsync(v.Id);

        // QuestPDF generated a real, non-empty PDF byte[] headless.
        Assert.NotNull(blob.LastBytes);
        Assert.NotEmpty(blob.LastBytes!);
        Assert.Equal($"iep-versions/{v.Id}/iep-v{v.VersionNumber}.pdf", blob.LastBlobPath);

        using (var ctx = CreateContext())
        {
            var pdf = ctx.IepVersionPdfs.Single(p => p.IepVersionId == v.Id);
            Assert.Equal(PdfRenderStatus.Rendered, pdf.RenderStatus);
            Assert.False(string.IsNullOrWhiteSpace(pdf.BlobUri));
            Assert.False(string.IsNullOrWhiteSpace(pdf.Checksum));
            Assert.NotNull(pdf.RenderedAt);
            Assert.Null(pdf.ErrorMessage);
        }
    }

    [Fact]
    public async Task RenderAsync_BlobFailure_MarksErrorAndLeavesVersionValid()
    {
        var s = SeedSchoolWithStudent("pdf-fail");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "Goal text");
        var v = await FinalizeAsync(s, draftId);

        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new FailingBlobStorageFake()).RenderAsync(v.Id); // does not throw

        using (var ctx = CreateContext())
        {
            var pdf = ctx.IepVersionPdfs.Single(p => p.IepVersionId == v.Id);
            Assert.Equal(PdfRenderStatus.Error, pdf.RenderStatus);
            Assert.False(string.IsNullOrWhiteSpace(pdf.ErrorMessage));
            Assert.Null(pdf.RenderedAt);

            // The immutable version + its children are untouched/valid (interceptor not tripped).
            var version = ctx.IepVersions
                .Where(x => x.Id == v.Id)
                .Select(x => new { x.Id, GoalCount = x.Goals.Count })
                .Single();
            Assert.Equal(v.Id, version.Id);
            Assert.Equal(1, version.GoalCount);
        }
    }

    [Fact]
    public async Task RenderAsync_RetryAfterError_Succeeds()
    {
        var s = SeedSchoolWithStudent("pdf-retry");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "Goal");
        var v = await FinalizeAsync(s, draftId);

        // First render fails.
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new FailingBlobStorageFake()).RenderAsync(v.Id);
        using (var ctx = CreateContext())
            Assert.Equal(PdfRenderStatus.Error, ctx.IepVersionPdfs.Single(p => p.IepVersionId == v.Id).RenderStatus);

        // Retry with a working blob -> Rendered (overwrites the Error fields).
        var blob = new SuccessBlobStorageFake();
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, blob).RenderAsync(v.Id);

        using (var ctx = CreateContext())
        {
            var pdf = ctx.IepVersionPdfs.Single(p => p.IepVersionId == v.Id);
            Assert.Equal(PdfRenderStatus.Rendered, pdf.RenderStatus);
            Assert.Null(pdf.ErrorMessage);
            Assert.False(string.IsNullOrWhiteSpace(pdf.Checksum));
        }
        Assert.NotEmpty(blob.LastBytes!);
    }

    // ---------------------------------------------------------------- Audit (P6a)

    [Fact]
    public async Task Finalize_RecordsOneFinalizeAuditEntry()
    {
        var s = SeedSchoolWithStudent("audit-finalize");
        var draftId = await CreateDraftAsync(s);
        await AddGoalAsync(s, draftId, "G");

        _audit.Entries.Clear();
        var v = await FinalizeAsync(s, draftId);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Finalize, entry.Action);
        Assert.Equal(s.EducatorUserId, entry.ActorUserId);
        Assert.Equal("IepVersion", entry.ResourceType);
        Assert.Equal(v.Id, entry.ResourceId);
    }

    [Fact]
    public async Task GetVersion_RecordsOneViewAuditEntry()
    {
        var s = SeedSchoolWithStudent("audit-getversion");
        var draftId = await CreateDraftAsync(s);
        var v = await FinalizeAsync(s, draftId);

        _audit.Entries.Clear();
        using (var ctx = CreateContext())
        {
            var get = await CreateVersionService(ctx).GetVersionAsync(s.EducatorUserId, v.Id);
            Assert.True(get.Success, get.Message);
        }

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.View, entry.Action);
        Assert.Equal(s.EducatorUserId, entry.ActorUserId);
        Assert.Equal("IepVersion", entry.ResourceType);
        Assert.Equal(v.Id, entry.ResourceId);
    }

    public void Dispose() => _connection.Dispose();
}
