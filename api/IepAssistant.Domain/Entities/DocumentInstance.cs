namespace IepAssistant.Domain.Entities;

/// <summary>
/// An educator-authored document (IEP, ETR, 504, …) for a specific student, driven by the State
/// Document Template Engine (Phase 3). The instance pins a single <b>Published</b>
/// <see cref="DocumentTemplateVersion"/> (<see cref="DocumentTemplateVersionId"/>) at creation time so
/// its structure never shifts underneath the author, and stores the entered values as a JSON
/// <em>value-document</em> in <see cref="ValuesJson"/>.
///
/// <para><b>Value-document shape</b> (validated in the service against the pinned template schema):
/// a top-level JSON object keyed by field <c>FieldKey</c> (guid string). Scalars —
/// Text/RichText/Date/Select map to a JSON string; Checkbox maps to a JSON bool. A Table field maps to
/// an array of row objects, each keyed by <c>columnKey</c> (guid string) whose value is a scalar of the
/// column's type. Example:
/// <code>
/// {
///   "&lt;fieldKeyA&gt;": "hello",
///   "&lt;checkboxKey&gt;": true,
///   "&lt;tableKey&gt;": [ { "&lt;colKey1&gt;": "r1c1", "&lt;colKey2&gt;": "2026-01-01" } ]
/// }
/// </code>
/// Unknown field keys (not present in the pinned version) are stripped on save; RichText is sanitized to
/// a safe formatting allowlist before persisting.</para>
///
/// <para><see cref="RowVersion"/> is an optimistic-concurrency token guarding the whole value-document
/// against concurrent edits. Following the Phase 2 <see cref="DocumentTemplateVersion"/> pattern it is a
/// plain concurrency-token byte[] rotated by the service on every save (NOT a store-generated
/// <c>rowversion</c>), so it advances deterministically under both SQL Server and the SQLite test
/// provider. <see cref="LastEditedByUserId"/>/<see cref="LastEditedAt"/> mirror the last writer, as on
/// <see cref="IepDraft"/>.</para>
///
/// <para>Distinct from the legacy typed <see cref="IepDraft"/>/<see cref="IepVersion"/> aggregate and
/// from the student-upload <see cref="EtrDocument"/> system — this shares no table with either.</para>
/// </summary>
public class DocumentInstance : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }

    public int DocumentTypeId { get; set; }

    /// <summary>The pinned (immutable) Published template version this instance renders and validates against.</summary>
    public int DocumentTemplateVersionId { get; set; }

    public DocumentInstanceStatus Status { get; set; } = DocumentInstanceStatus.Draft;

    /// <summary>The value-document: a JSON object keyed by field FieldKey. Defaults to an empty object.</summary>
    public string ValuesJson { get; set; } = "{}";

    /// <summary>Optimistic-concurrency token for the value-document; rotated by the service on each save.</summary>
    public byte[]? RowVersion { get; set; }

    // Overall last-edit stamp (mirrors the last writer), same aggregate pattern as IepDraft.
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public DocumentType DocumentType { get; set; } = null!;
    public DocumentTemplateVersion DocumentTemplateVersion { get; set; } = null!;
}
