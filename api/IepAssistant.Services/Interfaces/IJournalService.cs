using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Parent-private journal entries about a child. Readers need Viewer+ on the child, writers Collaborator+.
/// There is deliberately no school-side read: entries never reach a linked school team.
/// </summary>
public interface IJournalService
{
    /// <summary>Newest <c>OccurredOn</c> first (then newest created). <paramref name="take"/> caps the list (1–200).</summary>
    Task<ServiceResult<List<JournalEntryModel>>> GetForChildAsync(int childId, int userId, JournalTag? tag = null, int? take = null, CancellationToken ct = default);
    Task<ServiceResult<JournalEntryModel>> CreateAsync(int childId, int userId, SaveJournalEntryModel model, CancellationToken ct = default);
    Task<ServiceResult<JournalEntryModel>> UpdateAsync(int entryId, int userId, SaveJournalEntryModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int entryId, int userId, CancellationToken ct = default);
}
