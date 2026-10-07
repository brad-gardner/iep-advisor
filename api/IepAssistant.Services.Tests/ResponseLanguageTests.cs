using System.Globalization;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: <see cref="ResponseLanguage.SystemLine"/> is the one place
/// that decides whether an AI system prompt gets a Spanish response-language instruction. Covers: "" for
/// English (byte-stable prompt caching unaffected), a fixed, non-empty instruction for Spanish that
/// never varies per call, region-qualified cultures ("es-MX") resolving the same as bare "es", and that
/// the instruction text keeps the required acronyms and glossary terms (so a future edit can't silently
/// drop one without a test noticing).
/// </summary>
public class ResponseLanguageTests
{
    [Fact]
    public void SystemLine_English_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("en")));
    }

    [Fact]
    public void SystemLine_Null_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, ResponseLanguage.SystemLine(null));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("de")]
    public void SystemLine_UnsupportedCulture_ReturnsEmptyString(string culture)
    {
        Assert.Equal(string.Empty, ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void SystemLine_Spanish_ReturnsNonEmptyFixedInstruction()
    {
        var line = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es"));

        Assert.NotEqual(string.Empty, line);
        Assert.Contains("Spanish", line);
        Assert.Contains("usted", line);
    }

    [Fact]
    public void SystemLine_RegionQualifiedSpanish_SameAsBareSpanish()
    {
        var bare = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es"));
        var regional = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es-MX"));

        Assert.Equal(bare, regional);
    }

    [Fact]
    public void SystemLine_Spanish_KeepsRequiredAcronyms()
    {
        var line = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es"));

        foreach (var acronym in new[] { "IEP", "ETR", "IDEA", "FAPE", "LRE" })
            Assert.Contains(acronym, line);
    }

    [Fact]
    public void SystemLine_Spanish_EmbedsKeyGlossaryPairs()
    {
        var line = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es"));

        Assert.Contains("meta anual", line);
        Assert.Contains("adaptaciones", line);
        Assert.Contains("servicios relacionados", line);
        Assert.Contains("niveles actuales de desempeño", line);
        Assert.Contains("debido proceso", line);
        Assert.Contains("equipo del IEP", line);
    }

    [Fact]
    public void SystemLine_NeverInterpolatesArguments_SameCultureAlwaysProducesIdenticalText()
    {
        // The whole point of a fixed instruction: two independent calls for the same culture produce
        // byte-identical text, because nothing from the caller (user/document text) is ever woven in.
        var first = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es"));
        var second = ResponseLanguage.SystemLine(CultureInfo.GetCultureInfo("es"));

        Assert.Equal(first, second);
    }
}
