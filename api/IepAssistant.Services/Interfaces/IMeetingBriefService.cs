using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Server-composed, cached pre-meeting brief for the LEA rep (plan 7, decision 2).</summary>
public interface IMeetingBriefService
{
    /// <summary>Reads the cached brief. Fails with a "not found" message (mapped to 404) when none has been generated yet. Staff on the team, Viewer+.</summary>
    Task<ServiceResult<MeetingBriefModel>> GetAsync(int userId, int meetingId, CancellationToken ct = default);

    /// <summary>Composes (or recomposes) the brief and replaces the cached row. Staff on the team, Viewer+.</summary>
    Task<ServiceResult<MeetingBriefModel>> GenerateAsync(int userId, int meetingId, CancellationToken ct = default);
}
