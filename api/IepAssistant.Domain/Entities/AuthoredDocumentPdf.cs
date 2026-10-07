namespace IepAssistant.Domain.Entities;

/// <summary>
/// The rendered-PDF tracking row for an <see cref="AuthoredDocumentVersion"/> (one-to-one, State
/// Document Template Engine, Phase 4). This is the ONE mutable version-related table: the render worker
/// updates <see cref="RenderStatus"/> / <see cref="BlobUri"/> / <see cref="Checksum"/> /
/// <see cref="RenderedAt"/> after rendering. <c>ImmutableVersionInterceptor</c> intentionally excludes
/// this entity (exactly like <see cref="IepVersionPdf"/>).
/// </summary>
public class AuthoredDocumentPdf : BaseEntity, IAuditableEntity
{
    public int AuthoredDocumentVersionId { get; set; }

    /// <summary>
    /// Multilingual plan (2026-10-06) phase 7: "en"/"es" (see <c>SupportedLanguages</c>), nullable for
    /// backward compatibility — every row created before this column existed is NULL and is treated as
    /// English everywhere this is queried (never re-backfilled, mirroring <see cref="IepVersionPdf.Language"/>).
    /// One row per (AuthoredDocumentVersionId, Language): the unique index on
    /// <see cref="AuthoredDocumentVersionId"/> alone became a composite
    /// <c>(AuthoredDocumentVersionId, Language)</c> index so an English and a Spanish render of the same
    /// version coexist as separate rows instead of overwriting each other.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Review fix (2026-10-07, "freeze the authored-document PDF header"): the JSON-serialized
    /// <c>AuthoredDocumentPdfHeaderContext</c> (System.Text.Json) resolved the FIRST time any language of
    /// this version was rendered — student/district facts, the latest Held meeting's date and
    /// participants, and the amendment banner fields. Lives ONLY on the ENGLISH row (<see cref="Language"/>
    /// is <c>"en"</c> or null) regardless of which language renders; every other language's row leaves
    /// this null. Null until the first render resolves it; once set, every later render/retry in every
    /// language reuses it (nvarchar(max) — no size constraint on the serialized payload). Without this,
    /// a Spanish PDF rendered on demand weeks after finalize could show a different meeting/team than the
    /// English record, since <c>AuthoredDocumentPdfService.BuildHeaderContextAsync</c> reads live data.
    /// </summary>
    public string? HeaderSnapshotJson { get; set; }

    public PdfRenderStatus RenderStatus { get; set; } = PdfRenderStatus.Pending;

    public string? BlobUri { get; set; }
    public string? Checksum { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? RenderedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public AuthoredDocumentVersion AuthoredDocumentVersion { get; set; } = null!;
}
