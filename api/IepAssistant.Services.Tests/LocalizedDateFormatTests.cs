using System.Globalization;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 4 review fix: <see cref="LocalizedDateFormat"/> is the single
/// place EmailService and the Notification title/body builders (MeetingService, EvaluationCaseService)
/// format a date/date-time for a recipient's language — replacing several independently-drifted private
/// copies (EmailService's own four format helpers, and the separate <c>NotificationDateFormat</c> class).
/// English formats must stay byte-identical to what those replaced copies produced.
/// </summary>
public class LocalizedDateFormatTests
{
    private static readonly DateTime Value = new(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc); // 2:00 PM

    [Fact]
    public void LongDate_English_MatchesPriorFormat()
        => Assert.Equal("October 15, 2026", LocalizedDateFormat.LongDate(Value, "en"));

    [Fact]
    public void LongDate_Spanish_UsesDeConnectors()
        => Assert.Equal("15 de octubre de 2026", LocalizedDateFormat.LongDate(Value, "es"));

    [Fact]
    public void LongDate_NullLanguage_FallsBackToEnglish()
        => Assert.Equal("October 15, 2026", LocalizedDateFormat.LongDate(Value, null));

    [Fact]
    public void ShortDate_English_MatchesPriorFormat()
        => Assert.Equal("Oct 15, 2026", LocalizedDateFormat.ShortDate(Value, "en"));

    [Fact]
    public void ShortDate_Spanish_UsesDeConnectors()
        => Assert.Equal("15 de oct de 2026", LocalizedDateFormat.ShortDate(Value, "es"));

    [Fact]
    public void ShortDateTime_English_MatchesPriorFormat()
        => Assert.Equal("Oct 15, 2:00 PM", LocalizedDateFormat.ShortDateTime(Value, "en"));

    [Fact]
    public void ShortDateTime_Spanish_UsesDeConnectorBeforeMonth()
    {
        var result = LocalizedDateFormat.ShortDateTime(Value, "es");
        Assert.StartsWith("15 de oct, 2:00", result);
    }

    [Fact]
    public void MeetingDateTime_English_MatchesPriorFormat()
        => Assert.Equal("October 15, 2026 at 2:00 PM", LocalizedDateFormat.MeetingDateTime(Value, "en"));

    [Fact]
    public void MeetingDateTime_Spanish_TwoPm_UsesALas()
    {
        // The PM designator ("p. m.", with a narrow no-break space) comes from the Spanish culture's own
        // DateTimeFormat rather than a hardcoded literal, so this doesn't depend on exactly which
        // whitespace character .NET's ICU data uses between "p." and "m.".
        var pm = CultureInfo.GetCultureInfo("es").DateTimeFormat.PMDesignator;
        Assert.Equal($"15 de octubre de 2026 a las 2:00 {pm}", LocalizedDateFormat.MeetingDateTime(Value, "es"));
    }

    [Fact]
    public void MeetingDateTime_Spanish_OnePm_UsesSingularALa()
    {
        var onePm = new DateTime(2026, 10, 15, 13, 0, 0, DateTimeKind.Utc);
        var result = LocalizedDateFormat.MeetingDateTime(onePm, "es");

        Assert.Contains("a la 1:00", result);
        Assert.DoesNotContain("a las 1:00", result);
    }

    [Fact]
    public void MeetingDateTime_Spanish_OneAm_UsesSingularALa()
    {
        var oneAm = new DateTime(2026, 10, 15, 1, 0, 0, DateTimeKind.Utc);
        var result = LocalizedDateFormat.MeetingDateTime(oneAm, "es");

        Assert.Contains("a la 1:00", result);
        Assert.DoesNotContain("a las 1:00", result);
    }

    [Fact]
    public void MeetingDateTime_Spanish_MidnightAndNoon_UseALas()
    {
        var midnight = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
        var noon = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        Assert.Contains("a las 12:00", LocalizedDateFormat.MeetingDateTime(midnight, "es"));
        Assert.Contains("a las 12:00", LocalizedDateFormat.MeetingDateTime(noon, "es"));
    }

    [Fact]
    public void MeetingDateTime_UnsupportedLanguage_FallsBackToEnglish()
        => Assert.Equal("October 15, 2026 at 2:00 PM", LocalizedDateFormat.MeetingDateTime(Value, "fr"));
}
