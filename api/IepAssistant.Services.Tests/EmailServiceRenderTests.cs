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
/// </summary>
public class EmailServiceRenderTests
{
    private const string Payload = "<a href=\"http://attacker.example\">click</a>";

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
        var html = EmailService.RenderMeetingHtml("A meeting has been scheduled", MeetingModel(title: Payload));

        Assert.Contains("&lt;a", html);
        Assert.DoesNotContain(Payload, html);
    }

    [Fact]
    public void RenderMeetingHtml_LocationWithMarkup_IsEncoded()
    {
        var html = EmailService.RenderMeetingHtml("A meeting has been scheduled", MeetingModel(location: Payload));

        Assert.Contains("&lt;a", html);
        Assert.DoesNotContain(Payload, html);
    }

    [Fact]
    public void RenderMeetingHtml_StudentFirstNameAndOrganizerName_AreEncoded()
    {
        var model = MeetingModel();
        model.StudentFirstName = Payload;
        model.OrganizerName = Payload;

        var html = EmailService.RenderMeetingHtml("A meeting has been scheduled", model);

        Assert.DoesNotContain(Payload, html);
        // Two occurrences: student name and organizer name.
        Assert.Equal(2, html.Split("&lt;a href=").Length - 1);
    }

    [Fact]
    public void RenderNotificationHtml_TitleAndBodyWithMarkup_AreEncoded()
    {
        var html = EmailService.RenderNotificationHtml(Payload, Payload, "https://app.example.com/notifications");

        Assert.Contains("&lt;a", html);
        Assert.DoesNotContain(Payload, html);
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

        var html = EmailService.RenderDigestHtml(model);

        Assert.DoesNotContain(Payload, html);
        Assert.Contains("&lt;a", html);
    }
}
