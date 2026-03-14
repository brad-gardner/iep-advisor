using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IMeetingPrepService
{
    Task<IEnumerable<MeetingPrepChecklistModel>> GetByChildIdAsync(int childId, int userId, CancellationToken ct = default);
    Task<MeetingPrepChecklistModel?> GetByIdAsync(int id, int userId, CancellationToken ct = default);
    Task<ServiceResult<int>> GenerateFromGoalsAsync(int childId, int userId, CancellationToken ct = default);
    Task<ServiceResult<int>> GenerateFromIepAsync(int iepDocumentId, int userId, CancellationToken ct = default);
    Task GenerateChecklistAsync(int checklistId, CancellationToken ct = default);
    Task<ServiceResult> CheckItemAsync(int id, int userId, CheckItemRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, int userId, CancellationToken ct = default);
}
