namespace IepAssistant.Services.Models;

/// <summary>What the brief's content/diff were composed from (plan 7, decision 2): the latest shared
/// revision when one exists, else the live draft.</summary>
public enum BriefSourceKind
{
    SharedRevision,
    Draft
}

/// <summary>Kind of resource commitment a brief calls out (plan 7, decision 2) — new/changed services
/// rows plus scalar fields whose label matches the placement/ESY/transportation/1:1 keyword set.</summary>
public enum ResourceCommitmentKind
{
    NewService,
    ChangedService,
    Placement,
    Esy,
    OneToOne,
    Transportation
}

public class BriefSourceModel
{
    public BriefSourceKind Kind { get; set; }
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class ResourceCommitmentModel
{
    public Guid FieldKey { get; set; }
    public string? RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public ResourceCommitmentKind Kind { get; set; }
    public string Detail { get; set; } = string.Empty;
}

/// <summary>One procedural checklist line. <see cref="Satisfied"/> is <c>null</c> when the item cannot be
/// evaluated (e.g. no meeting-invite notification on record), rendered as "—" rather than a false negative.</summary>
public class BriefChecklistItemModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool? Satisfied { get; set; }
    public string? Detail { get; set; }
}

/// <summary>The full composed pre-meeting brief (plan 7, decision 2) — cached verbatim in
/// <c>MeetingBrief.BriefJson</c>; a GET reads the cache, a POST recomposes and replaces it.</summary>
public class MeetingBriefModel
{
    public int MeetingId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public BriefSourceModel? Source { get; set; }

    /// <summary>Plain-language, AI-drafted, ≤ 6 sentences.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Semantic diff vs. the latest finalized version's values; null when there is no prior finalized version to diff against.</summary>
    public ChangeSummaryModel? Changes { get; set; }

    public List<ResourceCommitmentModel> ResourceCommitments { get; set; } = new();
    public List<BriefChecklistItemModel> Checklist { get; set; } = new();
    public List<DraftResponseModel> OpenFamilyResponses { get; set; } = new();
    public List<OfflineFamilyInputModel> OfflineInput { get; set; } = new();
    public List<FamilyContactAttemptModel> ContactAttempts { get; set; } = new();

    /// <summary>Multilingual plan (2026-10-06) phase 5 review fix P3-3: set explicitly by
    /// <c>MeetingBriefService.GenerateAsync</c> from <c>MeetingBrief.Disclaimer</c> (en/es), in the
    /// SAME generated language captured on <see cref="GeneratedLanguage"/> — never hardcoded English
    /// here, since this model is also rehydrated (via <c>GetAsync</c>'s deserialize) for a brief that
    /// may have been generated in Spanish. Empty only for the (deserialize-failure) fallback model.</summary>
    public string Disclaimer { get; set; } = string.Empty;

    /// <summary>The requester's language ("en"/"es") when this brief was generated — multilingual plan
    /// (2026-10-06) phase 5, mirrors <c>MeetingBrief.Language</c>. Null means English (including every
    /// brief generated before the column existed).</summary>
    public string? GeneratedLanguage { get; set; }
}
