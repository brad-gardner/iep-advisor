using System.Security.Cryptography;
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
/// Renders the immutable IepVersion aggregate into a PDF and tracks it on the (mutable) IepVersionPdf
/// row (P5b). The immutability interceptor excludes IepVersionPdf, so this service can update its
/// RenderStatus/BlobUri/Checksum/RenderedAt after rendering.
///
/// <para><b>Failure stays retryable:</b> any exception sets RenderStatus=Error + ErrorMessage and is
/// swallowed (not rethrown) so the worker continues. The IepVersion content rows are never touched,
/// so the legal record remains valid and the render can be retried.</para>
///
/// <para><b>Multilingual (phase 7):</b> renders inside <see cref="CultureScope.For"/> for the requested
/// language so <see cref="PdfLabels.From"/> resolves the right resx, and writes the (version, language)
/// row matching <paramref name="language"/> — never "the" row, since a version can now have one per
/// language. See <see cref="IIepVersionPdfService.BlobPathFor(int, int, string?)"/> for the blob path.</para>
/// </summary>
public class IepVersionPdfService : IIepVersionPdfService
{
    private const int MaxErrorLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<IepVersionPdfService> _logger;
    private readonly IStringLocalizer<Pdf> _localizer;

    public IepVersionPdfService(ApplicationDbContext context, IBlobStorageService blob, ILogger<IepVersionPdfService> logger, IStringLocalizer<Pdf> localizer)
    {
        _context = context;
        _blob = blob;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task RenderAsync(int versionId, string? language = null, CancellationToken ct = default)
    {
        var normalizedLanguage = SupportedLanguages.Normalize(language) ?? SupportedLanguages.English;

        // Load the immutable aggregate read-only (split query to avoid cartesian explosion across 5 children).
        var version = await _context.IepVersions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(v => v.Sections)
            .Include(v => v.Goals)
            .Include(v => v.ServiceLines)
            .Include(v => v.Accommodations)
            .Include(v => v.TransitionItems)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);

        if (version == null)
        {
            _logger.LogWarning("PDF render skipped: IepVersion {VersionId} not found", versionId);
            return;
        }

        // The PDF tracking row is tracked (we update it) — one per (version, language). Created Pending
        // by FinalizeAsync for English, or by IepVersionService.GetPdfStatusAsync on first request for
        // any other language.
        var pdf = await _context.IepVersionPdfs.FirstOrDefaultAsync(
            p => p.IepVersionId == versionId && (p.Language == normalizedLanguage
                || (normalizedLanguage == SupportedLanguages.English && p.Language == null)), ct);
        if (pdf == null)
        {
            _logger.LogWarning("PDF render skipped: IepVersionPdf row for version {VersionId} language {Language} not found", versionId, normalizedLanguage);
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
            byte[] bytes;
            using (CultureScope.For(normalizedLanguage))
            {
                var labels = PdfLabels.From(_localizer);
                bytes = new IepVersionPdfDocument(version, labels).GeneratePdf();
            }

            var checksum = Convert.ToBase64String(SHA256.HashData(bytes));

            var blobPath = IIepVersionPdfService.BlobPathFor(versionId, version.VersionNumber, normalizedLanguage);
            using var stream = new MemoryStream(bytes);
            var storedUri = await _blob.UploadAsync(blobPath, stream, "application/pdf", ct);

            pdf.RenderStatus = PdfRenderStatus.Rendered;
            pdf.BlobUri = string.IsNullOrWhiteSpace(storedUri) ? blobPath : storedUri;
            pdf.Checksum = checksum;
            pdf.RenderedAt = DateTime.UtcNow;
            pdf.ErrorMessage = null;
            pdf.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Rendered PDF for IepVersion {VersionId} language {Language} ({Bytes} bytes)", versionId, normalizedLanguage, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render PDF for IepVersion {VersionId}; leaving retryable Error state", versionId);

            // Failure-isolated + retryable: mark Error, never rethrow, never touch the immutable content.
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
                _logger.LogError(saveEx, "Failed to persist Error render status for IepVersion {VersionId}", versionId);
            }
        }
    }
}
