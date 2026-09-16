using System.Text;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Pure RFC 5545 rendering coverage for <see cref="IcsBuilder"/> (plan 4, decision 4) — no DB.</summary>
public class IcsBuilderTests
{
    private readonly IcsBuilder _builder = new();

    private static IcsMeetingInput Meeting(int id = 1, int sequence = 0, bool cancelled = false, string title = "Annual Review",
        List<IcsAttendee>? attendees = null) => new()
    {
        MeetingId = id,
        Title = title,
        StartsAtUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
        TimeZoneId = "America/New_York",
        DurationMinutes = 60,
        Location = "Room 12",
        Sequence = sequence,
        IsCancelled = cancelled,
        OrganizerName = "Pat Organizer",
        OrganizerEmail = "organizer@example.com",
        Attendees = attendees ?? new List<IcsAttendee>()
    };

    private static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>Reverses RFC 5545 line folding (removes every "\r\n " continuation marker) so content
    /// assertions don't have to know or care where a long logical line happened to wrap.</summary>
    private static string Unfold(string folded) => folded.Replace("\r\n ", "");

    [Fact]
    public void BuildMeetingEvent_UsesStableUid()
    {
        var ics = Text(_builder.BuildMeetingEvent(Meeting(id: 42), "REQUEST"));
        Assert.Contains("UID:meeting-42@iep-advisor", ics);
    }

    [Fact]
    public void BuildMeetingEvent_UidStableAcrossRebuilds()
    {
        var first = Text(_builder.BuildMeetingEvent(Meeting(id: 7, sequence: 0), "REQUEST"));
        var second = Text(_builder.BuildMeetingEvent(Meeting(id: 7, sequence: 3), "REQUEST"));

        var uid1 = first.Split("\r\n").Single(l => l.StartsWith("UID:"));
        var uid2 = second.Split("\r\n").Single(l => l.StartsWith("UID:"));
        Assert.Equal(uid1, uid2);
    }

    [Fact]
    public void BuildMeetingEvent_SequenceFollowsMeeting()
    {
        var ics = Text(_builder.BuildMeetingEvent(Meeting(sequence: 5), "REQUEST"));
        Assert.Contains("SEQUENCE:5", ics);
    }

    [Fact]
    public void BuildMeetingEvent_TzidPresentOnDtStartAndDtEnd()
    {
        var ics = Text(_builder.BuildMeetingEvent(Meeting(), "REQUEST"));
        Assert.Contains("DTSTART;TZID=America/New_York:", ics);
        Assert.Contains("DTEND;TZID=America/New_York:", ics);
        Assert.Contains("BEGIN:VTIMEZONE", ics);
        Assert.Contains("TZID:America/New_York", ics);
    }

    [Fact]
    public void BuildMeetingEvent_UnknownTimeZone_FallsBackToAmericaNewYork()
    {
        var input = Meeting();
        input.TimeZoneId = "Not/A/RealZone";
        var ics = Text(_builder.BuildMeetingEvent(input, "REQUEST"));

        Assert.Contains("TZID:America/New_York", ics);
        Assert.Contains("DTSTART;TZID=America/New_York:", ics);
    }

    [Fact]
    public void BuildMeetingEvent_Cancelled_HasCancelledStatusAndMethod()
    {
        var ics = Text(_builder.BuildMeetingEvent(Meeting(cancelled: true), "CANCEL"));
        Assert.Contains("STATUS:CANCELLED", ics);
        Assert.Contains("METHOD:CANCEL", ics);
    }

    [Fact]
    public void BuildMeetingEvent_NotCancelled_HasConfirmedStatusAndRequestMethod()
    {
        var ics = Text(_builder.BuildMeetingEvent(Meeting(cancelled: false), "REQUEST"));
        Assert.Contains("STATUS:CONFIRMED", ics);
        Assert.Contains("METHOD:REQUEST", ics);
    }

