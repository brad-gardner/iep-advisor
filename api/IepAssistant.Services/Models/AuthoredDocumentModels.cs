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

    // Plan 7, decisions 4-5.
    public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Unsigned;
    public int SignedArtifactCount { get; set; }
    public int? AmendsVersionId { get; set; }
    public int? AmendsVersionNumber { get; set; }
    public string? AmendmentReason { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<int> AmendedByVersionIds { get; set; } = new();
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

    // Plan 7, decisions 4-5.
    public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Unsigned;
    public int SignedArtifactCount { get; set; }
    public int? AmendsVersionId { get; set; }
    public int? AmendsVersionNumber { get; set; }
    public string? AmendmentReason { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public List<int> AmendedByVersionIds { get; set; } = new();

    /// <summary>The pinned template version's full section/field tree, so the client can render the finalized form.</summary>
    public TemplateVersionDetailModel TemplateVersion { get; set; } = new();
}

/// <summary>Input to <c>POST /api/authored-versions/{id}/amend</c> (plan 7, decision 5).</summary>
public class AmendDocumentVersionModel
{
    public string Reason { get; set; } = string.Empty;
    public DateTime? EffectiveDate { get; set; }
}

/// <summary>Result of a successful amend: the new Draft instance's id, ready to open in the editor.</summary>
public class AmendResultModel
{
    public int InstanceId { get; set; }
}

/// <summary>
/// PDF render status for a finalized version. Carries no download URL — the SAS is minted only by
/// <c>GetPdfDownloadUrlAsync</c>, which is where the FERPA Export audit is recorded (a poll is not an
/// export). Multilingual plan phase 7: no longer side-effect-free on a FIRST poll for a non-English
/// language — see <see cref="NeedsRender"/>.
/// </summary>
public class AuthoredDocumentPdfStatusModel
{
    public int VersionId { get; set; }

    /// <summary>Multilingual plan phase 7: the language this status reflects (the caller's resolved
    /// current UI language — see <c>SupportedLanguages.CurrentUiLanguage</c>), internal-only (not carried
    /// onto <c>AuthoredDocumentPdfStatusDto</c>) — used by the controller to enqueue a render for the SAME
    /// language <see cref="NeedsRender"/> flagged, without re-resolving it.</summary>
    public string Language { get; set; } = Localization.SupportedLanguages.English;

    /// <summary>Multilingual plan phase 7: true only when this call just created the tracking row for
    /// <see cref="Language"/> (first request for that language) — the controller enqueues a render
    /// exactly then, never on a later poll of an already-queued row. Internal-only, not carried onto the DTO.</summary>
    public bool NeedsRender { get; set; }

    public PdfRenderStatus RenderStatus { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
