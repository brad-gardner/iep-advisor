using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IStudentInviteService
{
    /// <summary>PARENT action: invite a student (by email) to link to a ChildProfile the parent owns.</summary>
    Task<ServiceResult<StudentInviteModel>> InviteFromParentAsync(int parentUserId, int childProfileId, string studentEmail, CancellationToken ct = default);

    /// <summary>EDUCATOR action: invite a student (by email) to link to a SchoolStudent (SchoolId-bound access).</summary>
    Task<ServiceResult<StudentInviteModel>> InviteFromEducatorAsync(int educatorUserId, int schoolStudentId, string studentEmail, CancellationToken ct = default);

    /// <summary>STUDENT action: preview an invite token before consenting (context for the consent screen).</summary>
    Task<ServiceResult<StudentInvitePreviewModel>> PreviewInviteAsync(int userId, string token, CancellationToken ct = default);

    /// <summary>
    /// STUDENT action: accept an invite. CONSENT GATE — if consentAccepted is false the account is NOT
    /// activated. On success: flips the user's Role to Student, finds-or-creates their single
    /// StudentProfile, records consent, and links the invite's side onto the profile (one pair max).
    /// </summary>
    Task<ServiceResult<AcceptStudentInviteModel>> AcceptInviteAsync(int studentUserId, string token, bool consentAccepted, CancellationToken ct = default);
}
