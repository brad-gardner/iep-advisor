using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IChildLinkService
{
    /// <summary>EDUCATOR action: invite a parent (by email) to link to a school student.</summary>
    Task<ServiceResult<ChildLinkModel>> InviteParentAsync(int educatorUserId, int studentId, string parentEmail, CancellationToken ct = default);

    /// <summary>PARENT action: preview an invite token (student info + the parent's owned children as link candidates).</summary>
    Task<ServiceResult<ChildLinkInvitePreviewModel>> PreviewInviteAsync(int parentUserId, string token, CancellationToken ct = default);

    /// <summary>PARENT action: accept an invite, linking to an existing owned child or creating a new one.</summary>
    Task<ServiceResult<ChildLinkModel>> AcceptInviteAsync(int parentUserId, string token, int? linkToChildProfileId, CancellationToken ct = default);

    /// <summary>EDUCATOR action: revoke a link (forward-only — does not retroactively remove shared content).</summary>
    Task<ServiceResult> RevokeLinkAsync(int educatorUserId, int studentId, int linkId, CancellationToken ct = default);

    /// <summary>EDUCATOR action: list a student's links (pending + accepted), SchoolId-bound.</summary>
    Task<ServiceResult<List<ChildLinkModel>>> GetLinksForStudentAsync(int educatorUserId, int studentId, CancellationToken ct = default);
}
