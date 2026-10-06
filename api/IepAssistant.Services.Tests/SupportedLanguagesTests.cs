using System.Globalization;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// <see cref="SupportedLanguages"/> is the one place the supported-language list and normalization rule
/// live — <c>PUT /api/auth/me</c> validation, <see cref="CultureScope"/>, and the request-culture
/// provider all resolve through it, so a bug here would silently drift every one of those.
/// </summary>
public class SupportedLanguagesTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("es", "es")]
    [InlineData("EN", "en")]
    [InlineData("Es", "es")]
    [InlineData(" en ", "en")]
    public void Normalize_SupportedCodeAnyCaseOrPadding_ReturnsLowercaseCode(string input, string expected)
    {
        Assert.Equal(expected, SupportedLanguages.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("fr")]
    [InlineData("es-MX")] // region-qualified values are not normalized here (see CultureScopeTests remark).
    [InlineData("english")]
    public void Normalize_UnsupportedOrMissing_ReturnsNull(string? input)
    {
        Assert.Null(SupportedLanguages.Normalize(input));
    }

    /// <summary>
    /// <see cref="SupportedLanguages.ForRecipient"/> backs the password-reset and magic-link flows'
    /// language choice (previously duplicated inline in each service) — the saved value wins when it's a
    /// supported code, otherwise the ambient UI culture is the fallback, never a throw.
    /// </summary>
    [Fact]
    public void ForRecipient_SavedLanguageSupported_ReturnsSavedLanguage_RegardlessOfAmbientUiCulture()
    {
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            Assert.Equal("es", SupportedLanguages.ForRecipient("es"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")] // unsupported — falls through same as missing
    public void ForRecipient_SavedLanguageMissingOrUnsupported_FallsBackToAmbientUiCulture(string? saved)
    {
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");

            Assert.Equal("es", SupportedLanguages.ForRecipient(saved));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Fact]
    public void ForRecipient_NoSavedLanguageAndAmbientUiCultureUnsupported_ReturnsNull()
    {
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");

            Assert.Null(SupportedLanguages.ForRecipient(null));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }
}
