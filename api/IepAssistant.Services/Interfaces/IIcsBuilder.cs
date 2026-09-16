using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Pure RFC 5545 iCalendar rendering (plan 4, decision 4) — no I/O, safe to call from services, workers,
/// or tests directly. CRLF line endings, 75-octet folding, and a VTIMEZONE block are always emitted.
/// </summary>
public interface IIcsBuilder
{
    /// <summary>One VEVENT for a single meeting (used for the email attachment and the per-meeting .ics
    /// download). <paramref name="method"/> is "REQUEST" normally, "CANCEL" when the meeting is cancelled.</summary>
    byte[] BuildMeetingEvent(IcsMeetingInput meeting, string method);

    /// <summary>A multi-VEVENT calendar for the per-user subscription feed (METHOD:PUBLISH, no attendees).</summary>
    byte[] BuildFeed(string calendarName, IEnumerable<IcsFeedEvent> events);
}
