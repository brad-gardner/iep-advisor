using System.Globalization;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// <see cref="CultureScope"/> is the disposable that out-of-request code (background workers, email
/// composition) uses to set the ambient UI culture for the duration of a unit of work. Covers: it applies
/// the requested supported language to CurrentUICulture only, it falls back to English for
/// null/unsupported input rather than throwing (a stale or hand-edited PreferredLanguage must never crash
/// a worker), it never touches CurrentCulture (P2 fix, 2026-10-06 multilingual plan review — date/number
/// formatting must not follow the UI language), and — most importantly — it restores whatever UI culture
/// was ambient before, even when the scope's body throws.
/// </summary>
public class CultureScopeTests
{
    [Fact]
    public void For_Spanish_SetsUiCultureOnly_CurrentCultureUnaffected()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");

            using (CultureScope.For("es"))
            {
                Assert.Equal("es", CultureInfo.CurrentUICulture.Name);
                Assert.Equal("en", CultureInfo.CurrentCulture.Name);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("ES-MX")] // region-qualified input is not what SupportedLanguages.Normalize handles — falls back to en.
    public void For_UnsupportedOrMissingLanguage_FallsBackToEnglishUiCulture(string? language)
    {
        using (CultureScope.For(language))
        {
            Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
        }
    }

    [Fact]
    public void Dispose_RestoresThePriorAmbientUiCulture()
    {
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            using (CultureScope.For("es"))
            {
                Assert.Equal("es", CultureInfo.CurrentUICulture.Name);
            }

            Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Fact]
    public void Dispose_RestoresPriorUiCulture_EvenWhenBodyThrows()
    {
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            var threw = false;
            try
            {
                using (CultureScope.For("es"))
                {
                    throw new InvalidOperationException("boom");
                }
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw);
            Assert.Equal("en", CultureInfo.CurrentUICulture.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }
}
