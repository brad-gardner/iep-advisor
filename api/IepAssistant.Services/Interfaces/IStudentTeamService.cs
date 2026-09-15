using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// IEP team membership (plan 3, decision 4). Reads need Viewer access on the student; mutations need an
/// admin in scope OR the student's current lead case manager. Adding a member upserts the permission row
/// (<c>SchoolStudentAccess</c>) with a role-derived default unless overridden; removing deactivates both.
/// Exactly one active member is the lead; the lead cannot be removed while other members remain.
/// </summary>
public interface IStudentTeamService
{
    Task<ServiceResult<List<StudentTeamMemberModel>>> GetTeamAsync(int userId, int studentId, CancellationToken ct = default);
    Task<ServiceResult<StudentTeamMemberModel>> AddMemberAsync(int userId, int studentId, AddTeamMemberModel model, CancellationToken ct = default);
    Task<ServiceResult<StudentTeamMemberModel>> UpdateMemberAsync(int userId, int studentId, int memberId, UpdateTeamMemberModel model, CancellationToken ct = default);
    Task<ServiceResult<StudentTeamMemberModel>> SetLeadAsync(int userId, int studentId, int memberId, CancellationToken ct = default);
    Task<ServiceResult> RemoveMemberAsync(int userId, int studentId, int memberId, CancellationToken ct = default);
}
