using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Structured decisions captured live during a meeting, and their surfacing as proposed edits
/// on a draft (plan 7, decision 3). Never auto-applied — a human reviews and applies each edit themselves.</summary>
public interface IMeetingDecisionService
{
    /// <summary>Staff on the team, Viewer+.</summary>
    Task<ServiceResult<List<MeetingDecisionModel>>> GetForMeetingAsync(int userId, int meetingId, CancellationToken ct = default);

    /// <summary>Staff on the team, Collaborator+. The meeting must be Held or Continued.</summary>
    Task<ServiceResult<MeetingDecisionModel>> CreateAsync(int userId, int meetingId, CreateMeetingDecisionModel model, CancellationToken ct = default);

    Task<ServiceResult<MeetingDecisionModel>> UpdateAsync(int userId, int decisionId, UpdateMeetingDecisionModel model, CancellationToken ct = default);

    Task<ServiceResult> DeleteAsync(int userId, int decisionId, CancellationToken ct = default);

    /// <summary>Candidate edits for a Draft instance's editor: decisions from meetings linked to the
    /// instance directly, or from any meeting for the same student within the last 60 days. Staff, Viewer+.</summary>
    Task<ServiceResult<List<ProposedEditModel>>> GetProposedEditsForInstanceAsync(int userId, int instanceId, CancellationToken ct = default);

    /// <summary>Marks a decision as applied — the human already made the corresponding edit themselves. Collaborator+.</summary>
    Task<ServiceResult<ProposedEditModel>> MarkAppliedAsync(int userId, int decisionId, CancellationToken ct = default);
}
