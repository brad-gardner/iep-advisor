namespace IepAssistant.Domain.Entities;

/// <summary>
/// Immutable, finalized snapshot of an <see cref="IepDraft"/> (P5a). Once written, the version
/// row and all its content children may never be modified or deleted — enforced by
/// <c>ImmutableVersionInterceptor</c>. The one exception is the related <see cref="IepVersionPdf"/>,
/// which the render worker (P5b) updates after rendering.
///
/// Setters are intentionally normal (not init-only) to avoid EF materialization friction; the
/// interceptor is the immutability guard.
/// </summary>
public class IepVersion : BaseEntity, IAuditableEntity
{
    public int SchoolStudentId { get; set; }
    public int SourceDraftId { get; set; }
    public int VersionNumber { get; set; }
    public IepDocumentType DocumentType { get; set; } = IepDocumentType.Iep;
    public string? Title { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SchoolStudent SchoolStudent { get; set; } = null!;

    public ICollection<IepVersionSection> Sections { get; set; } = new List<IepVersionSection>();
    public ICollection<IepVersionGoal> Goals { get; set; } = new List<IepVersionGoal>();
    public ICollection<IepVersionServiceLine> ServiceLines { get; set; } = new List<IepVersionServiceLine>();
    public ICollection<IepVersionAccommodation> Accommodations { get; set; } = new List<IepVersionAccommodation>();
    public ICollection<IepVersionTransitionItem> TransitionItems { get; set; } = new List<IepVersionTransitionItem>();

    public IepVersionPdf? Pdf { get; set; }
}
