using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>
/// The result of resolving which <see cref="DocumentTemplateVersion"/> a new instance should pin for a
/// given <c>(state, documentType)</c> (State Document Template Engine, Phase 3). Points at the highest
/// Published version of the best-matching template (state-specific preferred, else the default).
/// </summary>
public class TemplateResolutionModel
{
    public int DocumentTemplateId { get; set; }
    public int DocumentTemplateVersionId { get; set; }
    public int VersionNumber { get; set; }

    /// <summary>The resolved template's state, or null when the default (state-less) template was used.</summary>
    public string? StateCode { get; set; }

    /// <summary>True when the state-specific template had no Published version and the default was used.</summary>
    public bool UsedDefault { get; set; }
}

/// <summary>Full view of a <see cref="DocumentInstance"/> including the pinned template version tree and the value-document.</summary>
public class DocumentInstanceDetailModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int DocumentTemplateVersionId { get; set; }
    public DocumentInstanceStatus Status { get; set; }

    /// <summary>The value-document JSON (object keyed by field FieldKey).</summary>
    public string ValuesJson { get; set; } = "{}";

    /// <summary>Optimistic-concurrency token to echo on the next save.</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>Plan 7: set when this draft was opened as an amendment of a finalized version.</summary>
    public int? AmendsVersionId { get; set; }
    public int? AmendsVersionNumber { get; set; }
    public string? AmendmentReason { get; set; }
    public DateTime? EffectiveDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public int? LastEditedByUserId { get; set; }

    /// <summary>The pinned template version's full section/field tree, so the client can render the form.</summary>
    public TemplateVersionDetailModel TemplateVersion { get; set; } = new();
}

/// <summary>
/// Lightweight result of a value save. The pinned template tree is immutable for a Draft and already
/// held by the client, so a save returns only the (possibly re-normalized) value-document + the rotated
/// concurrency token — not the whole detail tree (which would re-query + re-ship the schema on every
/// autosave tick).
/// </summary>
public class DocumentInstanceValuesModel
{
    /// <summary>The stored value-document JSON after the patch was merged + normalized.</summary>
    public string ValuesJson { get; set; } = "{}";

    /// <summary>The rotated optimistic-concurrency token to echo on the next save.</summary>
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Field-level warnings produced while normalizing this save — e.g. a row's <c>_ownerUserId</c> was
    /// dropped because that user is not an active member of the student's team. Additive (plan
    /// 2026-10-02-002): empty on every save that had nothing to warn about, including every save before
    /// this field existed.
    /// </summary>
    public IReadOnlyList<DocumentSaveWarningModel> Warnings { get; set; } = Array.Empty<DocumentSaveWarningModel>();
}

/// <summary>One field/row-level warning surfaced alongside an otherwise-successful save (see
/// <see cref="DocumentInstanceValuesModel.Warnings"/>).</summary>
public class DocumentSaveWarningModel
{
    /// <summary>The table field's FieldKey (string GUID) the affected row belongs to.</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>The affected row's <c>_rowId</c> (string GUID), so the client can surface the warning next to that row's control.</summary>
    public string RowId { get; set; } = string.Empty;

    /// <summary>Stable machine-readable reason, e.g. <c>"ownerNotTeamMember"</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable message, safe to show directly.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>List-row view of a student's instances.</summary>
public class DocumentInstanceSummaryModel
{
    public int Id { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public DocumentInstanceStatus Status { get; set; }
    public int DocumentTemplateVersionId { get; set; }
    public int TemplateVersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastEditedAt { get; set; }
}
