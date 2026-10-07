using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06), phase 4: every remaining <see cref="EmailService"/> email kind
/// renders in the RECIPIENT's language, resolved by looking up <c>toEmail</c> in <c>Users</c>
/// (<see cref="EmailServiceTestDb"/> seeds real rows — this is deliberately not a stubbed lookup).
/// A pre-account recipient (no User row) falls back to the sender's current request UI culture and gets
/// <c>?lang=</c> appended to the invite/landing link. Covers one representative kind end-to-end per
/// category (invite, meeting, notification wrapper, digest, account-deletion) rather than every kind
/// exhaustively, plus the security/encoding requirements that cut across all of them.
/// </summary>
public sealed class EmailServicePhase4LanguageTests : IDisposable
{
    private const string Payload = "<script>alert(1)</script>";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    private sealed class CapturingQueue : IOutboundEmailQueue
    {
        public OutboundEmailDraft? LastDraft { get; private set; }

        public Task<int> EnqueueAsync(OutboundEmailDraft draft, CancellationToken ct = default)
        {
            LastDraft = draft;
            return Task.FromResult(1);
        }
    }

    public EmailServicePhase4LanguageTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private void SeedUser(string email, string? preferredLanguage)
    {
        using var ctx = new ApplicationDbContext(_options);
        ctx.Users.Add(new User
        {
            Email = email,
            PasswordHash = "x",
            FirstName = "Test",
            LastName = "User",
            Role = UserRole.Parent,
            PreferredLanguage = preferredLanguage
        });
        ctx.SaveChanges();
    }

    private EmailService CreateService(CapturingQueue queue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendUrl"] = "https://app.example.com" })
            .Build();
        return new EmailService(configuration, queue, TestSupport.TestLocalizers.Emails(), new ApplicationDbContext(_options));
    }

    // ----------------------------------------------------------------- ShareInvite: recipient lookup, lang param, encoding

    [Fact]
    public async Task ShareInvite_RecipientHasSpanishAccount_RendersSpanishAndNoLangParam()
    {
        SeedUser("padre@example.com", "es");
        var queue = new CapturingQueue();

        await CreateService(queue).SendShareInviteEmailAsync("padre@example.com", "Ana", "Sam", "Collaborator", "tok123");

        Assert.Equal("Ana le invitó a IEP Advisor", queue.LastDraft!.Subject);
        Assert.Contains("colaborar en la información del IEP de Sam", queue.LastDraft.HtmlBody);
        Assert.DoesNotContain("lang=", queue.LastDraft.HtmlBody.Split("href=\"")[1].Split('"')[0]);
    }

    [Fact]
    public async Task ShareInvite_NoAccount_UsesEnglishDefaultAndAppendsLangParam()
    {
        var queue = new CapturingQueue();

        // No ambient request culture in a unit test -> SupportedLanguages.ForRecipient(null) falls back
        // to English (design: "outside a request -> en").
        await CreateService(queue).SendShareInviteEmailAsync("new-parent@example.com", "Ana", "Sam", "Collaborator", "tok123");

        Assert.Equal("Ana invited you to IEP Advisor", queue.LastDraft!.Subject);
        var href = queue.LastDraft.HtmlBody.Split("href=\"")[1].Split('"')[0];
        Assert.Contains("lang=en", href);
        Assert.Contains("/accept-invite?token=tok123&amp;lang=en", href);
    }

    [Fact]
    public async Task ShareInvite_InviterNameContainsScript_IsHtmlEncodedInBodyButNotInPlainText()
    {
        var queue = new CapturingQueue();

        await CreateService(queue).SendShareInviteEmailAsync("new-parent@example.com", Payload, "Sam", "Viewer", "tok123");

        Assert.DoesNotContain(Payload, queue.LastDraft!.HtmlBody);
        Assert.Contains("&lt;script&gt;", queue.LastDraft.HtmlBody);
        // Plain-text parts are deliberately unencoded.
        Assert.Contains(Payload, queue.LastDraft.TextBody);
    }

    [Fact]
    public async Task ShareInvite_TokenIsUriEscaped()
    {
        var queue = new CapturingQueue();
        await CreateService(queue).SendShareInviteEmailAsync("new-parent@example.com", "Ana", "Sam", "Collaborator", "ab+c/DE==");

        Assert.Contains("token=ab%2Bc%2FDE%3D%3D", queue.LastDraft!.TextBody);
    }

