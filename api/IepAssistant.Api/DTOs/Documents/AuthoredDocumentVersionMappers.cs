using System.Text.Json;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Documents;

internal static class AuthoredDocumentVersionMappers
{
    public static AuthoredDocumentVersionSummaryDto MapSummary(AuthoredDocumentVersionSummaryModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        DocumentTypeId = m.DocumentTypeId,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        VersionNumber = m.VersionNumber,
        FinalizedByUserId = m.FinalizedByUserId,
        FinalizedAt = m.FinalizedAt,
        PdfRenderStatus = m.PdfRenderStatus?.ToString(),
        SignatureStatus = m.SignatureStatus.ToString(),
        SignedArtifactCount = m.SignedArtifactCount,
        AmendsVersionId = m.AmendsVersionId,
        AmendsVersionNumber = m.AmendsVersionNumber,
        AmendmentReason = m.AmendmentReason,
        EffectiveDate = m.EffectiveDate,
        AmendedByVersionIds = m.AmendedByVersionIds
    };

    public static AuthoredDocumentVersionDetailDto MapDetail(AuthoredDocumentVersionDetailModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        DocumentTypeId = m.DocumentTypeId,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        DocumentTemplateVersionId = m.DocumentTemplateVersionId,
        VersionNumber = m.VersionNumber,
        FinalizedByUserId = m.FinalizedByUserId,
        FinalizedAt = m.FinalizedAt,
        Values = ParseValues(m.ValuesJson),
        PdfRenderStatus = m.PdfRenderStatus?.ToString(),
        PdfBlobUri = m.PdfBlobUri,
        PdfRenderedAt = m.PdfRenderedAt,
        SignatureStatus = m.SignatureStatus.ToString(),
        SignedArtifactCount = m.SignedArtifactCount,
        AmendsVersionId = m.AmendsVersionId,
        AmendsVersionNumber = m.AmendsVersionNumber,
        AmendmentReason = m.AmendmentReason,
        EffectiveDate = m.EffectiveDate,
        AmendedByVersionIds = m.AmendedByVersionIds,
        TemplateVersion = DocumentTemplateMappers.MapVersionDetail(m.TemplateVersion)
    };

    public static SignedArtifactDto MapSignedArtifact(SignedArtifactModel m) => new()
    {
        Id = m.Id,
        AuthoredDocumentVersionId = m.AuthoredDocumentVersionId,
        FileName = m.FileName,
        ContentType = m.ContentType,
        SizeBytes = m.SizeBytes,
        UploadedByUserId = m.UploadedByUserId,
        UploadedByName = m.UploadedByName,
        UploadedAt = m.UploadedAt,
        SignerSummary = m.SignerSummary
    };

    public static AuthoredDocumentPdfStatusDto MapPdfStatus(AuthoredDocumentPdfStatusModel m) => new()
    {
        VersionId = m.VersionId,
        RenderStatus = m.RenderStatus.ToString(),
        RenderedAt = m.RenderedAt,
        ErrorMessage = m.ErrorMessage
    };

    /// <summary>Emits the frozen value-document as a JSON object; a blank/invalid store becomes <c>{}</c>.</summary>
    private static JsonElement ParseValues(string? valuesJson)
    {
        if (!string.IsNullOrWhiteSpace(valuesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(valuesJson);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // fall through to empty object
            }
        }

        using var empty = JsonDocument.Parse("{}");
        return empty.RootElement.Clone();
    }
}
