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

    [Theory]
    [InlineData("en", true)]
    [InlineData("ES", true)]
    [InlineData("fr", false)]
    [InlineData(null, false)]
    public void IsSupported_MatchesNormalize(string? input, bool expected)
    {
        Assert.Equal(expected, SupportedLanguages.IsSupported(input));
    }
}
