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

    /// <summary>The pinned (frozen) template version's full section/field schema for rendering the document.</summary>
    public TemplateVersionDetailDto TemplateVersion { get; set; } = new();
}

/// <summary>PDF render status + (when Rendered) a short-lived download URL.</summary>
public class AuthoredDocumentPdfStatusDto
{
    public int VersionId { get; set; }
    /// <summary>Serialized as a string (Pending | Rendered | Error).</summary>
    public string RenderStatus { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
