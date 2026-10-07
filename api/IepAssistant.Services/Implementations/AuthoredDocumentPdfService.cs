using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
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
///
/// <para><b>Multilingual (phase 7):</b> renders inside <see cref="CultureScope.For"/> for the requested
/// language so <see cref="PdfLabels.From"/> resolves the right resx, and writes the (version, language)
/// row matching <paramref name="language"/> — never "the" row, since a version can now have one per
/// language. District-authored template section/field labels are never translated. See
/// <see cref="IAuthoredDocumentPdfService.BlobPathFor(int, int, string?)"/> for the blob path.</para>
///
/// <para><b>Frozen header (review fix 2026-10-07):</b> a Spanish (or other non-English) PDF can now be
/// rendered on demand, potentially weeks after the English original finalized — long enough for the
/// student's latest Held meeting/team to change. So every language's render shares ONE header, resolved
/// (and persisted to <see cref="AuthoredDocumentPdf.HeaderSnapshotJson"/> on the version's ENGLISH row)
/// the first time ANY language is rendered, via <see cref="ResolveHeaderContextAsync"/>; every later
/// render/retry in every language reuses it, even if it fails before uploading, so the first resolution
/// always wins.</para>
/// </summary>
public class AuthoredDocumentPdfService : IAuthoredDocumentPdfService
{
    private const int MaxErrorLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly ITemplateAuthoringService _authoring;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<AuthoredDocumentPdfService> _logger;
    private readonly IStringLocalizer<Pdf> _localizer;