    [Fact]
    public async Task ShareInvite_EnglishDefault_SubjectAndCtaMatchCurrentCopy()
    {
        SeedUser("parent@example.com", "en");
        var queue = new CapturingQueue();

        await CreateService(queue).SendShareInviteEmailAsync("parent@example.com", "Ana", "Sam", "Collaborator", "tok123");

        Assert.Equal("Ana invited you to IEP Advisor", queue.LastDraft!.Subject);
        Assert.Contains("has invited you to collaborate on Sam's IEP information on IEP Advisor.", queue.LastDraft.HtmlBody);
        Assert.Contains("Accept Invitation", queue.LastDraft.HtmlBody);
        Assert.Contains("This invitation expires in 7 days.", queue.LastDraft.HtmlBody);
    }

    // ----------------------------------------------------------------- StaffInvite: district content passthrough

    [Fact]
    public async Task StaffInvite_SpanishRecipient_TranslatesChromeButKeepsDistrictContentAsWritten()
    {
        SeedUser("staff@example.com", "es");
        var queue = new CapturingQueue();

        await CreateService(queue).SendStaffInviteEmailAsync("staff@example.com", "Lincoln USD", "Lincoln High", "4th Grade Teacher", "tok123");

        Assert.Equal("Le han invitado a unirse a Lincoln USD en IEP Advisor", queue.LastDraft!.Subject);
        // District/school names and the (district-authored) role name are never translated.
        Assert.Contains("Lincoln High", queue.LastDraft.HtmlBody);
        Assert.Contains("Lincoln USD", queue.LastDraft.HtmlBody);
        Assert.Contains("4th Grade Teacher", queue.LastDraft.HtmlBody);
        Assert.Contains("Se le ha invitado a unirse a", queue.LastDraft.HtmlBody);
    }

    // ----------------------------------------------------------------- StaffInviteExpiring: Spanish date format

    [Fact]
    public async Task StaffInviteExpiring_SpanishRecipient_FormatsExpiryDateInSpanish()
    {
        SeedUser("admin@example.com", "es");
        var queue = new CapturingQueue();

        await CreateService(queue).SendStaffInviteExpiringEmailAsync("admin@example.com", "newhire@example.com", "Lincoln USD", null, new DateTime(2026, 11, 5));

        Assert.Contains("5 de noviembre de 2026", queue.LastDraft!.HtmlBody);
        Assert.Contains("Una invitación de personal está por vencer", queue.LastDraft.HtmlBody);
    }

    // ----------------------------------------------------------------- BetaInvite: pre-account + lang param

    [Fact]
    public async Task BetaInvite_NoAccount_RendersEnglishAndAppendsLangParam()
    {
        var queue = new CapturingQueue();

        await CreateService(queue).SendBetaInviteEmailAsync("prospect@example.com", "CODE123");

        Assert.Equal("Welcome to the IEP Advisor Beta", queue.LastDraft!.Subject);
        var href = queue.LastDraft.HtmlBody.Split("href=\"")[2].Split('"')[0]; // mailto link is href[1]; signup button is href[2]
        Assert.Contains("lang=en", href);
    }

    [Fact]
    public async Task BetaInvite_SpanishAccount_RendersSpanishLetter()
    {
        SeedUser("ya-signed-up@example.com", "es");
        var queue = new CapturingQueue();

        await CreateService(queue).SendBetaInviteEmailAsync("ya-signed-up@example.com", "CODE123");

        Assert.Equal("Bienvenido a la versión beta de IEP Advisor", queue.LastDraft!.Subject);
        Assert.Contains("Soy Brad, el fundador", queue.LastDraft.HtmlBody);
        Assert.Contains("Brad Gardner", queue.LastDraft.HtmlBody);
    }

    // ----------------------------------------------------------------- AccountDeletionCancelLink: Spanish date, untouched URL

    [Fact]
    public async Task AccountDeletionCancelLink_SpanishRecipient_FormatsDateAndDoesNotMutateSignedUrl()
    {
        SeedUser("leaving@example.com", "es");
        var queue = new CapturingQueue();
        const string signedUrl = "https://app.example.com/cancel-deletion?token=abc123&sig=def456";

        await CreateService(queue).SendAccountDeletionCancelLinkEmailAsync("leaving@example.com", "Jo", signedUrl, new DateTime(2026, 11, 5));

        Assert.Contains("5 de noviembre de 2026", queue.LastDraft!.HtmlBody);
        Assert.Contains("Su cuenta está programada para eliminación", queue.LastDraft.HtmlBody);
        // The signed cancel URL must round-trip untouched (modulo HTML-encoding the '&').
        Assert.Equal(signedUrl, queue.LastDraft.TextBody!.Split("Cancelar eliminación: ")[1].Split('\n')[0]);
    }

