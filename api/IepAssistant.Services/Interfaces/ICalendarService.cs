using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Staff/parent calendar aggregation + ICS export (plan 4, decision 4).</summary>
public interface ICalendarService
{
    /// <summary>My meetings (as a participant) + my caseload obligations (as lead), merged and date-sorted.</summary>
    Task<ServiceResult<List<CalendarItemModel>>> GetMineAsync(int userId, DateTime? from, DateTime? to, CancellationToken ct = default);

    /// <summary>Returns the caller's feed URL, creating a <see cref="Domain.Entities.User.CalendarFeedToken"/> if missing.</summary>
    Task<ServiceResult<CalendarFeedModel>> GetOrCreateFeedAsync(int userId, CancellationToken ct = default);

    /// <summary>Always issues a fresh token, revoking the previous one.</summary>
    Task<ServiceResult<CalendarFeedModel>> RegenerateFeedAsync(int userId, CancellationToken ct = default);

    /// <summary>Anonymous, token-addressed feed: Scheduled/Held meetings (next 365d, past 30d) + obligations
    /// as all-day events, for whichever user owns <paramref name="token"/>.</summary>
    Task<ServiceResult<byte[]>> GetFeedByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Single-meeting .ics (participant or Viewer+ on the student).</summary>
    Task<ServiceResult<byte[]>> GetMeetingIcsAsync(int userId, int meetingId, CancellationToken ct = default);
}
