using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Structured IEP authoring (P4a). All access is governed by the school-side
/// SchoolStudentAccess pattern: any active access (Viewer+) may read; mutations require
/// Role &gt;= Collaborator. Editing is last-write-wins (no concurrency token); every
/// create/update stamps LastEditedByUserId/At on the entity AND the parent draft.
/// </summary>
public interface IIepDraftService
{
    // Drafts
    Task<ServiceResult<IepDraftModel>> CreateDraftAsync(int userId, int studentId, string? title, CancellationToken ct = default);
    Task<ServiceResult<List<IepDraftModel>>> ListDraftsAsync(int userId, int studentId, CancellationToken ct = default);
    Task<ServiceResult<IepDraftModel>> GetDraftAsync(int userId, int draftId, CancellationToken ct = default);

    // Sections
    Task<ServiceResult<IepDraftSectionModel>> AddSectionAsync(int userId, int draftId, UpsertIepDraftSectionModel model, CancellationToken ct = default);
    Task<ServiceResult<IepDraftSectionModel>> UpdateSectionAsync(int userId, int draftId, int id, UpsertIepDraftSectionModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteSectionAsync(int userId, int draftId, int id, CancellationToken ct = default);

    // Goals
    Task<ServiceResult<IepDraftGoalModel>> AddGoalAsync(int userId, int draftId, UpsertIepDraftGoalModel model, CancellationToken ct = default);
    Task<ServiceResult<IepDraftGoalModel>> UpdateGoalAsync(int userId, int draftId, int id, UpsertIepDraftGoalModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteGoalAsync(int userId, int draftId, int id, CancellationToken ct = default);

    // Service lines
    Task<ServiceResult<IepDraftServiceLineModel>> AddServiceLineAsync(int userId, int draftId, UpsertIepDraftServiceLineModel model, CancellationToken ct = default);
    Task<ServiceResult<IepDraftServiceLineModel>> UpdateServiceLineAsync(int userId, int draftId, int id, UpsertIepDraftServiceLineModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteServiceLineAsync(int userId, int draftId, int id, CancellationToken ct = default);

    // Accommodations
    Task<ServiceResult<IepDraftAccommodationModel>> AddAccommodationAsync(int userId, int draftId, UpsertIepDraftAccommodationModel model, CancellationToken ct = default);
    Task<ServiceResult<IepDraftAccommodationModel>> UpdateAccommodationAsync(int userId, int draftId, int id, UpsertIepDraftAccommodationModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteAccommodationAsync(int userId, int draftId, int id, CancellationToken ct = default);

    // Transition items
    Task<ServiceResult<IepDraftTransitionItemModel>> AddTransitionItemAsync(int userId, int draftId, UpsertIepDraftTransitionItemModel model, CancellationToken ct = default);
    Task<ServiceResult<IepDraftTransitionItemModel>> UpdateTransitionItemAsync(int userId, int draftId, int id, UpsertIepDraftTransitionItemModel model, CancellationToken ct = default);
    Task<ServiceResult> DeleteTransitionItemAsync(int userId, int draftId, int id, CancellationToken ct = default);
}