    [Theory]
    [InlineData(InviteStatus.Accepted, "ACCEPTED")]
    [InlineData(InviteStatus.Declined, "DECLINED")]
    [InlineData(InviteStatus.Tentative, "TENTATIVE")]
    [InlineData(InviteStatus.Pending, "NEEDS-ACTION")]
    public void BuildMeetingEvent_AttendeePartStat_MapsFromInviteStatus(InviteStatus status, string expectedPartStat)
    {
        var attendees = new List<IcsAttendee> { new() { Name = "Alex Attendee", Email = "alex@example.com", IsRequired = true, InviteStatus = status } };
        var ics = Unfold(Text(_builder.BuildMeetingEvent(Meeting(attendees: attendees), "REQUEST")));

        Assert.Contains($"PARTSTAT={expectedPartStat}", ics);
        Assert.Contains("mailto:alex@example.com", ics);
    }

    [Fact]
    public void BuildMeetingEvent_RequiredVsOptional_MapsRole()
    {
        var attendees = new List<IcsAttendee>
        {
            new() { Name = "Req", Email = "req@example.com", IsRequired = true, InviteStatus = InviteStatus.Pending },
            new() { Name = "Opt", Email = "opt@example.com", IsRequired = false, InviteStatus = InviteStatus.Pending }
        };
        var ics = Unfold(Text(_builder.BuildMeetingEvent(Meeting(attendees: attendees), "REQUEST")));

        Assert.Contains("ROLE=REQ-PARTICIPANT", ics);
        Assert.Contains("ROLE=OPT-PARTICIPANT", ics);
    }

    [Fact]
    public void BuildMeetingEvent_UsesCrlfLineEndings()
    {
        var bytes = _builder.BuildMeetingEvent(Meeting(), "REQUEST");
        var text = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\r\n", text);
        // No bare LF (a LF not preceded by CR) — every line ending is a full CRLF pair.
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                Assert.True(i > 0 && text[i - 1] == '\r', $"Found bare LF at index {i}");
        }
    }

    [Fact]
    public void BuildMeetingEvent_FoldsLongLines_NoPhysicalLineExceeds75Octets()
    {
        var input = Meeting(title: new string('A', 200)); // forces SUMMARY to fold
        var bytes = _builder.BuildMeetingEvent(input, "REQUEST");
        var text = Encoding.UTF8.GetString(bytes);

        foreach (var physicalLine in text.Split("\r\n"))
        {
            if (physicalLine.Length == 0)
                continue;
            var octets = Encoding.UTF8.GetByteCount(physicalLine);
            Assert.True(octets <= 75, $"Line exceeded 75 octets ({octets}): {physicalLine}");
        }
        // Continuation lines begin with a single space.
        var lines = text.Split("\r\n");
        var summaryLineIndex = Array.FindIndex(lines, l => l.StartsWith("SUMMARY:"));
        Assert.True(summaryLineIndex >= 0);
        Assert.StartsWith(" ", lines[summaryLineIndex + 1]);
    }

    [Fact]
    public void BuildFeed_ContainsAllDayObligationAndTimedMeetingEvents()
    {
        var events = new List<IcsFeedEvent>
        {
            new() { Uid = "meeting-1@iep-advisor", Summary = "Annual Review — Sam", StartsAtUtc = DateTime.UtcNow.AddDays(1), DurationMinutes = 60, TimeZoneId = "America/New_York", Sequence = 0 },
            new() { Uid = "obligation-AnnualReview-1@iep-advisor", Summary = "AnnualReview due — Sam", AllDayDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)) }
        };

        var ics = Text(_builder.BuildFeed("Test Calendar", events));

        Assert.Contains("METHOD:PUBLISH", ics);
        Assert.Contains("UID:meeting-1@iep-advisor", ics);
        Assert.Contains("UID:obligation-AnnualReview-1@iep-advisor", ics);
        Assert.Contains("VALUE=DATE:", ics);
        Assert.Contains("DTSTART;TZID=America/New_York:", ics);
    }
}
