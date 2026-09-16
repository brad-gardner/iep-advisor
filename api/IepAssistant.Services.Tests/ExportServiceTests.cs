using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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
/// Plan 7, decision 8: a student-scoped export ZIP is built with a complete manifest (every archived file
/// listed with a correct sha256), never includes any <see cref="ParentDraftNote"/> content (staff-visible
/// <see cref="DraftResponse"/> only), and a student-scoped job's archive references exactly one student.
/// Uses an in-memory <see cref="IBlobStorageService"/> fake — no real blob/network access.
/// </summary>
public sealed class ExportServiceTests : IDisposable
{
    private readonly RosterTestDb _db = new();

    private sealed class InMemoryBlobStorageFake : IBlobStorageService
    {
        public Dictionary<string, byte[]> Blobs { get; } = new();

        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            Blobs[blobPath] = ms.ToArray();
            return Task.FromResult(blobPath);
        }

        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(Blobs.TryGetValue(blobPath, out var bytes) ? bytes : Array.Empty<byte>()));

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
        {
            Blobs.Remove(blobPath);
            return Task.CompletedTask;
        }

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null) => Task.FromResult($"https://fake.blob/{blobPath}");
    }

    private ExportService CreateService(ApplicationDbContext ctx, IBlobStorageService blob) =>
        new(ctx, new OrgAccessService(ctx), blob, new NotificationService(ctx), NullLogger<ExportService>.Instance);

    [Fact]
    public async Task RunAsync_StudentScope_ProducesCompleteManifest_ExcludesParentDraftNotes_ScopesToOneStudent()
    {
        const string StaffMarker = "STAFF_VISIBLE_RESPONSE_MARKER";
        const string ParentPrivateMarker = "SECRET_PARENT_PREP_NOTE_MARKER";

        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var studentId = _db.Student(schoolId, "Sam");
        var otherStudentId = _db.Student(schoolId, "Other");
        var (userId, _) = _db.Staff("exp@example.com", districtId, schoolId, OrgRoleIds.Teacher);
        _db.Access(studentId, userId, AccessRole.Collaborator);
        _db.Access(otherStudentId, userId, AccessRole.Collaborator);

        var blob = new InMemoryBlobStorageFake();

        int instanceId, versionId, templateVersionId;
        using (var ctx = _db.Context())
        {
            var tv = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
            var template = new DocumentTemplate { DocumentTypeId = 1, Name = "T", Versions = { tv } };
            ctx.DocumentTemplates.Add(template);
            ctx.SaveChanges();
            templateVersionId = tv.Id;

            var instance = new DocumentInstance
            {
                SchoolStudentId = studentId, DocumentTypeId = 1, DocumentTemplateVersionId = templateVersionId,
                Status = DocumentInstanceStatus.Draft, ValuesJson = "{}"
            };
            ctx.DocumentInstances.Add(instance);
            ctx.SaveChanges();
            instanceId = instance.Id;

            var version = new AuthoredDocumentVersion
            {
                SchoolStudentId = studentId, DocumentTypeId = 1, DocumentTemplateVersionId = templateVersionId,
                VersionNumber = 1, ValuesJson = "{\"k\":\"v\"}", FinalizedByUserId = userId, FinalizedAt = DateTime.UtcNow.AddDays(-20),
                Pdf = new AuthoredDocumentPdf { RenderStatus = PdfRenderStatus.Rendered, RenderedAt = DateTime.UtcNow.AddDays(-20) }
            };
            ctx.AuthoredDocumentVersions.Add(version);
            ctx.SaveChanges();
            versionId = version.Id;

            ctx.SignedArtifacts.Add(new SignedArtifact
            {
                AuthoredDocumentVersionId = versionId, BlobPath = "signed-artifacts/1/signed.pdf", FileName = "signed.pdf",
                ContentType = "application/pdf", SizeBytes = 3, UploadedByUserId = userId, UploadedAt = DateTime.UtcNow
            });

            ctx.GoalRecords.Add(new GoalRecord
            {
                SchoolStudentId = studentId, LineageId = Guid.NewGuid(), AuthoredDocumentVersionId = versionId,
                DocumentInstanceId = instanceId, FieldKey = Guid.NewGuid(), GoalText = "Improve reading fluency",
                Status = GoalRecordStatus.Active, ProjectedAt = DateTime.UtcNow
            });

            ctx.FamilyContactAttempts.Add(new FamilyContactAttempt
            {
                SchoolStudentId = studentId, AttemptedAt = DateTime.UtcNow.AddDays(-5), Method = FamilyContactMethod.Phone,
                Outcome = FamilyContactOutcome.Reached, RecordedByUserId = userId
            });

            ctx.OfflineFamilyInputs.Add(new OfflineFamilyInput
            {
                SchoolStudentId = studentId, DocumentInstanceId = instanceId, ReceivedAt = DateTime.UtcNow.AddDays(-4),
                Method = FamilyContactMethod.InPerson, Summary = "Parent shared feedback in person.", RecordedByUserId = userId
            });

            var revision = new SharedDraftRevision
            {
                DocumentInstanceId = instanceId, RevisionNumber = 1, ValuesJson = "{}", DocumentTemplateVersionId = templateVersionId,
                SharedByUserId = userId, SharedAt = DateTime.UtcNow.AddDays(-6), Status = SharedDraftStatus.Active
            };
            ctx.SharedDraftRevisions.Add(revision);
            ctx.SaveChanges();

            var parentUserId = _db.SeedUser("parent-exp@example.com", UserRole.Parent);
            ctx.DraftResponses.Add(new DraftResponse
            {
                SharedDraftRevisionId = revision.Id, ParentUserId = parentUserId, Kind = DraftResponseKind.Comment,
                Text = StaffMarker, Status = DraftResponseStatus.Open
            });
            // Private parent prep note — must NEVER appear anywhere in the export.
            ctx.ParentDraftNotes.Add(new ParentDraftNote
            {
                SharedDraftRevisionId = revision.Id, ParentUserId = parentUserId,
                Question = ParentPrivateMarker, Answer = ParentPrivateMarker
            });

            var meetingId = _db.Meeting(studentId, userId, DateTime.UtcNow.AddDays(-3), status: MeetingStatus.Held);
            ctx.MeetingDecisions.Add(new MeetingDecision
            {
                MeetingId = meetingId, Text = "Adjust goal wording", Outcome = MeetingDecisionOutcome.Agreed, RecordedByUserId = userId
            });

            ctx.SaveChanges();
        }

        blob.Blobs[IAuthoredDocumentPdfService.BlobPathFor(versionId, 1)] = Encoding.UTF8.GetBytes("%PDF-fake-content");
        blob.Blobs["signed-artifacts/1/signed.pdf"] = new byte[] { 1, 2, 3 };

        int jobId;
        using (var ctx = _db.Context())
        {
            var enqueue = await CreateService(ctx, blob).EnqueueStudentExportAsync(userId, studentId);
            Assert.True(enqueue.Success, enqueue.Message);
            jobId = enqueue.Data!.Id;
        }

        using (var ctx = _db.Context())
            await CreateService(ctx, blob).RunAsync(jobId);

        using (var ctx = _db.Context())
        {
            var status = await CreateService(ctx, blob).GetStatusAsync(userId, jobId);
            Assert.True(status.Success, status.Message);
            Assert.Equal(ExportJobStatus.Completed, status.Data!.Status);
            Assert.Equal(1, status.Data!.StudentCount);
            Assert.True(status.Data!.FileCount > 0);
        }

        var zipBytes = blob.Blobs[$"exports/{jobId}.zip"];
        using var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);

        var entryBytes = new Dictionary<string, byte[]>();
        foreach (var entry in archive.Entries)
        {
            using var es = entry.Open();
            using var ms = new MemoryStream();
            es.CopyTo(ms);
            entryBytes[entry.FullName] = ms.ToArray();
        }

        // Manifest completeness: every listed file's sha256 matches the ACTUAL archived bytes.
        var manifestEntry = entryBytes["manifest.json"];
        using var manifestDoc = System.Text.Json.JsonDocument.Parse(manifestEntry);
        var files = manifestDoc.RootElement.GetProperty("files").EnumerateArray().ToList();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var path = file.GetProperty("path").GetString()!;
            var sha256 = file.GetProperty("sha256").GetString()!;
            Assert.True(entryBytes.ContainsKey(path), $"manifest references '{path}' which is not in the archive");
            var actualHash = Convert.ToHexString(SHA256.HashData(entryBytes[path])).ToLowerInvariant();
            Assert.Equal(actualHash, sha256);
        }
        // manifest.json itself is not required to list itself, but every OTHER archived file must be listed.
        var manifestPaths = files.Select(f => f.GetProperty("path").GetString()).ToHashSet();
        foreach (var actualPath in entryBytes.Keys.Where(k => k != "manifest.json"))
            Assert.Contains(actualPath, manifestPaths);

        // No ParentDraftNote content anywhere in the archive.
        foreach (var (path, bytes) in entryBytes)
            Assert.DoesNotContain(ParentPrivateMarker, Encoding.UTF8.GetString(bytes));

        // The staff-visible DraftResponse text DOES appear (responses.json is included).
        Assert.Contains(StaffMarker, Encoding.UTF8.GetString(entryBytes["responses.json"]));

        // Student scope contains exactly one student.
        var studentIdsInArchive = entryBytes.Keys
            .Where(k => k.StartsWith("students/"))
            .Select(k => k.Split('/')[1])
            .Distinct()
            .ToList();
        Assert.Single(studentIdsInArchive);
        Assert.Equal(studentId.ToString(), studentIdsInArchive[0]);

        // The rendered PDF and signed artifact both made it into the archive.
        Assert.Contains(entryBytes.Keys, k => k.EndsWith(".pdf") && k.Contains($"students/{studentId}/versions/"));
        Assert.Contains(entryBytes.Keys, k => k.StartsWith("signed/"));
    }

    [Fact]
    public async Task EnqueueDistrictExport_NonDistrictAdmin_IsDenied()
    {
        var districtId = _db.District();
        var schoolId = _db.School(districtId, "S");
        var (userId, _) = _db.Staff("teacher@example.com", districtId, schoolId, OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var result = await CreateService(ctx, new InMemoryBlobStorageFake()).EnqueueDistrictExportAsync(userId);
        Assert.False(result.Success);
    }

    public void Dispose() => _db.Dispose();
}
