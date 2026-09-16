namespace IepAssistant.Domain.Entities;

/// <summary>
/// Server-composed, cached pre-meeting brief for the LEA rep (plan 7, decision 2): deterministic parts
/// (changes vs. the last finalized version, resource-commitment items, procedural checklist, open family
/// responses/offline input/contact attempts) plus one AI-drafted plain-language <c>summary</c>. One row
/// per meeting (1:1) — an explicit regenerate replaces <see cref="BriefJson"/> in place rather than
/// versioning history, mirroring <see cref="MeetingSummary"/>'s single-row-per-meeting shape. Advisory
/// only: the team's decisions are made in the meeting, never derived from this cache.
/// </summary>
public class MeetingBrief : BaseEntity, IAuditableEntity
{
    public int MeetingId { get; set; }

    /// <summary>The full composed brief (see <c>MeetingBriefModel</c>), serialized as JSON.</summary>
    public string BriefJson { get; set; } = "{}";

    public DateTime GeneratedAt { get; set; }

    /// <summary>Provenance: the <see cref="SharedDraftRevision"/> the brief's content/diff were built from, if any.</summary>
    public int? SourceRevisionId { get; set; }

    /// <summary>Provenance: the prior finalized <see cref="AuthoredDocumentVersion"/> the diff was built against, if any.</summary>
    public int? SourceVersionId { get; set; }

    public int GeneratedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Meeting Meeting { get; set; } = null!;
}
