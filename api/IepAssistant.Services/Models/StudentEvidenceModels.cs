namespace IepAssistant.Services.Models;

/// <summary>What kind of evidence an item is; drives prompt grouping, prefill and UI grouping.</summary>
public enum EvidenceKind
{
    Identity,
    TeamMember,
    PriorGoal,
    PriorService,
    PriorAccommodation,
    PriorTransition,
    PresentLevels,
    EtrFinding,
    StudentVoice,
    ParentContribution
}

/// <summary>
/// One citable fact about a student. <see cref="Id"/> is stable within a bundle ("E7") so an AI
/// suggestion can cite it and the UI can open it; <see cref="SourceLabel"/>/<see cref="SourceDate"/>
/// are what a case manager sees ("IEP v2 — finalized 2025-10-14").
/// </summary>
public sealed class EvidenceItem
{
    public required string Id { get; init; }
    public required EvidenceKind Kind { get; init; }
    /// <summary>"SchoolStudent" | "AuthoredDocumentVersion" | "IepVersion" | "StudentWorkspaceEntry" | "ParentContribution" | "StaffProfile"</summary>
    public required string SourceType { get; init; }
    public required int SourceId { get; init; }
    public required string SourceLabel { get; init; }
    public DateTime? SourceDate { get; init; }
    /// <summary>"school" | "student" | "family" | "system"</summary>
    public required string AuthorRole { get; init; }
    public required string Text { get; init; }
    /// <summary>For rows carried out of a prior finalized version: the row's stable `_rowId`.</summary>
    public string? RowId { get; init; }
    /// <summary>For structured rows: semantic → value (goalText, baseline, …) so prefill can remap by semantic.</summary>
    public IReadOnlyDictionary<string, string>? Fields { get; init; }
}

public sealed class StudentEvidenceBundle
{
    public required int SchoolStudentId { get; init; }
    public required IReadOnlyList<EvidenceItem> Items { get; init; }
    /// <summary>The finalized versions the structured items came from (for provenance chips).</summary>
    public required IReadOnlyList<EvidenceSource> Sources { get; init; }
}

public sealed class EvidenceSource
{
    public required string SourceType { get; init; }
    public required int SourceId { get; init; }
    public required string Label { get; init; }
    public DateTime? Date { get; init; }
    /// <summary>Document type key for versions ("IEP", "ETR", "Section504").</summary>
    public string? DocumentTypeKey { get; init; }
}
