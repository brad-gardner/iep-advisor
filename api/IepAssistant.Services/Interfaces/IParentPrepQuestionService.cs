using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// The parent's own meeting-prep questions for a child (as opposed to the AI-generated checklist). Readers
/// need Viewer+ on the child, writers Collaborator+. There is deliberately no school-side read. Unknown ids
/// and missing access collapse to the same "not found".
/// </summary>
public interface IParentPrepQuestionService
{
    /// <summary>Ordered by <c>DisplayOrder</c>, then id.</summary>
    Task<ServiceResult<List<ParentPrepQuestionModel>>> GetForChildAsync(int childId, int userId, CancellationToken ct = default);

    /// <summary>
    /// Trims, strips markup and limits the text; a case-insensitive match against an existing question for
    /// the child returns that row with <see cref="ParentPrepQuestionAddResult.AlreadyExisted"/> set instead of
    /// adding a duplicate. <paramref name="source"/> is "parent" (default when null) or "advocate".
    /// </summary>
    Task<ServiceResult<ParentPrepQuestionAddResult>> AddAsync(int childId, int userId, string? text, string? source, CancellationToken ct = default);

    /// <summary>Changes the text and/or the checked state; at least one must be supplied.</summary>
    Task<ServiceResult<ParentPrepQuestionModel>> UpdateAsync(int id, int userId, string? text, bool? isChecked, CancellationToken ct = default);

    /// <summary>
    /// Puts <paramref name="orderedIds"/> first, in that order; every id must be one of this child's
    /// questions. Questions not listed keep their relative order after the listed ones.
    /// </summary>
    Task<ServiceResult<List<ParentPrepQuestionModel>>> ReorderAsync(int childId, int userId, IReadOnlyList<int> orderedIds, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken ct = default);
}
