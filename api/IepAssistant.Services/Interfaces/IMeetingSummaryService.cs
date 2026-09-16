using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Post-meeting, plain-language family summary (plan 6, decision 6): AI-drafted from the meeting plus the
/// latest finalized document version (or, absent one, the latest shared revision) and resolved family
/// responses, human-edited, and sent only on an explicit staff action. Requires the meeting to be
/// Held/Continued.
/// </summary>
public interface IMeetingSummaryService
{
    /// <summary>Staff: AI-drafts (or re-drafts, before any Send) the summary.</summary>
    Task<ServiceResult<FamilyMeetingSummaryModel>> DraftAsync(int userId, int meetingId, CancellationToken ct = default);

    /// <summary>Staff: human edit of the draft body.</summary>
    Task<ServiceResult<FamilyMeetingSummaryModel>> UpdateAsync(int userId, int meetingId, string body, CancellationToken ct = default);

    /// <summary>Staff: sends the summary to family participants (notification + email) and marks it Sent.</summary>
    Task<ServiceResult<FamilyMeetingSummaryModel>> SendAsync(int userId, int meetingId, CancellationToken ct = default);

    /// <summary>Staff (Viewer+) or a family participant (only once Sent).</summary>
    Task<ServiceResult<FamilyMeetingSummaryModel>> GetAsync(int userId, int meetingId, CancellationToken ct = default);
}
