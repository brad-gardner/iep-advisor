using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Deliberate whole-draft sharing with the family (plan 6, decisions 1, 2, 5, 7). A share freezes the
/// instance's current value-document into an immutable <c>SharedDraftRevision</c>; later staff edits are
/// invisible to the family until the next deliberate share. Also owns the staff converge view and the
/// family's "reviewed" acknowledgement (folded in rather than a separate pass-through service).
/// </summary>
public interface IDraftSharingService
{
    /// <summary>Staff: who a share would notify right now, and whether the policy/prior-share state allows it. Collaborator+ on the student.</summary>
    Task<ServiceResult<RecipientPreviewModel>> PreviewRecipientsAsync(int userId, int instanceId, CancellationToken ct = default);

    /// <summary>Staff: freezes the current value-document into a new revision, supersedes the previous Active one, computes the change summary, and notifies recipients.</summary>
    Task<ServiceResult<SharedDraftRevisionModel>> ShareAsync(int userId, int instanceId, string? message, CancellationToken ct = default);

    /// <summary>Staff: every revision for this instance, newest first, with acknowledgements + open response counts.</summary>
    Task<ServiceResult<List<SharedDraftRevisionModel>>> ListForInstanceAsync(int userId, int instanceId, CancellationToken ct = default);

    /// <summary>Staff: withdraws an Active revision (marks Withdrawn, notifies recipients).</summary>
    Task<ServiceResult<SharedDraftRevisionModel>> WithdrawAsync(int userId, int instanceId, int revisionId, CancellationToken ct = default);

    /// <summary>Staff: the converge view for an instance (open/resolved responses, live-vs-latest change summary, acknowledgements, canShare).</summary>
    Task<ServiceResult<ConvergeModel>> GetConvergeAsync(int userId, int instanceId, CancellationToken ct = default);

    /// <summary>Parent: every revision (all statuses) for a linked child, newest first.</summary>
    Task<ServiceResult<List<SharedDraftRevisionModel>>> ListForParentAsync(int parentUserId, int childId, CancellationToken ct = default);

    /// <summary>Parent: one revision's frozen values + pinned template schema. Withdrawn/superseded revisions remain readable (flagged).</summary>
    Task<ServiceResult<SharedDraftRevisionDetailModel>> GetForParentAsync(int parentUserId, int revisionId, CancellationToken ct = default);

    /// <summary>Parent: "I've reviewed this" stamp — idempotent upsert, never consent/signature.</summary>
    Task<ServiceResult<SharedDraftRevisionModel>> AcknowledgeAsync(int parentUserId, int revisionId, CancellationToken ct = default);
}