    // ----------------------------------------------------------------- StudentInvite: structured context clause (phase 4 review fix)

    [Fact]
    public async Task StudentInvite_ParentChildContext_SpanishRecipient_RendersSpanishClauseWithChildName()
    {
        SeedUser("maestra-es@example.com", "es");
        var queue = new CapturingQueue();
        var context = new StudentInviteContext { Kind = StudentInviteContextKind.ParentChild, ChildFirstName = "Sam" };

        await CreateService(queue).SendStudentInviteEmailAsync("maestra-es@example.com", "Ana", context, "tok123");

        Assert.Contains("para contribuir al IEP de Sam", queue.LastDraft!.HtmlBody);
    }

    [Fact]
    public async Task StudentInvite_EducatorSchoolContext_SpanishRecipient_RendersSpanishClauseWithSchoolName()
    {
        SeedUser("maestra-es2@example.com", "es");
        var queue = new CapturingQueue();
        var context = new StudentInviteContext { Kind = StudentInviteContextKind.EducatorSchool, SchoolName = "Lincoln High School" };

        await CreateService(queue).SendStudentInviteEmailAsync("maestra-es2@example.com", "Pat", context, "tok123");

        Assert.Contains("en la escuela Lincoln High School", queue.LastDraft!.HtmlBody);
    }

    [Fact]
    public async Task StudentInvite_EducatorSchoolContext_NoSchoolName_UsesLocalizedUnknownSchoolClause()
    {
        SeedUser("maestra-es3@example.com", "es");
        var queue = new CapturingQueue();
        var context = new StudentInviteContext { Kind = StudentInviteContextKind.EducatorSchool, SchoolName = null };

        await CreateService(queue).SendStudentInviteEmailAsync("maestra-es3@example.com", "Pat", context, "tok123");

        Assert.Contains("en la escuela su escuela", queue.LastDraft!.HtmlBody);
    }

    [Fact]
    public async Task StudentInvite_EnglishDefault_ContextClauseMatchesCurrentCopy()
    {
        var queue = new CapturingQueue();
        var context = new StudentInviteContext { Kind = StudentInviteContextKind.ParentChild, ChildFirstName = "Sam" };

        await CreateService(queue).SendStudentInviteEmailAsync("unseeded@example.com", "Ana", context, "tok123");

        // The apostrophe in "Sam's" is HTML-encoded, same as every other interpolated value in this body.
        Assert.Contains("has invited you to set up your own student account on IEP Advisor to contribute to Sam&#39;s IEP.", queue.LastDraft!.HtmlBody);
    }

    // ----------------------------------------------------------------- Meeting invitation: full send, Spanish

    [Fact]
    public async Task SendMeetingInvitation_SpanishRecipient_RendersSpanishSubjectAndWhenLine()
    {
        SeedUser("maestro@example.com", "es");
        var queue = new CapturingQueue();
        var model = new MeetingEmailModel
        {
            StudentFirstName = "Sam",
            Title = "Annual Review",
            StartsAtUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
            TimeZoneId = "America/New_York",
            DurationMinutes = 60,
            OrganizerName = "Pat Organizer",
            DetailUrl = "https://app.example.com/meetings/1"
        };

        await CreateService(queue).SendMeetingInvitationAsync("maestro@example.com", model, new byte[] { 1, 2, 3 });

        Assert.Equal("Reunión programada: Annual Review para Sam", queue.LastDraft!.Subject);
        Assert.Contains("1 de octubre de 2026", queue.LastDraft.HtmlBody);
        Assert.Contains("a las", queue.LastDraft.HtmlBody);
        Assert.Contains("60 minutos", queue.LastDraft.HtmlBody);
        Assert.Single(queue.LastDraft.Attachments ?? Array.Empty<OutboundEmailAttachmentDraft>());
    }

