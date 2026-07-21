using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>
/// Lightweight summary of a finalized <see cref="AuthoredDocumentVersion"/> (State Document Template
/// Engine, Phase 4). Returned by list endpoints and by FinalizeAsync.
/// </summary>
public class AuthoredDocumentVersionSummaryModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }
    public PdfRenderStatus? PdfRenderStatus { get; set; }
}

/// <summary>
/// Full view of a finalized version: metadata + the frozen value-document + the pinned template version
/// tree (so a client can render the finalized document), plus PDF availability.
/// </summary>
public class AuthoredDocumentVersionDetailModel
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

    /// <summary>The frozen value-document JSON (object keyed by field FieldKey).</summary>
    public string ValuesJson { get; set; } = "{}";

    public PdfRenderStatus? PdfRenderStatus { get; set; }
    public string? PdfBlobUri { get; set; }
    public DateTime? PdfRenderedAt { get; set; }

    /// <summary>The pinned template version's full section/field tree, so the client can render the finalized form.</summary>
    public TemplateVersionDetailModel TemplateVersion { get; set; } = new();
}

/// <summary>PDF availability for a finalized version. Url is set only when RenderStatus is Rendered.</summary>
public class AuthoredDocumentPdfStatusModel
{
    public int VersionId { get; set; }
    public PdfRenderStatus RenderStatus { get; set; }
    public string? Url { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
