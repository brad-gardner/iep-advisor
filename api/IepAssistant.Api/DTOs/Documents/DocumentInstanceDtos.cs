using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using IepAssistant.Api.DTOs.Templates;

namespace IepAssistant.Api.DTOs.Documents;

// ---- Requests ----

public class CreateDocumentInstanceRequest
{
    /// <summary>The document type (from the DocumentType lookup) to author for this student.</summary>
    [Required]
    public int DocumentTypeId { get; set; }
}

public class SaveDocumentValuesRequest
{
    /// <summary>
    /// A <c>{fieldKey: value}</c> patch merged into the value-document. Scalars: Text/RichText/Date/
    /// Select are JSON strings, Checkbox is a JSON bool; Table is an array of row objects keyed by
    /// columnKey. Unknown field keys are ignored server-side; a JSON null clears a field.
    /// </summary>
    public Dictionary<string, JsonElement> Values { get; set; } = new();

    /// <summary>Base64-encoded optimistic-concurrency token echoed from the last read; optional but recommended.</summary>
    public string? RowVersion { get; set; }
}

// ---- Responses ----

/// <summary>An instance plus its pinned template version tree and the value-document.</summary>
public class DocumentInstanceDetailDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int DocumentTemplateVersionId { get; set; }
    /// <summary>Serialized as a string (Draft | Finalizing | Finalized).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>The value-document as a JSON object keyed by field FieldKey.</summary>
    public JsonElement Values { get; set; }

    /// <summary>Base64-encoded optimistic-concurrency token to echo on the next save.</summary>
    public string? RowVersion { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public int? LastEditedByUserId { get; set; }

    /// <summary>Plan 7: present when this draft amends a finalized version (the editor shows a banner).</summary>
    public int? AmendsVersionId { get; set; }
    public int? AmendsVersionNumber { get; set; }
    public string? AmendmentReason { get; set; }
    public DateTime? EffectiveDate { get; set; }

    /// <summary>The pinned template version's full section/field schema for rendering the form.</summary>
    public TemplateVersionDetailDto TemplateVersion { get; set; } = new();
}

/// <summary>
/// Lightweight response to a value save: the normalized value-document + the rotated concurrency token.
/// The pinned template tree is immutable and already held by the client, so it is not re-sent per save.
/// </summary>
public class DocumentInstanceValuesDto
{
    /// <summary>The value-document as a JSON object keyed by field FieldKey (after merge + normalization).</summary>
    public JsonElement Values { get; set; }

    /// <summary>Base64-encoded optimistic-concurrency token to echo on the next save.</summary>
    public string? RowVersion { get; set; }

    /// <summary>
    /// Field-level warnings from normalizing this save (e.g. an owner that is not an active team member
    /// was dropped). Empty when there is nothing to warn about.
    /// </summary>
    public List<DocumentSaveWarningDto> Warnings { get; set; } = new();
}

/// <summary>One field/row-level save warning (see <see cref="DocumentInstanceValuesDto.Warnings"/>).</summary>
public class DocumentSaveWarningDto
{
    /// <summary>The table field's FieldKey (string GUID) the affected row belongs to.</summary>
    public string FieldKey { get; set; } = string.Empty;

    /// <summary>The affected row's <c>_rowId</c> (string GUID).</summary>
    public string RowId { get; set; } = string.Empty;

    /// <summary>Stable machine-readable reason, e.g. <c>"ownerNotTeamMember"</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-readable message, safe to show directly next to the affected control.</summary>
    public string Message { get; set; } = string.Empty;
}

public class DocumentInstanceSummaryDto
{
    public int Id { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    /// <summary>Serialized as a string (Draft | Finalizing | Finalized).</summary>
    public string Status { get; set; } = string.Empty;
    public int DocumentTemplateVersionId { get; set; }
    public int TemplateVersionNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastEditedAt { get; set; }
}
