using IepAssistant.Api.DTOs.Meetings;
using IepAssistant.Api.DTOs.Obligations;

namespace IepAssistant.Api.DTOs.Calendar;

public class CalendarItemDto
{
    /// <summary>"Meeting" or "Obligation".</summary>
    public string Kind { get; set; } = string.Empty;

    public DateTime Date { get; set; }
    public MeetingDto? Meeting { get; set; }
    public ObligationDto? Obligation { get; set; }
}

public class CalendarFeedDto
{
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
