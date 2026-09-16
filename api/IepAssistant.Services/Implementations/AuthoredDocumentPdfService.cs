using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using QuestPDF.Fluent;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Renders a finalized <see cref="AuthoredDocumentVersion"/> against its pinned template tree into a PDF
/// and tracks it on the (mutable) <see cref="AuthoredDocumentPdf"/> row (State Document Template Engine,
/// Phase 4). The dynamic-template equivalent of <see cref="IepVersionPdfService"/>. The immutability
/// interceptor excludes <see cref="AuthoredDocumentPdf"/>, so this service can update its
/// RenderStatus/BlobUri/Checksum/RenderedAt after rendering.
///
/// <para><b>Failure stays retryable:</b> any exception (including an unhandled field type thrown by the
/// composer) sets RenderStatus=Error + ErrorMessage and is swallowed (not rethrown) so the worker
/// continues. The frozen version content is never touched, so the legal record stays valid and the render
/// can be retried.</para>
/// </summary>
public class AuthoredDocumentPdfService : IAuthoredDocumentPdfService
{
    private const int MaxErrorLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly ITemplateAuthoringService _authoring;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<AuthoredDocumentPdfService> _logger;

    public AuthoredDocumentPdfService(
        ApplicationDbContext context,
        ITemplateAuthoringService authoring,
        IBlobStorageService blob,
        ILogger<AuthoredDocumentPdfService> logger)
    {
        _context = context;
        _authoring = authoring;
        _blob = blob;
        _logger = logger;
    }

    public async Task RenderAsync(int versionId, CancellationToken ct = default)
    {
        // Load the immutable version read-only as a flat scalar projection (the pinned section/field tree
        // is loaded separately below via the authoring tree builder).
        var version = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new
            {
                v.Id,
                v.SchoolStudentId,
                v.VersionNumber,
                v.FinalizedAt,
                v.DocumentTemplateVersionId,
                v.ValuesJson,
                DocumentTypeKey = v.DocumentType.Key,
                DocumentTypeDisplayName = v.DocumentType.DisplayName,
                StateCode = v.DocumentTemplateVersion.DocumentTemplate.StateCode,
                v.AmendsVersionId,
                v.EffectiveDate,
                AmendsVersionNumber = v.AmendsVersion != null ? (int?)v.AmendsVersion.VersionNumber : null
            })
            .FirstOrDefaultAsync(ct);

        if (version == null)
        {
            _logger.LogWarning("PDF render skipped: AuthoredDocumentVersion {VersionId} not found", versionId);
            return;
        }

        // The PDF tracking row is tracked (we update it). It is created Pending by FinalizeAsync.
        var pdf = await _context.AuthoredDocumentPdfs.FirstOrDefaultAsync(p => p.AuthoredDocumentVersionId == versionId, ct);
        if (pdf == null)
        {
            _logger.LogWarning("PDF render skipped: AuthoredDocumentPdf row for version {VersionId} not found", versionId);
            return;
        }

        // Show Pending while rendering (covers a retry from Error).
        if (pdf.RenderStatus != PdfRenderStatus.Pending)
        {
            pdf.RenderStatus = PdfRenderStatus.Pending;
            pdf.ErrorMessage = null;
            pdf.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        try
        {
            // Load the pinned, frozen template tree (reuses the Phase 2 read-only tree builder).
            var tree = await _authoring.GetVersionAsync(version.DocumentTemplateVersionId, ct);
            if (!tree.Success)
                throw new InvalidOperationException(tree.Message ?? "The pinned template version could not be loaded.");

            var header = await BuildHeaderContextAsync(version.SchoolStudentId, version.DocumentTypeKey, version.StateCode, version.AmendsVersionNumber, version.EffectiveDate, ct);

            var document = new AuthoredDocumentPdfDocument(
                version.DocumentTypeDisplayName, version.VersionNumber, version.FinalizedAt, tree.Data!, version.ValuesJson, header);
            var bytes = document.GeneratePdf();

            var checksum = Convert.ToBase64String(SHA256.HashData(bytes));

            var blobPath = IAuthoredDocumentPdfService.BlobPathFor(versionId, version.VersionNumber);
            using var stream = new MemoryStream(bytes);
            var storedUri = await _blob.UploadAsync(blobPath, stream, "application/pdf", ct);

            pdf.RenderStatus = PdfRenderStatus.Rendered;
            pdf.BlobUri = string.IsNullOrWhiteSpace(storedUri) ? blobPath : storedUri;
            pdf.Checksum = checksum;
            pdf.RenderedAt = DateTime.UtcNow;
            pdf.ErrorMessage = null;
            pdf.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Rendered PDF for AuthoredDocumentVersion {VersionId} ({Bytes} bytes)", versionId, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render PDF for AuthoredDocumentVersion {VersionId}; leaving retryable Error state", versionId);

            // Failure-isolated + retryable: mark Error, never rethrow, never touch the frozen content.
            try
            {
                var message = ex.Message;
                if (message.Length > MaxErrorLength) message = message[..MaxErrorLength];

                pdf.RenderStatus = PdfRenderStatus.Error;
                pdf.ErrorMessage = message;
                pdf.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist Error render status for AuthoredDocumentVersion {VersionId}", versionId);
            }
        }
    }

    /// <summary>
    /// Resolves everything the OH form layout needs beyond the frozen values (plan 7, decision 9): the
    /// student/district/date facts, the latest Held meeting's date and participants, and the amendment
    /// banner fields. Cheap for a generic (state-less) template too — the document simply ignores it.
    /// </summary>
    private async Task<AuthoredDocumentPdfHeaderContext> BuildHeaderContextAsync(
        int schoolStudentId, string documentTypeKey, string? stateCode, int? amendsVersionNumber, DateTime? effectiveDate, CancellationToken ct)
    {
        var student = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => new
            {
                s.FirstName, s.LastName, s.DateOfBirth, s.IepDate, s.EtrDate,
                DistrictName = s.District.Name
            })
            .FirstOrDefaultAsync(ct);

        var latestHeldMeeting = await _context.Meetings.AsNoTracking()
            .Where(m => m.SchoolStudentId == schoolStudentId && m.Status == MeetingStatus.Held)
            .OrderByDescending(m => m.StartsAtUtc)
            .Select(m => new { m.Id, m.StartsAtUtc })
            .FirstOrDefaultAsync(ct);

        var participants = latestHeldMeeting == null
            ? new List<AuthoredDocumentPdfParticipant>()
            : await _context.MeetingParticipants.AsNoTracking()
                .Where(p => p.MeetingId == latestHeldMeeting.Id)
                .Select(p => new AuthoredDocumentPdfParticipant(
                    p.UserId != null ? (p.User!.FirstName + " " + p.User!.LastName).Trim() : (p.ExternalName ?? "Unknown"),
                    p.TeamRole.ToString(),
                    p.Attended))
                .ToListAsync(ct);

        return new AuthoredDocumentPdfHeaderContext(
            StateCode: stateCode,
            DocumentTypeKey: documentTypeKey,
            StudentFirstName: student?.FirstName ?? string.Empty,
            StudentLastName: student?.LastName,
            StudentDateOfBirth: student?.DateOfBirth,
            DistrictName: student?.DistrictName,
            IepDate: student?.IepDate,
            EtrDate: student?.EtrDate,
            MeetingDate: latestHeldMeeting?.StartsAtUtc,
            Participants: participants,
            AmendsVersionNumber: amendsVersionNumber,
            EffectiveDate: effectiveDate);
    }
}
