using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 4 review fix: <see cref="IcsMeetingInputMapper.Map"/>'s
/// organizer-name fallback (no <see cref="Meeting.CreatedByUser"/> on file) now resolves from
/// Emails.resx under the ambient culture instead of the hardcoded English literal "Organizer".
/// </summary>
public class IcsMeetingInputMapperTests
{
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<Emails> Localizer = TestSupport.TestLocalizers.Emails();

    private static Meeting MinimalMeeting(User? createdByUser = null) => new()
    {
        Id = 7,
        Title = "Annual Review",
        StartsAtUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
        TimeZoneId = "America/New_York",
        DurationMinutes = 60,
        Sequence = 0,
        Status = MeetingStatus.Scheduled,
        CreatedByUser = createdByUser,
        Participants = new List<MeetingParticipant>()
    };

    [Fact]
    public void Map_CreatedByUserPresent_UsesTheirName()
    {
        var meeting = MinimalMeeting(new User { FirstName = "Pat", LastName = "Organizer", Email = "pat@example.com" });

        var input = IcsMeetingInputMapper.Map(meeting, Localizer);

        Assert.Equal("Pat Organizer", input.OrganizerName);
        Assert.Equal("pat@example.com", input.OrganizerEmail);
    }

    [Fact]
    public void Map_NoCreatedByUser_English_FallsBackToLocalizedOrganizer()
    {
        using var _ = CultureScope.For("en");
        var meeting = MinimalMeeting(createdByUser: null);

        var input = IcsMeetingInputMapper.Map(meeting, Localizer);

        Assert.Equal("Organizer", input.OrganizerName);
        Assert.Equal("no-reply@iep-advisor.com", input.OrganizerEmail);
    }

    [Fact]
    public void Map_NoCreatedByUser_Spanish_FallsBackToLocalizedOrganizer()
    {
        using var _ = CultureScope.For("es");
        var meeting = MinimalMeeting(createdByUser: null);

        var input = IcsMeetingInputMapper.Map(meeting, Localizer);

        Assert.Equal("Organizador", input.OrganizerName);
    }
}
