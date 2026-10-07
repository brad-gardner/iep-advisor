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

    /// <summary>The language ("en"/"es") this explanation was generated in (multilingual plan 2026-10-06
    /// phase 3, migration AddAiArtifactLanguage) — set from the FIRST reading parent's UI culture when
    /// <c>DraftExplanationService.GetOrGenerateAsync</c> first populates this cached, one-time row. Since
    /// the explanation is never regenerated, a later reader in the other language sees this value and a
    /// "Generated in …" notice rather than a silently re-generated answer. Null means English, including
    /// every explanation generated before this column existed.</summary>
    public string? Language { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public SharedDraftRevision SharedDraftRevision { get; set; } = null!;
}