    [Fact]
    public async Task SendMeetingInvitation_EnglishDefault_SubjectMatchesCurrentCopy()
    {
        var queue = new CapturingQueue();
        var model = new MeetingEmailModel
        {
            StudentFirstName = "Sam",
            Title = "Annual Review",
            StartsAtUtc = new DateTime(2026, 10, 1, 14, 0, 0, DateTimeKind.Utc),
            TimeZoneId = "America/New_York",
            DurationMinutes = 60,
            OrganizerName = "Pat Organizer",
            DetailUrl = "https://app.example.com/meetings/1"
        };

        await CreateService(queue).SendMeetingInvitationAsync("unseeded@example.com", model, new byte[] { 1, 2, 3 });

        Assert.Equal("Meeting scheduled: Annual Review for Sam", queue.LastDraft!.Subject);
        Assert.Contains("October 1, 2026 at 2:00 PM (America/New_York)", queue.LastDraft.HtmlBody);
    }

    // ----------------------------------------------------------------- Notification: wrapper chrome only

    [Fact]
    public async Task SendNotification_SpanishRecipient_LocalizesButtonButLeavesStoredTitleBodyAsGiven()
    {
        SeedUser("parent-es@example.com", "es");
        var queue = new CapturingQueue();

        await CreateService(queue).SendNotificationAsync("parent-es@example.com", "Draft shared with you", "A new draft is ready.", "https://app.example.com/drafts/1");

        // Title/body already arrive localized (or not) from the notification's creator; only the
        // wrapper's button text changes here.
        Assert.Contains("Draft shared with you", queue.LastDraft!.HtmlBody);
        Assert.Contains("Ver en IEP Advisor", queue.LastDraft.HtmlBody);
        Assert.Contains("Ver en IEP Advisor", queue.LastDraft.TextBody);
    }

    // ----------------------------------------------------------------- Digest: full send, Spanish

    [Fact]
    public async Task SendDigest_SpanishRecipient_RendersSpanishChromeAndDates()
    {
        SeedUser("parent-es@example.com", "es");
        var queue = new CapturingQueue();
        var model = new DigestEmailModel
        {
            RecipientFirstName = "Lupe",
            Obligations = new List<DigestObligationItem>
            {
                new() { StudentName = "Sam", Kind = ObligationKind.AnnualReview, Status = ObligationStatus.Overdue, DueDate = new DateTime(2026, 10, 15) }
            },
            UpcomingMeetings = new List<DigestMeetingItem>(),
            DetailUrl = "https://app.example.com"
        };

        await CreateService(queue).SendDigestAsync("parent-es@example.com", model);

        Assert.Equal("Su resumen diario de IEP Advisor", queue.LastDraft!.Subject);
        Assert.Contains("Buenos días, Lupe", queue.LastDraft.HtmlBody);
        Assert.Contains("vence el 15 oct 2026", queue.LastDraft.HtmlBody);
        Assert.Contains("Abrir IEP Advisor", queue.LastDraft.TextBody);
    }

    [Fact]
    public async Task SendDigest_EnglishDefault_MatchesCurrentCopy()
    {
        var queue = new CapturingQueue();
        var model = new DigestEmailModel
        {
            RecipientFirstName = "Pat",
            Obligations = new List<DigestObligationItem>(),
            UpcomingMeetings = new List<DigestMeetingItem>(),
            DetailUrl = "https://app.example.com"
        };

        await CreateService(queue).SendDigestAsync("unseeded@example.com", model);

        Assert.Equal("Your daily IEP Advisor digest", queue.LastDraft!.Subject);
        Assert.Contains("Good morning, Pat", queue.LastDraft.HtmlBody);
        Assert.Contains("No overdue or upcoming deadlines. Nice work.", queue.LastDraft.HtmlBody);
        Assert.Contains("Open IEP Advisor: https://app.example.com", queue.LastDraft.TextBody);
    }

    // ----------------------------------------------------------------- Recipient lookup normalization

    [Fact]
    public async Task ShareInvite_RecipientEmailDiffersOnlyByCase_StillMatchesAccount()
    {
        SeedUser("mixedcase@example.com", "es");
        var queue = new CapturingQueue();

        await CreateService(queue).SendShareInviteEmailAsync("MixedCase@Example.com", "Ana", "Sam", "Collaborator", "tok123");

        Assert.Contains("colaborar en la información del IEP de Sam", queue.LastDraft!.HtmlBody);
    }
}