    public AuthoredDocumentPdfService(
        ApplicationDbContext context,
        ITemplateAuthoringService authoring,
        IBlobStorageService blob,
        ILogger<AuthoredDocumentPdfService> logger,
        IStringLocalizer<Pdf> localizer)
    {
        _context = context;
        _authoring = authoring;
        _blob = blob;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task RenderAsync(int versionId, string? language = null, CancellationToken ct = default)
    {
        var normalizedLanguage = SupportedLanguages.Normalize(language) ?? SupportedLanguages.English;

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

        // The PDF tracking row is tracked (we update it) — one per (version, language). Created Pending
        // by FinalizeAsync for English, or by AuthoredDocumentVersionService.GetPdfStatusAsync on first
        // request for any other language.
        var pdf = await _context.AuthoredDocumentPdfs.FirstOrDefaultAsync(
            p => p.AuthoredDocumentVersionId == versionId && (p.Language == normalizedLanguage
                || (normalizedLanguage == SupportedLanguages.English && p.Language == null)), ct);
        if (pdf == null)
        {
            _logger.LogWarning("PDF render skipped: AuthoredDocumentPdf row for version {VersionId} language {Language} not found", versionId, normalizedLanguage);
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

            var header = await ResolveHeaderContextAsync(
                versionId, normalizedLanguage, pdf,
                version.SchoolStudentId, version.DocumentTypeKey, version.StateCode, version.AmendsVersionNumber, version.EffectiveDate, ct);

            byte[] bytes;
            using (CultureScope.For(normalizedLanguage))
            {
                var labels = PdfLabels.From(_localizer);
                var document = new AuthoredDocumentPdfDocument(
                    version.DocumentTypeDisplayName, version.VersionNumber, version.FinalizedAt, tree.Data!, version.ValuesJson, header, labels);
                bytes = document.GeneratePdf();
            }

            var checksum = Convert.ToBase64String(SHA256.HashData(bytes));

            var blobPath = IAuthoredDocumentPdfService.BlobPathFor(versionId, version.VersionNumber, normalizedLanguage);
            using var stream = new MemoryStream(bytes);
            var storedUri = await _blob.UploadAsync(blobPath, stream, "application/pdf", ct);

            pdf.RenderStatus = PdfRenderStatus.Rendered;
            pdf.BlobUri = string.IsNullOrWhiteSpace(storedUri) ? blobPath : storedUri;
            pdf.Checksum = checksum;
            pdf.RenderedAt = DateTime.UtcNow;
            pdf.ErrorMessage = null;
            pdf.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Rendered PDF for AuthoredDocumentVersion {VersionId} language {Language} ({Bytes} bytes)", versionId, normalizedLanguage, bytes.Length);
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
    /// Returns the ONE header every language of this version's PDF renders with: the frozen snapshot on
    /// the version's ENGLISH <see cref="AuthoredDocumentPdf"/> row (<see cref="AuthoredDocumentPdf.Language"/>
    /// <c>"en"</c> or null) if one is already stored there, or — the first render of any language for this
    /// version — a freshly built one, stored onto that English row's
    /// <see cref="AuthoredDocumentPdf.HeaderSnapshotJson"/> (tracked, not yet saved; whichever SaveChanges
    /// follows — success or the catch block's failure path — persists it, so the freeze holds even if
    /// THIS render then fails). <paramref name="currentPdf"/> IS the English row when
    /// <paramref name="normalizedLanguage"/> is English (the earlier lookup already matched it), so no
    /// second query is needed in that case.
    /// </summary>
    private async Task<AuthoredDocumentPdfHeaderContext> ResolveHeaderContextAsync(
        int versionId, string normalizedLanguage, AuthoredDocumentPdf currentPdf,
        int schoolStudentId, string documentTypeKey, string? stateCode, int? amendsVersionNumber, DateTime? effectiveDate, CancellationToken ct)
    {
        var englishPdf = normalizedLanguage == SupportedLanguages.English
            ? currentPdf
            : await _context.AuthoredDocumentPdfs.FirstOrDefaultAsync(
                p => p.AuthoredDocumentVersionId == versionId
                     && (p.Language == SupportedLanguages.English || p.Language == null), ct);

        if (englishPdf == null)
        {
            // Shouldn't happen — FinalizeAsync always creates the English row Pending. Build live rather
            // than throw: a missing row is not this render's fault, and there's nowhere to store a freeze.
            _logger.LogWarning(
                "PDF header freeze: no English AuthoredDocumentPdf row for version {VersionId}; building header live without storing a snapshot", versionId);
            return await BuildHeaderContextAsync(schoolStudentId, documentTypeKey, stateCode, amendsVersionNumber, effectiveDate, ct);
        }

        if (!string.IsNullOrEmpty(englishPdf.HeaderSnapshotJson))
        {
            var cached = JsonSerializer.Deserialize<AuthoredDocumentPdfHeaderContext>(englishPdf.HeaderSnapshotJson);
            if (cached != null) return cached;

            _logger.LogWarning("PDF header freeze: HeaderSnapshotJson on version {VersionId} failed to deserialize; rebuilding live (not re-stored)", versionId);
            return await BuildHeaderContextAsync(schoolStudentId, documentTypeKey, stateCode, amendsVersionNumber, effectiveDate, ct);
        }

        var header = await BuildHeaderContextAsync(schoolStudentId, documentTypeKey, stateCode, amendsVersionNumber, effectiveDate, ct);

        // First resolution for this version — freeze it onto the English row now (persisted by whichever
        // SaveChanges follows, success or failure, since englishPdf stays tracked in this same context).
        englishPdf.HeaderSnapshotJson = JsonSerializer.Serialize(header);
        englishPdf.UpdatedAt = DateTime.UtcNow;

        return header;
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

        // Goal/service/accommodation/transition rows show "Responsible: <role>" — role only, never a name
        // (plan 2026-10-02-002) — resolved here (DB access) so the document itself stays DB-free.
        var ownerRoleByUserId = await TeamRoleResolver.LoadRoleByUserIdAsync(_context, schoolStudentId, ct);

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
            EffectiveDate: effectiveDate,
            OwnerRoleByUserId: ownerRoleByUserId);
    }
}
