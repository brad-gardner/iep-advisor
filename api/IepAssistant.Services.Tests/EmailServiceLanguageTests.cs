using System.Globalization;
using Microsoft.Extensions.Configuration;
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
/// <see cref="CultureScopeTests"/> for the underlying guarantee).
/// </summary>
public sealed class EmailServiceLanguageTests
{
    private sealed class CapturingQueue : IOutboundEmailQueue
    {
        public OutboundEmailDraft? LastDraft { get; private set; }

        public Task<int> EnqueueAsync(OutboundEmailDraft draft, CancellationToken ct = default)
        {
            LastDraft = draft;
            return Task.FromResult(1);
        }
    }

    private static EmailService CreateService(CapturingQueue queue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["App:FrontendUrl"] = "https://app.example.com" })
            .Build();
        return new EmailService(configuration, queue, TestSupport.TestLocalizers.Emails());
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
