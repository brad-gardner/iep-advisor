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

        lines.Add($"ORGANIZER;CN={QuoteParam(meeting.OrganizerName)}:mailto:{SanitizeAddress(meeting.OrganizerEmail)}");

        foreach (var attendee in meeting.Attendees)
        {
            if (string.IsNullOrWhiteSpace(attendee.Email))
                continue;
            var role = attendee.IsRequired ? "REQ-PARTICIPANT" : "OPT-PARTICIPANT";
            var partstat = ToPartStat(attendee.InviteStatus);
            var cn = string.IsNullOrWhiteSpace(attendee.Name) ? attendee.Email : attendee.Name;
            lines.Add($"ATTENDEE;CN={QuoteParam(cn!)};ROLE={role};PARTSTAT={partstat}:mailto:{SanitizeAddress(attendee.Email)}");
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
        if (!tz.SupportsDaylightSavingTime)
            return BuildStandardOnlyVTimezone(tz, tzId);

        var rules = tz.GetAdjustmentRules();

        // Some zones carry adjustment rules for DST they no longer observe (e.g. America/Phoenix's last
        // rule ended in 1967, but SupportsDaylightSavingTime is still true because it observed DST
        // historically). Only treat DST as currently active when some rule actually covers a year near
        // "now" — otherwise the stale historical rule would produce a bogus DAYLIGHT/STANDARD pair.
        var currentYear = DateTime.UtcNow.Year;
        var activeRule = rules.FirstOrDefault(r => r.DateStart.Year <= currentYear && r.DateEnd.Year >= currentYear - 1);
        if (activeRule == null)
            return BuildStandardOnlyVTimezone(tz, tzId);

        // Anchor the recurring rule far enough back that a feed spanning "past 30 days" never needs a
        // transition date earlier than DTSTART (RRULE only recurs forward from DTSTART).
        var anchorYear = currentYear - 1;
        var rule = rules.FirstOrDefault(r => r.DateStart.Year <= anchorYear && r.DateEnd.Year >= anchorYear) ?? activeRule;

        var standardOffset = tz.BaseUtcOffset;
        var daylightOffset = tz.BaseUtcOffset + rule.DaylightDelta;

        var daylightStart = ComputeTransitionDate(anchorYear, rule.DaylightTransitionStart);
        var daylightEnd = ComputeTransitionDate(anchorYear, rule.DaylightTransitionEnd);

        var lines = new List<string> { "BEGIN:VTIMEZONE", $"TZID:{tzId}" };

        lines.Add("BEGIN:DAYLIGHT");
        lines.Add($"DTSTART:{daylightStart:yyyyMMdd\\THHmmss}");
        lines.Add($"TZOFFSETFROM:{FormatOffset(standardOffset)}");
        lines.Add($"TZOFFSETTO:{FormatOffset(daylightOffset)}");
        lines.Add($"RRULE:FREQ=YEARLY;BYMONTH={daylightStart.Month};BYDAY={ToByDay(daylightStart)}");
        lines.Add("TZNAME:DST");
        lines.Add("END:DAYLIGHT");

        lines.Add("BEGIN:STANDARD");
        lines.Add($"DTSTART:{daylightEnd:yyyyMMdd\\THHmmss}");
        lines.Add($"TZOFFSETFROM:{FormatOffset(daylightOffset)}");
        lines.Add($"TZOFFSETTO:{FormatOffset(standardOffset)}");
        lines.Add($"RRULE:FREQ=YEARLY;BYMONTH={daylightEnd.Month};BYDAY={ToByDay(daylightEnd)}");
        lines.Add("TZNAME:STD");
        lines.Add("END:STANDARD");

        lines.Add("END:VTIMEZONE");
        return lines;
    }

    /// <summary>The no-DST VTIMEZONE shape, shared by the "zone never observes DST" and "zone's DST rules
    /// are historical/stale" cases so the two branches can't drift out of sync.</summary>
    private static List<string> BuildStandardOnlyVTimezone(TimeZoneInfo tz, string tzId) => new()
    {
        "BEGIN:VTIMEZONE",
        $"TZID:{tzId}",
        "BEGIN:STANDARD",
        "DTSTART:19700101T000000",
        $"TZOFFSETFROM:{FormatOffset(tz.BaseUtcOffset)}",
        $"TZOFFSETTO:{FormatOffset(tz.BaseUtcOffset)}",
        "TZNAME:STD",
        "END:STANDARD",
        "END:VTIMEZONE"
    };

    /// <summary>Resolves a .NET <see cref="TimeZoneInfo.TransitionTime"/> to an actual calendar date/time
    /// in <paramref name="year"/> (week-of-month rules are "the Nth <c>DayOfWeek</c> in <c>Month</c>",
    /// with week 5 meaning "the last one"). The time-of-day is rounded to the nearest second: tzdata-derived
    /// rules on non-Windows runtimes can carry a transition documented as "2:00 AM" as 01:59:59.999, which
    /// would otherwise render as a wrong-looking DTSTART.</summary>
    private static DateTime ComputeTransitionDate(int year, TimeZoneInfo.TransitionTime t)
    {
        var timeOfDay = RoundToNearestSecond(t.TimeOfDay.TimeOfDay);
        if (t.IsFixedDateRule)
            return new DateTime(year, t.Month, t.Day) + timeOfDay;

        var firstOfMonth = new DateTime(year, t.Month, 1);
        var daysToFirstMatch = ((int)t.DayOfWeek - (int)firstOfMonth.DayOfWeek + 7) % 7;
        var candidate = firstOfMonth.AddDays(daysToFirstMatch + (t.Week - 1) * 7);
        if (candidate.Month != t.Month)
            candidate = candidate.AddDays(-7); // week 5 overflowed past month end -> back up to the last occurrence
        return candidate.Date + timeOfDay;
    }

    private static TimeSpan RoundToNearestSecond(TimeSpan value)
        => TimeSpan.FromSeconds(Math.Round(value.TotalSeconds, MidpointRounding.AwayFromZero));

    /// <summary>Derives RRULE's BYDAY (ordinal + weekday, e.g. "2SU" or the last-occurrence "-1SU") from the
    /// already-resolved transition date rather than the raw <see cref="TimeZoneInfo.TransitionTime"/>: a
    /// fixed-date rule (what tzdata-derived rules on non-Windows runtimes report even for a genuinely
    /// floating "Nth weekday" rule) carries a meaningless default Week/DayOfWeek, so deriving from the
    /// concrete resolved date is the only form that is correct for both fixed and floating rules alike.</summary>
    private static string ToByDay(DateTime resolvedDate)
    {
        var daysInMonth = DateTime.DaysInMonth(resolvedDate.Year, resolvedDate.Month);
        var isLastOccurrence = resolvedDate.Day + 7 > daysInMonth;
        var ordinal = isLastOccurrence ? -1 : (resolvedDate.Day - 1) / 7 + 1;
        var day = resolvedDate.DayOfWeek switch
        {
            DayOfWeek.Sunday => "SU",
            DayOfWeek.Monday => "MO",
            DayOfWeek.Tuesday => "TU",
            DayOfWeek.Wednesday => "WE",
            DayOfWeek.Thursday => "TH",
            DayOfWeek.Friday => "FR",
            _ => "SA"
        };
        return $"{ordinal}{day}";
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
        .Replace("\r", "\\n")
        .Replace("\n", "\\n");

    /// <summary>Quotes a CN/param value per RFC 5545 §3.2 when it contains a colon, semicolon or comma.
    /// Also strips CR/LF first: an unvalidated external participant name could otherwise inject new
    /// content lines into the rendered .ics (defense-in-depth alongside the service-level input
    /// validation that rejects control characters outright).</summary>
    private static string QuoteParam(string value)
    {
        var cleaned = SanitizeAddress(value).Replace("\"", "'");
        return cleaned.IndexOfAny(new[] { ':', ';', ',' }) >= 0 ? $"\"{cleaned}\"" : cleaned;
    }

    /// <summary>Strips CR/LF from a value bound for an unquoted ICS position (a mailto: address, or ahead
    /// of <see cref="QuoteParam"/>) so it cannot inject new content lines.</summary>
    private static string SanitizeAddress(string value) => value.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

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
        if (Encoding.UTF8.GetByteCount(line) <= maxOctets)
            return line;

        var bytes = Encoding.UTF8.GetBytes(line);
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
