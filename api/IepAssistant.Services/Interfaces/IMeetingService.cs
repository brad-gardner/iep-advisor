using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Meeting scheduling/lifecycle (plan 4, decision 1). Authorization: create/list-for-student require
/// Viewer+/Collaborator+ via <see cref="IOrgAccessService"/> on the student; single-meeting reads allow a
/// participant OR Viewer+ on the student; mutations (update/cancel/status/attendance) require the creator,
/// the student's lead case manager, or an admin in scope; RSVP is restricted to the caller's own
/// participant row (by user id, or by <see cref="MeetingParticipant.RsvpToken"/> for the anonymous flow).
/// </summary>
public interface IMeetingService
{
    Task<ServiceResult<MeetingModel>> CreateAsync(int userId, int studentId, CreateMeetingModel model, CancellationToken ct = default);
    Task<ServiceResult<List<MeetingModel>>> GetForStudentAsync(int userId, int studentId, CancellationToken ct = default);

    /// <summary>The participants a new meeting would get by default (team, accepted family, student account),
    /// for the schedule form. Collaborator+ on the student.</summary>
    Task<ServiceResult<List<DefaultParticipantModel>>> GetDefaultParticipantsAsync(int userId, int studentId, CancellationToken ct = default);
    Task<ServiceResult<MeetingModel>> GetAsync(int userId, int meetingId, CancellationToken ct = default);
    Task<ServiceResult<MeetingModel>> UpdateAsync(int userId, int meetingId, UpdateMeetingModel model, CancellationToken ct = default);
    Task<ServiceResult<MeetingModel>> CancelAsync(int userId, int meetingId, string? reason, CancellationToken ct = default);
    Task<ServiceResult<MeetingModel>> SetStatusAsync(int userId, int meetingId, MeetingStatus status, CancellationToken ct = default);
    Task<ServiceResult<MeetingModel>> RecordAttendanceAsync(int userId, int meetingId, List<AttendanceItemModel> items, CancellationToken ct = default);
    Task<ServiceResult<MeetingModel>> RsvpAsync(int userId, int meetingId, InviteStatus status, CancellationToken ct = default);
    Task<ServiceResult<List<MeetingModel>>> ListMineAsync(int userId, DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>Parent: meetings for a child's linked school student(s) (Viewer+ on the child).</summary>
    Task<ServiceResult<List<MeetingModel>>> ListForChildAsync(int parentUserId, int childProfileId, CancellationToken ct = default);

    /// <summary>Anonymous email-link RSVP: preview by token (no auth). Fails once the meeting has started.</summary>
    Task<ServiceResult<MeetingRsvpPreviewModel>> GetByRsvpTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Anonymous email-link RSVP: submit by token (no auth). Fails once the meeting has started.</summary>
    Task<ServiceResult<MeetingRsvpPreviewModel>> RsvpByTokenAsync(string token, InviteStatus status, CancellationToken ct = default);
}
