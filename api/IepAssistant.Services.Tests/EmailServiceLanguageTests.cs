using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06), phase 1: the password-reset and magic-link emails render in the
/// recipient's saved language (<c>PreferredLanguage ?? "en"</c>), via the real <c>Emails.resx</c>/
/// <c>Emails.es.resx</c> resources (not a stub) — covers the English default, the Spanish subject/body,
/// and that composing one language's email leaves the ambient culture exactly as it found it (so a later
/// English send in the same worker/request isn't accidentally rendered in Spanish — see
/// <see cref="CultureScopeTests"/> for the underlying guarantee). These two methods take an explicit
/// <c>language</c> parameter and never query <c>Users</c>, so the DbContext below is wired up (phase 4
/// added it to <see cref="EmailService"/>'s constructor for the recipient-lookup methods) but never hits
/// the database in this file — see <c>EmailServicePhase4LanguageTests</c> for the lookup-driven methods.
/// </summary>
public sealed class EmailServiceLanguageTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public EmailServiceLanguageTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = new ApplicationDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private sealed class CapturingQueue : IOutboundEmailQueue
    {
        public OutboundEmailDraft? LastDraft { get; private set; }

        public Task<int> EnqueueAsync(OutboundEmailDraft draft, CancellationToken ct = default)
        {
            LastDraft = draft;
            return Task.FromResult(1);
        }
    }

    private EmailService CreateService(CapturingQueue queue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendUrl"] = "https://app.example.com" })
            .Build();
        return new EmailService(configuration, queue, TestSupport.TestLocalizers.Emails(), new ApplicationDbContext(_options));
    }

    [Fact]
    public async Task SendPasswordResetEmail_NullLanguage_RendersEnglish()
    {
        var queue = new CapturingQueue();
        await CreateService(queue).SendPasswordResetEmailAsync("parent@example.com", "raw-token", language: null);

        Assert.Equal("Reset Your IEP Advisor Password", queue.LastDraft!.Subject);
        Assert.Contains("Reset Your Password", queue.LastDraft.HtmlBody);
    }

    [Fact]
    public async Task SendPasswordResetEmail_Spanish_RendersSpanishSubjectAndBody()
    {
        var queue = new CapturingQueue();
        await CreateService(queue).SendPasswordResetEmailAsync("padre@example.com", "raw-token", language: "es");

        Assert.Equal("Restablezca su contraseña de IEP Advisor", queue.LastDraft!.Subject);
        Assert.Contains("Restablecer su contraseña", queue.LastDraft.HtmlBody);
        Assert.Contains("Restablezca su contraseña de IEP Advisor visitando:", queue.LastDraft.TextBody);
    }

    [Fact]
    public async Task SendMagicLinkEmail_Spanish_RendersSpanishSubjectAndGreeting()
    {
        var queue = new CapturingQueue();
        await CreateService(queue).SendMagicLinkEmailAsync("maestra@example.com", "Dana", "https://app.example.com/auth/magic?token=abc", language: "ES");

        Assert.Equal("Su enlace de inicio de sesión de IEP Advisor", queue.LastDraft!.Subject);
        Assert.Contains("Hola Dana, haga clic abajo para iniciar sesión.", queue.LastDraft.HtmlBody);
    }

    [Fact]
    public async Task SendPasswordResetEmail_TokenContainsPlusSlashEquals_EscapesAndRoundTripsThroughUrl()
    {
        // Regression: the raw base64 reset token can contain '+', '/', '=' — unescaped, a '+' decodes as
        // a space (and '/'/'=' can confuse query parsing), corrupting the token before ResetPasswordAsync
        // ever sees it. The magic-link URL already escapes (see MagicLinkService); this proves the
        // password-reset URL does too, and that the escaped value decodes back to the exact original.
        var queue = new CapturingQueue();
        const string rawToken = "ab+c/DE==";

        await CreateService(queue).SendPasswordResetEmailAsync("parent@example.com", rawToken, language: null);

        var textBody = queue.LastDraft!.TextBody;
        Assert.NotNull(textBody);
        var match = System.Text.RegularExpressions.Regex.Match(textBody, @"token=(\S+)");
        Assert.True(match.Success, "expected a token= query parameter in the plain-text body");

        var encodedToken = match.Groups[1].Value;
        Assert.DoesNotContain("+", encodedToken);
        Assert.Equal(rawToken, Uri.UnescapeDataString(encodedToken));
    }

    [Fact]
    public async Task SendPasswordResetEmail_RestoresAmbientCultureAfterSending()
    {
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            var queue = new CapturingQueue();
            await CreateService(queue).SendPasswordResetEmailAsync("parent@example.com", "raw-token", language: "es");

            Assert.Equal("en", CultureInfo.CurrentCulture.Name);
            Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }
}
