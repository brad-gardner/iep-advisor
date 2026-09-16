using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// A parent's private question about a shared revision, answered by Claude grounded in the revision plus
/// the parent's OWN evidence (their child profile facts and all their own <c>ParentContribution</c>s,
/// shared or not). Persisted as a private <c>ParentDraftNote</c> — never exposed to staff.
/// </summary>
public interface IDraftQuestionService
{
    Task<ServiceResult<DraftAnswerModel>> AskAsync(int parentUserId, int revisionId, AskDraftQuestionModel model, CancellationToken ct = default);

    /// <summary>The asking parent's own notes for this revision. No staff-facing equivalent exists.</summary>
    Task<ServiceResult<List<ParentDraftNoteModel>>> GetNotesAsync(int parentUserId, int revisionId, CancellationToken ct = default);

    /// <summary>Deletes one of the caller's own notes.</summary>
    Task<ServiceResult> DeleteNoteAsync(int parentUserId, int noteId, CancellationToken ct = default);
}
