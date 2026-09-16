namespace IepAssistant.Services.Models;

/// <summary>
/// Server-side mirror of the web's advisory completeness summary
/// (<c>web/src/features/document-authoring/lib/completeness.ts</c>), collapsed to the counts the plan-5
/// home surface needs (no itemized message list — that stays a client-only concern). <see cref="Percent"/>
/// = filled top-level fields / total top-level fields (a Table field counts as filled once it has any
/// row, regardless of that row's contents); <see cref="RequiredMissing"/> = blank required top-level
/// fields PLUS, for every row of a Table field, each of that row's required columns left blank.
/// </summary>
public class DocumentCompletenessModel
{
    public int Percent { get; set; }
    public int FilledCount { get; set; }
    public int TotalCount { get; set; }
    public int RequiredMissing { get; set; }
}
