using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// District and per-student export (see <see cref="IExportService"/>, plan 7, decision 8). Enqueue
/// methods only create the <see cref="ExportJob"/> row (Queued); the controller enqueues onto
/// <c>ExportQueue</c> after commit, mirroring <see cref="AuthoredDocumentVersionService"/>/PDF worker
/// split. <see cref="RunAsync"/> is the actual ZIP build, invoked only by <c>ExportWorker</c>.
///
/// <para><b>ZIP construction:</b> streamed through a temp <see cref="FileStream"/> — the archive itself
/// is never held fully in memory. Individual member files (JSON payloads, one PDF/signed-artifact at a
/// time) are bounded per-document and are buffered briefly to compute their SHA-256 for the manifest.</para>
///
/// <para><b>Audit extract:</b> <see cref="AccessAuditLog"/> has no direct SchoolStudentId column, so the
/// extract resolves a fixed set of resource types this feature actually writes against
/// (SchoolStudent/StudentEvidence/DocumentInstance/AuthoredDocumentVersion/Meeting/GoalRecord/
/// EvaluationCase), gathers the in-scope ids for each, and filters rows created in the last two years —
/// documented limitation rather than a generic cross-resource-type join.</para>
/// </summary>
public class ExportService : IExportService
{
    private const string PermissionMessage = "You do not have permission to request this export.";
    private const string JobNotFoundMessage = "Export not found.";
    private const string NotReadyMessage = "This export is not ready for download yet.";
    private const string StudentNotFoundMessage = "Student not found.";
    private static readonly TimeSpan DownloadExpiry = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AuditWindow = TimeSpan.FromDays(365 * 2);

    // camelCase to match the contract's manifest shape (files: [{path, sha256, bytes}], schemaVersion, …).
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IBlobStorageService _blob;
    private readonly INotificationService _notifications;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IBlobStorageService blob,
        INotificationService notifications,
        ILogger<ExportService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _blob = blob;
        _notifications = notifications;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Enqueue + read

    public async Task<ServiceResult<ExportJobModel>> EnqueueDistrictExportAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null || ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            return ServiceResult<ExportJobModel>.FailureResult(PermissionMessage);

        var job = new ExportJob
        {
            Scope = ExportScope.District,
            DistrictId = ctx.DistrictId,
            RequestedByUserId = userId,
            Status = ExportJobStatus.Queued,
            RequestedAt = DateTime.UtcNow,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.ExportJobs.AddAsync(job, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<ExportJobModel>.SuccessResult(await MapAsync(job.Id, ct));
    }

    public async Task<ServiceResult<List<ExportJobModel>>> ListDistrictExportsAsync(int userId, CancellationToken ct = default)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null || ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            return ServiceResult<List<ExportJobModel>>.FailureResult(PermissionMessage);

        var rows = await MapQuery(_context.ExportJobs.AsNoTracking()
                // Both scopes: a "Export record" from a student page lands here too, so the admin
                // page is the one place every archive for the district can be found and downloaded.
                .Where(j => j.DistrictId == ctx.DistrictId))
            .OrderByDescending(j => j.RequestedAt)
            .ToListAsync(ct);

        return ServiceResult<List<ExportJobModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<ExportJobModel>> EnqueueStudentExportAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<ExportJobModel>.FailureResult(PermissionMessage);

        var districtId = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => (int?)s.DistrictId)
            .FirstOrDefaultAsync(ct);
        if (districtId == null)
            return ServiceResult<ExportJobModel>.FailureResult(StudentNotFoundMessage);

