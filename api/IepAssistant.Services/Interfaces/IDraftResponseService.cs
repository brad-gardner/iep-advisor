using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Per-item family responses (Agree/Question/ChangeRequest/Comment) to a shared revision (plan 6, decision 4).</summary>
public interface IDraftResponseService
{
    /// <summary>Parent: submits a response. Only on an Active revision. Notifies the student's active team + the sharer.</summary>
    Task<ServiceResult<DraftResponseModel>> CreateAsync(int parentUserId, int revisionId, CreateDraftResponseModel model, CancellationToken ct = default);

    /// <summary>Parent: the caller's own responses for this revision.</summary>
    Task<ServiceResult<List<DraftResponseModel>>> GetForParentAsync(int parentUserId, int revisionId, CancellationToken ct = default);

    /// <summary>Staff: every response across the instance's revisions, optionally filtered by status.</summary>
    Task<ServiceResult<List<DraftResponseModel>>> GetForInstanceAsync(int userId, int instanceId, DraftResponseStatus? status, CancellationToken ct = default);

    /// <summary>Staff: resolves a response with a reply and/or a "resolved in the draft" flag (at least one required). Notifies the parent.</summary>
    Task<ServiceResult<DraftResponseModel>> ResolveAsync(int userId, int responseId, ResolveDraftResponseModel model, CancellationToken ct = default);
}
