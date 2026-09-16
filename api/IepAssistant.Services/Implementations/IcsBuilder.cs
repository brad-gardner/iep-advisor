using System.Text;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Pure RFC 5545 rendering (see <see cref="IIcsBuilder"/>). No DB/network access — every input arrives
/// already resolved. Falls back to a fixed America/New_York VTIMEZONE block (and uses that zone for the
/// actual UTC→local conversion) whenever <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> fails for the
/// requested id, per the plan-4 contract.
/// </summary>
public class IcsBuilder : IIcsBuilder
{
    private const string ProdId = "-//IEP Advisor//Meetings//EN";
    internal const string FallbackTimeZoneId = "America/New_York";

    public byte[] BuildMeetingEvent(IcsMeetingInput meeting, string method)
    {
        var tz = ResolveTimeZone(meeting.TimeZoneId, out var effectiveTzId);
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            $"PRODID:{ProdId}",
            "VERSION:2.0",
            $"METHOD:{method}",
            "CALSCALE:GREGORIAN"
        };
        lines.AddRange(BuildVTimeZoneLines(tz, effectiveTzId));
        lines.AddRange(BuildEventLines(meeting, tz, effectiveTzId, method));
        lines.Add("END:VCALENDAR");

        return RenderAsBytes(lines);
    }

    public byte[] BuildFeed(string calendarName, IEnumerable<IcsFeedEvent> events)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            $"PRODID:{ProdId}",
            "VERSION:2.0",
            "METHOD:PUBLISH",
            "CALSCALE:GREGORIAN",
            $"X-WR-CALNAME:{EscapeText(calendarName)}"
        };

        // A feed can mix events across several time zones (rare, but possible for a multi-district
        // provider); emit one VTIMEZONE block per distinct zone actually used, in first-seen order.
        var seenZones = new List<string>();
        foreach (var evt in events)
        {
            if (evt.TimeZoneId == null || seenZones.Contains(evt.TimeZoneId))
                continue;
            seenZones.Add(evt.TimeZoneId);
        }
        foreach (var zoneId in seenZones)
        {
            var tz = ResolveTimeZone(zoneId, out var effectiveTzId);
            lines.AddRange(BuildVTimeZoneLines(tz, effectiveTzId));
        }

        foreach (var evt in events)
        {
            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:{evt.Uid}");
            lines.Add($"DTSTAMP:{FormatUtc(DateTime.UtcNow)}");
            lines.Add($"SEQUENCE:{evt.Sequence}");

            if (evt.AllDayDate.HasValue)
            {
                var start = evt.AllDayDate.Value;
                var end = start.AddDays(1);
                lines.Add($"DTSTART;VALUE=DATE:{start:yyyyMMdd}");
                lines.Add($"DTEND;VALUE=DATE:{end:yyyyMMdd}");
            }
            else if (evt.StartsAtUtc.HasValue && evt.TimeZoneId != null)
            {
                var tz = ResolveTimeZone(evt.TimeZoneId, out var effectiveTzId);
                var startLocal = TimeZoneInfo.ConvertTimeFromUtc(evt.StartsAtUtc.Value, tz);
                var endLocal = startLocal.AddMinutes(evt.DurationMinutes);
                lines.Add($"DTSTART;TZID={effectiveTzId}:{FormatLocal(startLocal)}");
                lines.Add($"DTEND;TZID={effectiveTzId}:{FormatLocal(endLocal)}");
            }

            lines.Add($"SUMMARY:{EscapeText(evt.Summary)}");
            if (!string.IsNullOrWhiteSpace(evt.Location))
                lines.Add($"LOCATION:{EscapeText(evt.Location)}");
            if (!string.IsNullOrWhiteSpace(evt.Description))
                lines.Add($"DESCRIPTION:{EscapeText(evt.Description)}");
            lines.Add("END:VEVENT");
        }

        lines.Add("END:VCALENDAR");
        return RenderAsBytes(lines);
    }

    // ----------------------------------------------------------------- VEVENT

    private static List<string> BuildEventLines(IcsMeetingInput meeting, TimeZoneInfo tz, string effectiveTzId, string method)
    {
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(meeting.StartsAtUtc, tz);
        var endLocal = startLocal.AddMinutes(meeting.DurationMinutes);

        var descriptionParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(meeting.VideoUrl))
            descriptionParts.Add($"Video: {meeting.VideoUrl}");
        if (!string.IsNullOrWhiteSpace(meeting.Notes))
            descriptionParts.Add(meeting.Notes!);

        var lines = new List<string>
        {
            "BEGIN:VEVENT",
            $"UID:meeting-{meeting.MeetingId}@iep-advisor",
            $"DTSTAMP:{FormatUtc(DateTime.UtcNow)}",
            $"DTSTART;TZID={effectiveTzId}:{FormatLocal(startLocal)}",
            $"DTEND;TZID={effectiveTzId}:{FormatLocal(endLocal)}",
            $"SEQUENCE:{meeting.Sequence}",
            $"STATUS:{(meeting.IsCancelled ? "CANCELLED" : "CONFIRMED")}",
            $"SUMMARY:{EscapeText(meeting.Title)}"
        };

        if (!string.IsNullOrWhiteSpace(meeting.Location))
            lines.Add($"LOCATION:{EscapeText(meeting.Location)}");
        if (descriptionParts.Count > 0)
            lines.Add($"DESCRIPTION:{EscapeText(string.Join("\\n", descriptionParts))}");

        lines.Add($"ORGANIZER;CN={QuoteParam(meeting.OrganizerName)}:mailto:{meeting.OrganizerEmail}");

        foreach (var attendee in meeting.Attendees)
        {
            if (string.IsNullOrWhiteSpace(attendee.Email))
                continue;
            var role = attendee.IsRequired ? "REQ-PARTICIPANT" : "OPT-PARTICIPANT";
            var partstat = ToPartStat(attendee.InviteStatus);
            var cn = string.IsNullOrWhiteSpace(attendee.Name) ? attendee.Email : attendee.Name;
            lines.Add($"ATTENDEE;CN={QuoteParam(cn!)};ROLE={role};PARTSTAT={partstat}:mailto:{attendee.Email}");
        }

        lines.Add("END:VEVENT");
        return lines;
    }

    private static string ToPartStat(InviteStatus status) => status switch
    {
        InviteStatus.Accepted => "ACCEPTED",
        InviteStatus.Declined => "DECLINED",
        InviteStatus.Tentative => "TENTATIVE",
        _ => "NEEDS-ACTION"
    };

    // ----------------------------------------------------------------- VTIMEZONE

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId, out string effectiveTzId)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            effectiveTzId = timeZoneId;
            return tz;
        }
        catch
        {
            effectiveTzId = FallbackTimeZoneId;
            return TimeZoneInfo.FindSystemTimeZoneById(FallbackTimeZoneId);
        }
    }

    private static List<string> BuildVTimeZoneLines(TimeZoneInfo tz, string tzId)
    {
        var lines = new List<string> { "BEGIN:VTIMEZONE", $"TZID:{tzId}" };

        if (!tz.SupportsDaylightSavingTime)
        {
            lines.Add("BEGIN:STANDARD");
            lines.Add("DTSTART:19700101T000000");
            lines.Add($"TZOFFSETFROM:{FormatOffset(tz.BaseUtcOffset)}");
            lines.Add($"TZOFFSETTO:{FormatOffset(tz.BaseUtcOffset)}");
            lines.Add("TZNAME:STD");
            lines.Add("END:STANDARD");
            lines.Add("END:VTIMEZONE");
            return lines;
        }

        // Anchor the recurring rule far enough back that a feed spanning "past 30 days" never needs a
        // transition date earlier than DTSTART (RRULE only recurs forward from DTSTART).
        var anchorYear = DateTime.UtcNow.Year - 1;
        var rule = tz.GetAdjustmentRules()
            .FirstOrDefault(r => r.DateStart.Year <= anchorYear && r.DateEnd.Year >= anchorYear)
            ?? tz.GetAdjustmentRules().LastOrDefault();

        if (rule == null)
        {
            // SupportsDaylightSavingTime but no adjustment rules resolved for any year — treat as standard-only.
            lines.Add("BEGIN:STANDARD");
            lines.Add("DTSTART:19700101T000000");
            lines.Add($"TZOFFSETFROM:{FormatOffset(tz.BaseUtcOffset)}");
            lines.Add($"TZOFFSETTO:{FormatOffset(tz.BaseUtcOffset)}");
            lines.Add("TZNAME:STD");
            lines.Add("END:STANDARD");
            lines.Add("END:VTIMEZONE");
            return lines;
        }

        var standardOffset = tz.BaseUtcOffset;
        var daylightOffset = tz.BaseUtcOffset + rule.DaylightDelta;

        var daylightStart = ComputeTransitionDate(anchorYear, rule.DaylightTransitionStart);
        var daylightEnd = ComputeTransitionDate(anchorYear, rule.DaylightTransitionEnd);

        lines.Add("BEGIN:DAYLIGHT");
        lines.Add($"DTSTART:{daylightStart:yyyyMMdd\\THHmmss}");
        lines.Add($"TZOFFSETFROM:{FormatOffset(standardOffset)}");
        lines.Add($"TZOFFSETTO:{FormatOffset(daylightOffset)}");
        lines.Add($"RRULE:FREQ=YEARLY;BYMONTH={rule.DaylightTransitionStart.Month};BYDAY={ToByDay(rule.DaylightTransitionStart)}");
        lines.Add("TZNAME:DST");
        lines.Add("END:DAYLIGHT");

        lines.Add("BEGIN:STANDARD");
        lines.Add($"DTSTART:{daylightEnd:yyyyMMdd\\THHmmss}");
        lines.Add($"TZOFFSETFROM:{FormatOffset(daylightOffset)}");
        lines.Add($"TZOFFSETTO:{FormatOffset(standardOffset)}");
        lines.Add($"RRULE:FREQ=YEARLY;BYMONTH={rule.DaylightTransitionEnd.Month};BYDAY={ToByDay(rule.DaylightTransitionEnd)}");
        lines.Add("TZNAME:STD");
        lines.Add("END:STANDARD");

        lines.Add("END:VTIMEZONE");
        return lines;
    }

    /// <summary>Resolves a .NET <see cref="TimeZoneInfo.TransitionTime"/> to an actual calendar date/time
    /// in <paramref name="year"/> (week-of-month rules are "the Nth <c>DayOfWeek</c> in <c>Month</c>",
    /// with week 5 meaning "the last one").</summary>
    private static DateTime ComputeTransitionDate(int year, TimeZoneInfo.TransitionTime t)
    {
        if (t.IsFixedDateRule)
            return new DateTime(year, t.Month, t.Day) + t.TimeOfDay.TimeOfDay;

        var firstOfMonth = new DateTime(year, t.Month, 1);
        var daysToFirstMatch = ((int)t.DayOfWeek - (int)firstOfMonth.DayOfWeek + 7) % 7;
        var candidate = firstOfMonth.AddDays(daysToFirstMatch + (t.Week - 1) * 7);
        if (candidate.Month != t.Month)
            candidate = candidate.AddDays(-7); // week 5 overflowed past month end -> back up to the last occurrence
        return candidate.Date + t.TimeOfDay.TimeOfDay;
    }

    private static string ToByDay(TimeZoneInfo.TransitionTime t)
    {
        var week = t.Week >= 5 ? -1 : t.Week;
        var day = t.DayOfWeek switch
        {
            DayOfWeek.Sunday => "SU",
            DayOfWeek.Monday => "MO",
            DayOfWeek.Tuesday => "TU",
            DayOfWeek.Wednesday => "WE",
            DayOfWeek.Thursday => "TH",
            DayOfWeek.Friday => "FR",
            _ => "SA"
        };
        return $"{week}{day}";
    }

    private static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return $"{sign}{abs.Hours:D2}{abs.Minutes:D2}";
    }

    // ----------------------------------------------------------------- formatting / escaping / folding

    private static string FormatUtc(DateTime utc) => utc.ToString("yyyyMMdd\\THHmmss\\Z");
    private static string FormatLocal(DateTime local) => local.ToString("yyyyMMdd\\THHmmss");

    private static string EscapeText(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");

    /// <summary>Quotes a CN/param value per RFC 5545 §3.2 when it contains a colon, semicolon or comma.</summary>
    private static string QuoteParam(string value)
    {
        var cleaned = value.Replace("\"", "'");
        return cleaned.IndexOfAny(new[] { ':', ';', ',' }) >= 0 ? $"\"{cleaned}\"" : cleaned;
    }

    private static byte[] RenderAsBytes(IEnumerable<string> logicalLines)
    {
        var sb = new StringBuilder();
        foreach (var line in logicalLines)
        {
            sb.Append(FoldLine(line));
            sb.Append("\r\n");
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>RFC 5545 §3.1 line folding: no physical line exceeds 75 octets; continuation lines begin
    /// with a single space (itself counted in that line's 75-octet budget). Splits only on UTF-8 character
    /// boundaries.</summary>
    private static string FoldLine(string line)
    {
        const int maxOctets = 75;
        var bytes = Encoding.UTF8.GetBytes(line);
        if (bytes.Length <= maxOctets)
            return line;

        var result = new StringBuilder();
        var pos = 0;
        var first = true;
        while (pos < bytes.Length)
        {
            var budget = first ? maxOctets : maxOctets - 1;
            var take = Math.Min(budget, bytes.Length - pos);
            while (take > 0 && pos + take < bytes.Length && (bytes[pos + take] & 0xC0) == 0x80)
                take--;

            if (!first)
                result.Append("\r\n ");
            result.Append(Encoding.UTF8.GetString(bytes, pos, take));
            pos += take;
            first = false;
        }
        return result.ToString();
    }
}
