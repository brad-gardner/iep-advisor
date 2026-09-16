using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using IepAssistant.Api.DTOs.Templates;

namespace IepAssistant.Api.DTOs.Documents;

// ---- Responses ----

/// <summary>List-row / finalize-result view of a finalized authored document version.</summary>
public class AuthoredDocumentVersionSummaryDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }
    /// <summary>Serialized as a string (Pending | Rendered | Error); null when no PDF row exists.</summary>
    public string? PdfRenderStatus { get; set; }

    // Plan 7, decisions 4-5.
    public string SignatureStatus { get; set; } = string.Empty;
    public int SignedArtifactCount { get; set; }
    public int? AmendsVersionId { get; set; }
    public int? AmendsVersionNumber { get; set; }
    public string? AmendmentReason { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<int> AmendedByVersionIds { get; set; } = new();
}

/// <summary>A finalized version plus its pinned template version tree, frozen values, and PDF status.</summary>
public class AuthoredDocumentVersionDetailDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int DocumentTemplateVersionId { get; set; }
    public int VersionNumber { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }

    /// <summary>The frozen value-document as a JSON object keyed by field FieldKey.</summary>
    public JsonElement Values { get; set; }

    /// <summary>Serialized as a string (Pending | Rendered | Error); null when no PDF row exists.</summary>
    public string? PdfRenderStatus { get; set; }
    public string? PdfBlobUri { get; set; }
    public DateTime? PdfRenderedAt { get; set; }

    // Plan 7, decisions 4-5.
    public string SignatureStatus { get; set; } = string.Empty;
    public int SignedArtifactCount { get; set; }
    public int? AmendsVersionId { get; set; }
    public int? AmendsVersionNumber { get; set; }
    public string? AmendmentReason { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<int> AmendedByVersionIds { get; set; } = new();

    /// <summary>The pinned (frozen) template version's full section/field schema for rendering the document.</summary>
    public TemplateVersionDetailDto TemplateVersion { get; set; } = new();
}

// ---- Plan 7: amendments + signed artifacts ----

public class AmendDocumentVersionRequest
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
}

public class AmendResultDto
{
    public int InstanceId { get; set; }
}

public class SignedArtifactDto
{
    public int Id { get; set; }
    public int AuthoredDocumentVersionId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int UploadedByUserId { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? SignerSummary { get; set; }
}

/// <summary>PDF render status (no URL — polled; the download URL comes from the download endpoint).</summary>
public class AuthoredDocumentPdfStatusDto
{
    public int VersionId { get; set; }
    /// <summary>Serialized as a string (Pending | Rendered | Error).</summary>
    public string RenderStatus { get; set; } = string.Empty;
    public DateTime? RenderedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>A freshly-minted, short-lived download URL for a Rendered authored-document PDF.</summary>
public class AuthoredDocumentPdfDownloadDto
{
    public string Url { get; set; } = string.Empty;
}
