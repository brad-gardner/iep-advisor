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
