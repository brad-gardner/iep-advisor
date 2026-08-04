namespace IepAssistant.Domain.Entities;

/// <summary>
/// Immutable, finalized snapshot of a <see cref="DocumentInstance"/> (State Document Template Engine,
/// Phase 4). Finalizing an instance validates its value-document against the pinned template schema and
/// deep-copies it here: the entered values are frozen in <see cref="ValuesJson"/> and the pinned
/// (already-immutable Published) <see cref="DocumentTemplateVersionId"/> is carried verbatim so the
/// snapshot always renders against the exact structure it was authored on.
///
/// <para>Once written, this row and its <see cref="ValuesJson"/> may never be modified or deleted —
/// enforced by <c>ImmutableVersionInterceptor</c>, mirroring <see cref="IepVersion"/>. The one exception
/// is the related <see cref="AuthoredDocumentPdf"/>, which the render worker updates after rendering.</para>
///
/// <para><see cref="VersionNumber"/> is scoped per <c>(SchoolStudentId, DocumentTypeId)</c> — a student
/// can hold IEP v1 and ETR v1 simultaneously and their numbers never interleave (unique index +
/// serializable finalize, mirroring <see cref="IepVersion"/>'s <c>(SchoolStudentId, VersionNumber)</c>).</para>
///
/// <para>Setters are intentionally normal (not init-only) to avoid EF materialization friction; the
/// interceptor is the immutability guard.</para>
/// </summary>
public class AuthoredDocumentVersion : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }

    public int DocumentTypeId { get; set; }

    /// <summary>The pinned (immutable Published) template version this snapshot was validated + rendered against. FK Restrict — never destroy the structure the frozen values are keyed to.</summary>
    public int DocumentTemplateVersionId { get; set; }

    /// <summary>Monotonic per <c>(SchoolStudentId, DocumentTypeId)</c>. VersionNumber = max+1 at finalize.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Frozen snapshot of the instance's value-document (JSON object keyed by field FieldKey). Immutable.</summary>
    public string ValuesJson { get; set; } = "{}";

    public int FinalizedByUserId { get; set; }

    public DateTime FinalizedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;
    public DocumentType DocumentType { get; set; } = null!;
    public DocumentTemplateVersion DocumentTemplateVersion { get; set; } = null!;

    /// <summary>The rendered-PDF tracking row (one-to-one). The ONE mutable child — the render worker updates it.</summary>
    public AuthoredDocumentPdf? Pdf { get; set; }
}
