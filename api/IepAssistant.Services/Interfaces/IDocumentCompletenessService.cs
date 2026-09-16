using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Server-side draft completeness (plan 5, deliverable B) — mirrors the required-field/blank rules in
/// <c>web/src/features/document-authoring/lib/completeness.ts</c> so the home surface's percent/missing
/// counts agree with what the authoring UI shows. No access check here: callers (e.g.
/// <see cref="IHomeService"/>) have already authorized the student/instance the values came from.
/// </summary>
public interface IDocumentCompletenessService
{
    /// <summary>
    /// Pure: no DB access. <paramref name="sections"/> is a template version's section/field tree (in any
    /// order — display order is applied internally); <paramref name="valuesJson"/> is the instance's
    /// value-document (same shape <see cref="IDocumentInstanceService"/> persists).
    /// </summary>
    DocumentCompletenessModel Compute(IReadOnlyList<TemplateSectionModel> sections, string? valuesJson);

    /// <summary>Loads the instance's pinned template version and values, then <see cref="Compute"/>.</summary>
    Task<ServiceResult<DocumentCompletenessModel>> ComputeAsync(int instanceId, CancellationToken ct = default);
}