        var job = new ExportJob
        {
            Scope = ExportScope.Student,
            DistrictId = districtId.Value,
            SchoolStudentId = schoolStudentId,
            RequestedByUserId = userId,
            Status = ExportJobStatus.Queued,
            RequestedAt = DateTime.UtcNow,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.ExportJobs.AddAsync(job, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<ExportJobModel>.SuccessResult(await MapAsync(job.Id, ct));
    }

    public async Task<ServiceResult<ExportJobModel>> GetStatusAsync(int userId, int jobId, CancellationToken ct = default)
    {
        var job = await _context.ExportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
            return ServiceResult<ExportJobModel>.FailureResult(JobNotFoundMessage);
        if (!await CanReadJobAsync(userId, job, ct))
            return ServiceResult<ExportJobModel>.FailureResult(PermissionMessage);

        return ServiceResult<ExportJobModel>.SuccessResult(await MapAsync(jobId, ct));
    }

    public async Task<ServiceResult<string>> GetDownloadUrlAsync(int userId, int jobId, CancellationToken ct = default)
    {
        var job = await _context.ExportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
            return ServiceResult<string>.FailureResult(JobNotFoundMessage);
        if (!await CanReadJobAsync(userId, job, ct))
            return ServiceResult<string>.FailureResult(PermissionMessage);
        if (job.Status != ExportJobStatus.Completed || string.IsNullOrWhiteSpace(job.BlobPath))
            return ServiceResult<string>.FailureResult(NotReadyMessage);

        var url = await _blob.GetDownloadUrlAsync(job.BlobPath, DownloadExpiry);
        return ServiceResult<string>.SuccessResult(url);
    }

    private async Task<bool> CanReadJobAsync(int userId, ExportJob job, CancellationToken ct)
    {
        if (job.RequestedByUserId == userId)
            return true;
        if (job.Scope == ExportScope.District)
        {
            var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
            return ctx != null && ctx.OrgRoleId == OrgRoleIds.DistrictAdmin && ctx.DistrictId == job.DistrictId;
        }
        return job.SchoolStudentId.HasValue
            && await _orgAccess.CanActOnStudentAsync(userId, job.SchoolStudentId.Value, AccessRole.Viewer, ct);
    }

    // ---------------------------------------------------------------- Run (worker only)

    public async Task RunAsync(int jobId, CancellationToken ct = default)
    {
        var job = await _context.ExportJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job == null)
        {
            _logger.LogWarning("Export run skipped: ExportJob {JobId} not found", jobId);
            return;
        }
        if (job.Status is ExportJobStatus.Completed or ExportJobStatus.Failed)
            return;

        job.Status = ExportJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        var tempPath = Path.Combine(Path.GetTempPath(), $"export-{jobId}-{Guid.NewGuid():N}.zip");
        try
        {
            var studentIds = await ResolveStudentIdsAsync(job, ct);
            var manifestFiles = new List<ExportManifestFileModel>();

            long sizeBytes;
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var studentId in studentIds)
                        await WriteStudentFilesAsync(archive, studentId, manifestFiles, ct);

                    await WriteAggregateFilesAsync(archive, studentIds, manifestFiles, ct);

                    var manifest = new
                    {
                        exportId = jobId,
                        scope = job.Scope.ToString(),
                        generatedAt = DateTime.UtcNow,
                        districtId = job.DistrictId,
                        studentCount = studentIds.Count,
                        files = manifestFiles,
                        schemaVersion = 1
                    };
                    await WriteJsonEntryAsync(archive, "manifest.json", manifest, null, ct);
                }
                sizeBytes = fileStream.Length;
            }

            var blobPath = $"exports/{jobId}.zip";
            await using (var uploadStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read))
                await _blob.UploadAsync(blobPath, uploadStream, "application/zip", ct);

            job.Status = ExportJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.BlobPath = blobPath;
            job.SizeBytes = sizeBytes;
            job.StudentCount = studentIds.Count;
            job.FileCount = manifestFiles.Count;
            job.UpdatedById = job.RequestedByUserId;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Export {JobId} completed: {StudentCount} student(s), {FileCount} file(s), {Bytes} bytes",
                jobId, studentIds.Count, manifestFiles.Count, sizeBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export {JobId} failed", jobId);
            try
            {
                job.Status = ExportJobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.Error = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                await _context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist Failed status for export {JobId}", jobId);
            }
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }

        await NotifyRequesterAsync(job, ct);
    }

    private async Task<List<int>> ResolveStudentIdsAsync(ExportJob job, CancellationToken ct)
    {
        if (job.Scope == ExportScope.Student)
            return job.SchoolStudentId.HasValue ? new List<int> { job.SchoolStudentId.Value } : new List<int>();

        return await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.DistrictId == job.DistrictId && s.Status == StudentStatus.Active)
            .Select(s => s.Id)
            .ToListAsync(ct);
    }

    // ---------------------------------------------------------------- Per-student files

    private async Task WriteStudentFilesAsync(ZipArchive archive, int studentId, List<ExportManifestFileModel> manifestFiles, CancellationToken ct)
    {
        var student = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                s.Id, s.FirstName, s.LastName, s.DateOfBirth, s.GradeLevel, s.DisabilityCategory,
                s.CaseManagerUserId, s.Status, s.IepDate, s.AnnualReviewDueDate, s.EtrDate, s.ReevaluationDueDate
            })
            .FirstOrDefaultAsync(ct);
        if (student == null)
            return;

        await WriteJsonEntryAsync(archive, $"students/{studentId}/student.json", student, manifestFiles, ct);

        var versions = await _context.AuthoredDocumentVersions.AsNoTracking()
            .Where(v => v.SchoolStudentId == studentId)
            .Select(v => new
            {
                v.Id,
                v.VersionNumber,
                DocumentTypeKey = v.DocumentType.Key,
                v.ValuesJson,
                PdfStatus = v.Pdf != null ? v.Pdf.RenderStatus : PdfRenderStatus.Pending
            })
            .ToListAsync(ct);

        foreach (var v in versions)
        {
            var basePath = $"students/{studentId}/versions/{v.DocumentTypeKey}-v{v.VersionNumber}";
            var valuesNode = ParseValuesOrEmpty(v.ValuesJson);
            await WriteJsonEntryAsync(archive, $"{basePath}.json", valuesNode, manifestFiles, ct);

            if (v.PdfStatus != PdfRenderStatus.Rendered)
                continue;

            try
            {
                var blobPath = IAuthoredDocumentPdfService.BlobPathFor(v.Id, v.VersionNumber);
                var bytes = await DownloadBytesAsync(blobPath, ct);
                await WriteBytesEntryAsync(archive, $"{basePath}.pdf", bytes, manifestFiles);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Export: could not include PDF for AuthoredDocumentVersion {VersionId}", v.Id);
            }
        }

        var artifacts = await _context.SignedArtifacts.AsNoTracking()
            .Where(a => a.AuthoredDocumentVersion.SchoolStudentId == studentId)
            .Select(a => new { a.Id, a.BlobPath, a.FileName })
            .ToListAsync(ct);

        foreach (var a in artifacts)
        {
            try
            {
                var bytes = await DownloadBytesAsync(a.BlobPath, ct);
                await WriteBytesEntryAsync(archive, $"signed/{a.Id}-{a.FileName}", bytes, manifestFiles);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Export: could not include SignedArtifact {ArtifactId}", a.Id);
            }
        }
    }

    // ---------------------------------------------------------------- Aggregate (whole-scope) files

    private async Task WriteAggregateFilesAsync(ZipArchive archive, List<int> studentIds, List<ExportManifestFileModel> manifestFiles, CancellationToken ct)
    {
        var goals = await _context.GoalRecords.AsNoTracking()
            .Where(g => studentIds.Contains(g.SchoolStudentId))
            .Select(g => new
            {
                g.Id,
                g.SchoolStudentId,
                g.LineageId,
                g.AuthoredDocumentVersionId,
                g.Domain,
                g.GoalText,
                g.Baseline,
                g.TargetCriteria,
                g.MeasurementMethod,
                g.Timeframe,
                Status = g.Status.ToString(),
                g.StatusReason,
                g.ProjectedAt,
                Observations = g.Observations.Select(o => new { o.ObservedAt, o.Value, o.Unit, o.Note }).ToList()
            })
            .ToListAsync(ct);
        await WriteJsonEntryAsync(archive, "goals.json", goals, manifestFiles, ct);

        var meetings = await _context.Meetings.AsNoTracking()
            .Where(m => studentIds.Contains(m.SchoolStudentId))
            .Select(m => new
            {
                m.Id,
                m.SchoolStudentId,
                Type = m.Type.ToString(),
                m.Title,
                m.StartsAtUtc,
                Status = m.Status.ToString(),
                Participants = m.Participants.Select(p => new
                {
                    p.UserId,
                    p.ExternalName,
                    TeamRole = p.TeamRole.ToString(),
                    p.IsRequired,
                    p.Attended
                }).ToList(),
                Decisions = _context.MeetingDecisions.Where(d => d.MeetingId == m.Id).Select(d => new
                {
                    d.Text,
                    Outcome = d.Outcome.ToString(),
                    d.TargetLabel,
                    d.CreatedAt,
                    d.AppliedAt
                }).ToList()
            })
            .ToListAsync(ct);
        await WriteJsonEntryAsync(archive, "meetings.json", meetings, manifestFiles, ct);

        var contactAttempts = await _context.FamilyContactAttempts.AsNoTracking()
            .Where(a => studentIds.Contains(a.SchoolStudentId))
            .Select(a => new { a.Id, a.SchoolStudentId, a.AttemptedAt, Method = a.Method.ToString(), Outcome = a.Outcome.ToString(), a.Note })
            .ToListAsync(ct);
        await WriteJsonEntryAsync(archive, "contact-attempts.json", contactAttempts, manifestFiles, ct);

        var offlineInput = await _context.OfflineFamilyInputs.AsNoTracking()
            .Where(o => studentIds.Contains(o.SchoolStudentId))
            .Select(o => new { o.Id, o.SchoolStudentId, o.DocumentInstanceId, o.ReceivedAt, Method = o.Method.ToString(), o.Summary })
            .ToListAsync(ct);
        await WriteJsonEntryAsync(archive, "offline-input.json", offlineInput, manifestFiles, ct);

        // Staff-visible DraftResponses ONLY — ParentDraftNote (private family prep notes) is a
        // completely separate table and is never queried here or anywhere else in this export.
        var responses = await _context.DraftResponses.AsNoTracking()
            .Where(r => studentIds.Contains(r.SharedDraftRevision.DocumentInstance.SchoolStudentId))
            .Select(r => new
            {
                r.Id,
                SchoolStudentId = r.SharedDraftRevision.DocumentInstance.SchoolStudentId,
                Kind = r.Kind.ToString(),
                r.Text,
                Status = r.Status.ToString(),
                r.StaffReply,
                r.CreatedAt,
                r.ResolvedAt
            })
            .ToListAsync(ct);
        await WriteJsonEntryAsync(archive, "responses.json", responses, manifestFiles, ct);

        var audit = await BuildAuditExtractAsync(studentIds, ct);
        await WriteJsonEntryAsync(archive, "audit.json", audit, manifestFiles, ct);
    }

    private async Task<List<object>> BuildAuditExtractAsync(List<int> studentIds, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - AuditWindow;
        var pairs = new HashSet<(string Type, int Id)>();
        foreach (var id in studentIds)
        {
            pairs.Add(("SchoolStudent", id));
            pairs.Add(("StudentEvidence", id));
        }

        var versionIds = await _context.AuthoredDocumentVersions.AsNoTracking().Where(v => studentIds.Contains(v.SchoolStudentId)).Select(v => v.Id).ToListAsync(ct);
        foreach (var id in versionIds) pairs.Add(("AuthoredDocumentVersion", id));

        var instanceIds = await _context.DocumentInstances.AsNoTracking().Where(i => studentIds.Contains(i.SchoolStudentId)).Select(i => i.Id).ToListAsync(ct);
        foreach (var id in instanceIds) pairs.Add(("DocumentInstance", id));

        var meetingIds = await _context.Meetings.AsNoTracking().Where(m => studentIds.Contains(m.SchoolStudentId)).Select(m => m.Id).ToListAsync(ct);
        foreach (var id in meetingIds) pairs.Add(("Meeting", id));

        var goalRecordIds = await _context.GoalRecords.AsNoTracking().Where(g => studentIds.Contains(g.SchoolStudentId)).Select(g => g.Id).ToListAsync(ct);
        foreach (var id in goalRecordIds) pairs.Add(("GoalRecord", id));

        var caseIds = await _context.EvaluationCases.AsNoTracking().Where(c => studentIds.Contains(c.SchoolStudentId)).Select(c => c.Id).ToListAsync(ct);
        foreach (var id in caseIds) pairs.Add(("EvaluationCase", id));

        var relevantTypes = pairs.Select(p => p.Type).Distinct().ToList();
        var rows = await _context.AccessAuditLogs.AsNoTracking()
            .Where(a => a.CreatedAt >= cutoff && relevantTypes.Contains(a.ResourceType))
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, Action = a.Action.ToString(), a.ActorUserId, a.ResourceType, a.ResourceId, a.RecipientUserId, a.CreatedAt })
            .ToListAsync(ct);

        return rows.Where(a => pairs.Contains((a.ResourceType, a.ResourceId))).Cast<object>().ToList();
    }

    // ---------------------------------------------------------------- Notification

    private async Task NotifyRequesterAsync(ExportJob job, CancellationToken ct)
    {
        try
        {
            var title = job.Status == ExportJobStatus.Completed ? "Your export is ready" : "Your export failed";
            var body = job.Status == ExportJobStatus.Completed
                ? "The export you requested has finished and is ready to download."
                : "The export you requested could not be completed. Please try again.";
            await _notifications.NotifyAsync(
                new[] { job.RequestedByUserId }, NotificationKind.ExportReady, title, body,
                "/educator/admin/exports", $"export-{job.Id}", emailImmediately: false, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue ExportReady notification for export {JobId}", job.Id);
        }
    }

    // ---------------------------------------------------------------- Zip helpers

    private async Task<byte[]> DownloadBytesAsync(string blobPath, CancellationToken ct)
    {
        await using var stream = await _blob.DownloadAsync(blobPath, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static async Task WriteJsonEntryAsync<T>(ZipArchive archive, string path, T obj, List<ExportManifestFileModel>? manifestFiles, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(obj, JsonOptions);
        await WriteBytesEntryAsyncCore(archive, path, bytes, manifestFiles, ct);
    }

    private static async Task WriteBytesEntryAsync(ZipArchive archive, string path, byte[] bytes, List<ExportManifestFileModel> manifestFiles)
        => await WriteBytesEntryAsyncCore(archive, path, bytes, manifestFiles, CancellationToken.None);

    private static async Task WriteBytesEntryAsyncCore(ZipArchive archive, string path, byte[] bytes, List<ExportManifestFileModel>? manifestFiles, CancellationToken ct)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using (var entryStream = entry.Open())
            await entryStream.WriteAsync(bytes, ct);

        manifestFiles?.Add(new ExportManifestFileModel
        {
            Path = path,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Bytes = bytes.LongLength
        });
    }

    private static JsonNode ParseValuesOrEmpty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try { return JsonNode.Parse(json) ?? new JsonObject(); }
        catch (JsonException) { return new JsonObject(); }
    }

    private void TryDeleteTempFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp export file {Path}", path); }
    }

    // ---------------------------------------------------------------- Mapping

    private static IQueryable<ExportJobModel> MapQuery(IQueryable<ExportJob> query) =>
        query.Select(j => new ExportJobModel
        {
            Id = j.Id,
            Scope = j.Scope,
            DistrictId = j.DistrictId,
            SchoolStudentId = j.SchoolStudentId,
            StudentName = j.SchoolStudent != null ? (j.SchoolStudent.FirstName + " " + j.SchoolStudent.LastName).Trim() : null,
            RequestedByUserId = j.RequestedByUserId,
            RequestedByName = null,
            Status = j.Status,
            RequestedAt = j.RequestedAt,
            StartedAt = j.StartedAt,
            CompletedAt = j.CompletedAt,
            SizeBytes = j.SizeBytes,
            Error = j.Error,
            StudentCount = j.StudentCount,
            FileCount = j.FileCount
        });

    private async Task<ExportJobModel> MapAsync(int jobId, CancellationToken ct)
    {
        var model = await MapQuery(_context.ExportJobs.AsNoTracking().Where(j => j.Id == jobId)).FirstAsync(ct);
        model.RequestedByName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == model.RequestedByUserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync(ct);
        return model;
    }
}
