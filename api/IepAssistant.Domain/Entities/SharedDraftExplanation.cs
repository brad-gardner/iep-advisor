namespace IepAssistant.Domain.Entities;

/// <summary>
/// The cached, one-time-generated plain-language AI explanation of a <see cref="SharedDraftRevision"/>
/// (plan 6, decision 3). One row per revision (1:1) — generated on the parent's first read of the
/// explanations endpoint and never regenerated, so a revision's explanation is stable for its lifetime
/// and costs exactly one Claude call.
/// </summary>
public class SharedDraftExplanation : BaseEntity, IAuditableEntity
{
    public int SharedDraftRevisionId { get; set; }

    /// <summary>JSON-serialized <c>{ sections: [...], items: [...] }</c> (see <c>DraftExplanationModel</c>).</summary>
    public string ExplanationJson { get; set; } = "{}";

    public DateTime GeneratedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SharedDraftRevision SharedDraftRevision { get; set; } = null!;
}
