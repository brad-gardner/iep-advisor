using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// P4 staff invite lifecycle + staff management. All authorization is resolved per-request from the
/// caller's active <c>StaffContext</c> (DB-backed). Invite tokens follow the shared hash-stored,
/// single-use, email-bound, 14-day pattern (<c>InviteTokenHelper</c>). Accept is anonymous and mints a JWT.
/// </summary>
public interface IStaffInviteService
{
    Task<ServiceResult<StaffInviteModel>> InviteAsync(int callerUserId, CreateStaffInviteModel model, CancellationToken ct = default);
    Task<ServiceResult<StaffListModel>> ListAsync(int callerUserId, CancellationToken ct = default);
    Task<ServiceResult> RevokeAsync(int callerUserId, int inviteId, CancellationToken ct = default);
    Task<ServiceResult<StaffInviteModel>> ResendAsync(int callerUserId, int inviteId, CancellationToken ct = default);

    Task<ServiceResult<DeactivateStaffResult>> DeactivateStaffAsync(int callerUserId, int staffProfileId, CancellationToken ct = default);
    Task<ServiceResult> ReactivateStaffAsync(int callerUserId, int staffProfileId, CancellationToken ct = default);

    // Anonymous accept flow.
    Task<StaffInvitePreviewModel?> PreviewAsync(string token, CancellationToken ct = default);
    Task<AcceptStaffInviteResult> AcceptAsync(AcceptStaffInviteModel model, CancellationToken ct = default);
}
