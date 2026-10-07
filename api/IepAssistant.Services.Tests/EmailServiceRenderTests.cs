using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Covers <see cref="EmailService"/>'s internal HTML renderers (todos/049): a meeting Title/Location,
/// notification title/body, or digest StudentName/meeting Title containing markup must render as inert
/// text in the email HTML body, not execute in the recipient's mail client. Exercised directly against the
/// internal static Render*Html methods (via InternalsVisibleTo) rather than through a live ACS send.
/// Phase 4 added a <c>language</c>/<c>localizer</c> parameter to each — these tests always pass English
/// ("en" + the real resx-backed localizer) since they're only asserting encoding, not translation; see
/// <c>EmailServicePhase4LanguageTests</c> for the Spanish-rendering coverage.
/// </summary>
public class EmailServiceRenderTests
{
    private const string Payload = "<a href=\"http://attacker.example\">click</a>";
    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<Emails> Localizer = TestSupport.TestLocalizers.Emails();

    private static MeetingEmailModel MeetingModel(string title = "Annual Review", string? location = null) => new()
    {
        StudentFirstName = "Sam",
        Title = title,
        StartsAtUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
        TimeZoneId = "America/New_York",
        DurationMinutes = 60,
        Location = location,
        OrganizerName = "Pat Organizer",
        RsvpAcceptUrl = "https://app.example.com/meetings/rsvp?token=abc&status=Accepted",
        RsvpDeclineUrl = "https://app.example.com/meetings/rsvp?token=abc&status=Declined",
        DetailUrl = "https://app.example.com/meetings/1"
    };

    [Fact]
    public void RenderMeetingHtml_TitleWithMarkup_IsEncoded()
    {
        var html = EmailService.RenderMeetingHtml("A meeting has been scheduled", MeetingModel(title: Payload), "en", Localizer);

        Assert.Contains("&lt;a", html);
        Assert.DoesNotContain(Payload, html);
    }

    [Fact]
    public void RenderMeetingHtml_LocationWithMarkup_IsEncoded()
    {
        var html = EmailService.RenderMeetingHtml("A meeting has been scheduled", MeetingModel(location: Payload), "en", Localizer);

        Assert.Contains("&lt;a", html);
        Assert.DoesNotContain(Payload, html);
    }

    [Fact]
    public void RenderMeetingHtml_StudentFirstNameAndOrganizerName_AreEncoded()
    {
        var model = MeetingModel();
        model.StudentFirstName = Payload;
        model.OrganizerName = Payload;

        var html = EmailService.RenderMeetingHtml("A meeting has been scheduled", model, "en", Localizer);

        Assert.DoesNotContain(Payload, html);
        // Two occurrences: student name and organizer name.
        Assert.Equal(2, html.Split("&lt;a href=").Length - 1);
    }

    [Fact]
    public void RenderMeetingHtml_Spanish_UsesSpanishMonthNameAndConnectors()
    {
        var html = EmailService.RenderMeetingHtml("Se ha programado una reunión", MeetingModel(), "es", Localizer);

        Assert.Contains("1 de octubre de 2026", html);
        Assert.Contains("a las", html);
        Assert.Contains("60 minutos", html);
        Assert.Contains("Aceptar", html);
        Assert.Contains("Rechazar", html);
    }

    [Fact]
    public void RenderMeetingHtml_Spanish_OneOClockHour_UsesSingularALaNotALas()
    {
        // Phase 4 review fix: "a las" was previously hardcoded regardless of hour — Spanish requires the
        // singular "a la" immediately before a 1 o'clock hour ("a la 1:00 p. m."), "a las" otherwise.
        var model = MeetingModel();
        model.StartsAtUtc = new DateTime(2026, 10, 1, 13, 0, 0, DateTimeKind.Utc); // 1:00 PM

        var html = EmailService.RenderMeetingHtml("Se ha programado una reunión", model, "es", Localizer);

        Assert.Contains("a la 1:00", html);
        Assert.DoesNotContain("a las 1:00", html);
    }

    [Fact]
    public void RenderNotificationHtml_TitleAndBodyWithMarkup_AreEncoded()
    {
        var html = EmailService.RenderNotificationHtml(Payload, Payload, "https://app.example.com/notifications", Localizer);

        Assert.Contains("&lt;a", html);
        Assert.DoesNotContain(Payload, html);
    }

    [Fact]
    public void RenderNotificationHtml_Spanish_LocalizesButtonTextOnlyNotStoredTitleBody()
    {
        // RenderNotificationHtml has no language parameter of its own — the caller
        // (SendNotificationAsync) is the one that opens the CultureScope before calling it, so this test
        // mirrors that to exercise the Spanish button text.
        string html;
        using (IepAssistant.Services.Localization.CultureScope.For("es"))
        {
            html = EmailService.RenderNotificationHtml("Stored Title", "Stored body", "https://app.example.com/notifications", Localizer);
        }

        // Title/body pass through untouched regardless of language (already localized, or not, by the
        // notification's creator); only the wrapper's button text is translated.
        Assert.Contains("Stored Title", html);
        Assert.Contains("Stored body", html);
        Assert.Contains("Ver en IEP Advisor", html);
    }

