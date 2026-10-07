namespace IepAssistant.Services.Models;

public class ExplanationSectionModel
{
    public string SectionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class ExplanationItemModel
{
    public Guid FieldKey { get; set; }
    public string? RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

/// <summary>
/// Plain-language explanation of a whole <see cref="Domain.Entities.SharedDraftRevision"/> (plan 6,
/// decision 3) — one Claude call per revision, cached forever in <see cref="Domain.Entities.SharedDraftExplanation"/>.
/// </summary>
public class DraftExplanationModel
{
    public int RevisionId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ExplanationSectionModel> Sections { get; set; } = new();
    public List<ExplanationItemModel> Items { get; set; } = new();
    public string Disclaimer { get; set; } = string.Empty;

    /// <summary>"en" | "es" | null — the language this explanation was generated in (multilingual plan
    /// 2026-10-06 phase 3); null means English, including every explanation cached before this column
    /// existed. Set from the persisted <see cref="Domain.Entities.SharedDraftExplanation.Language"/> on
    /// every read, never from this model's own serialized JSON, and carried straight through — never
    /// defaulted to English here (phase 3 review fix; that decision belongs to the client, the same as
    /// every other GeneratedLanguage-bearing model).</summary>
    public string? GeneratedLanguage { get; set; }
}