    [Fact]
    public void RenderDigestHtml_ObligationAndMeetingStudentNamesAndTitle_AreEncoded()
    {
        var model = new DigestEmailModel
        {
            RecipientFirstName = Payload,
            Obligations = new List<DigestObligationItem>
            {
                new() { StudentName = Payload, Kind = ObligationKind.AnnualReview, Status = ObligationStatus.Overdue, DueDate = DateTime.UtcNow }
            },
            UpcomingMeetings = new List<DigestMeetingItem>
            {
                new() { StudentName = Payload, Title = Payload, StartsAtUtc = DateTime.UtcNow, TimeZoneId = "America/New_York" }
            },
            DetailUrl = "https://app.example.com/notifications"
        };

        var html = EmailService.RenderDigestHtml(model, "en", Localizer);

        Assert.DoesNotContain(Payload, html);
        Assert.Contains("&lt;a", html);
    }

    [Fact]
    public void RenderDigestHtml_English_ObligationKindAndStatusAreHumanReadableNotRawIdentifiers()
    {
        // Phase 4 review fix: these were previously the raw enum ToString() (e.g. "GoalObservationStale",
        // "DueSoon") in English too — now a real display label, via Emails.resx.
        var model = new DigestEmailModel
        {
            RecipientFirstName = "Pat",
            Obligations = new List<DigestObligationItem>
            {
                new() { StudentName = "Sam", Kind = ObligationKind.GoalObservationStale, Status = ObligationStatus.DueSoon, DueDate = DateTime.UtcNow }
            },
            UpcomingMeetings = new List<DigestMeetingItem>(),
            DetailUrl = "https://app.example.com/notifications"
        };

        var html = EmailService.RenderDigestHtml(model, "en", Localizer);

        Assert.Contains("Goal progress overdue", html);
        Assert.Contains("Due soon", html);
        Assert.DoesNotContain("GoalObservationStale", html);
        Assert.DoesNotContain("DueSoon", html);
    }

    [Fact]
    public void RenderDigestHtml_Spanish_LocalizesObligationKindStatusAndForConnector()
    {
        var model = new DigestEmailModel
        {
            RecipientFirstName = "Lupe",
            Obligations = new List<DigestObligationItem>
            {
                new() { StudentName = "Sam", Kind = ObligationKind.AnnualReview, Status = ObligationStatus.Overdue, DueDate = DateTime.UtcNow }
            },
            UpcomingMeetings = new List<DigestMeetingItem>(),
            DetailUrl = "https://app.example.com/notifications"
        };

        var html = EmailService.RenderDigestHtml(model, "es", Localizer);

        Assert.Contains("Vencida", html);
        Assert.Contains("Revisión anual", html);
        Assert.Contains("para Sam", html);
        Assert.DoesNotContain("AnnualReview", html);
        Assert.DoesNotContain("Overdue", html);
    }

    [Fact]
    public void RenderDigestHtml_Spanish_MeetingRow_UsesLocalizedForConnector()
    {
        // Phase 4 review fix: the meeting row previously hardcoded the English word "for" regardless of
        // language — only the obligation row used the localized connector.
        var model = new DigestEmailModel
        {
            RecipientFirstName = "Lupe",
            Obligations = new List<DigestObligationItem>(),
            UpcomingMeetings = new List<DigestMeetingItem>
            {
                new() { StudentName = "Sam", Title = "Annual Review", StartsAtUtc = DateTime.UtcNow, TimeZoneId = "America/New_York" }
            },
            DetailUrl = "https://app.example.com/notifications"
        };

        var html = EmailService.RenderDigestHtml(model, "es", Localizer);

        Assert.Contains("Annual Review para Sam", html);
        Assert.DoesNotContain(" for ", html);
    }

    [Fact]
    public void RenderDigestHtml_Spanish_LocalizesChromeAndFormatsDates()
    {
        var model = new DigestEmailModel
        {
            RecipientFirstName = "Lupe",
            Obligations = new List<DigestObligationItem>(),
            UpcomingMeetings = new List<DigestMeetingItem>(),
            DetailUrl = "https://app.example.com/notifications"
        };

        var html = EmailService.RenderDigestHtml(model, "es", Localizer);

        Assert.Contains("Buenos días, Lupe", html);
        Assert.Contains("Plazos", html);
        Assert.Contains("No hay plazos vencidos ni próximos.", html);
        Assert.Contains("Reuniones en los próximos 7 días", html);
        Assert.Contains("No hay reuniones en los próximos 7 días.", html);
        Assert.Contains("Abrir IEP Advisor", html);
    }
}
