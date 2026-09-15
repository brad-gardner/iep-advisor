namespace IepAssistant.Domain.Entities;

/// <summary>
/// IDEA's 13 disability categories plus <see cref="Other"/> (plan 3, decision 2). Serialized as the
/// enum name and stored as a string column. Display labels (incl. Ohio's wording) come from
/// <see cref="DisabilityCategoryExtensions.ToDisplay"/>. Legacy free text that does not map is stored as
/// <see cref="Other"/> with the original preserved in <c>SchoolStudent.LegacyDisabilityText</c>.
/// </summary>
public enum DisabilityCategory
{
    Autism,
    DeafBlindness,
    Deafness,
    DevelopmentalDelay,
    EmotionalDisturbance,
    HearingImpairment,
    IntellectualDisability,
    MultipleDisabilities,
    OrthopedicImpairment,
    OtherHealthImpairment,
    SpecificLearningDisability,
    SpeechOrLanguageImpairment,
    TraumaticBrainInjury,
    VisualImpairment,
    Other
}

public static class DisabilityCategoryExtensions
{
    public static string ToDisplay(this DisabilityCategory category) => category switch
    {
        DisabilityCategory.Autism => "Autism",
        DisabilityCategory.DeafBlindness => "Deaf-Blindness",
        DisabilityCategory.Deafness => "Deafness",
        DisabilityCategory.DevelopmentalDelay => "Developmental Delay",
        DisabilityCategory.EmotionalDisturbance => "Emotional Disturbance",
        DisabilityCategory.HearingImpairment => "Hearing Impairment",
        DisabilityCategory.IntellectualDisability => "Intellectual Disability",
        DisabilityCategory.MultipleDisabilities => "Multiple Disabilities",
        DisabilityCategory.OrthopedicImpairment => "Orthopedic Impairment",
        DisabilityCategory.OtherHealthImpairment => "Other Health Impairment",
        DisabilityCategory.SpecificLearningDisability => "Specific Learning Disability",
        DisabilityCategory.SpeechOrLanguageImpairment => "Speech or Language Impairment",
        DisabilityCategory.TraumaticBrainInjury => "Traumatic Brain Injury",
        DisabilityCategory.VisualImpairment => "Visual Impairment",
        _ => "Other"
    };

    /// <summary>
    /// Best-effort parse of free text: the enum name, the display label, common abbreviations
    /// (SLD, OHI, ASD, ED, ID, MD, OI, TBI, VI, HI, DD, SLI, DB) and a few Ohio variants
    /// ("Cognitive Disability", "Speech and Language Impairment"). Null when unrecognized.
    /// </summary>
    public static DisabilityCategory? TryParseDisplay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = Normalize(text);
        foreach (DisabilityCategory c in Enum.GetValues(typeof(DisabilityCategory)))
        {
            if (Normalize(c.ToString()) == t || Normalize(c.ToDisplay()) == t)
                return c;
        }
        return t switch
        {
            "SLD" or "LEARNINGDISABILITY" or "SPECIFICLEARNINGDISABILITIES" => DisabilityCategory.SpecificLearningDisability,
            "OHI" or "OTHERHEALTHIMPAIRED" or "OTHERHEALTHIMPAIRMENTMAJOR" or "OTHERHEALTHIMPAIRMENTMINOR" => DisabilityCategory.OtherHealthImpairment,
            "ASD" or "AUTISMSPECTRUMDISORDER" => DisabilityCategory.Autism,
            "ED" or "EMOTIONALDISABILITY" or "EMOTIONALBEHAVIORALDISABILITY" or "SERIOUSEMOTIONALDISTURBANCE" => DisabilityCategory.EmotionalDisturbance,
            "ID" or "COGNITIVEDISABILITY" or "INTELLECTUALDISABILITIES" or "MR" => DisabilityCategory.IntellectualDisability,
            "MD" or "MULTIPLEDISABILITY" => DisabilityCategory.MultipleDisabilities,
            "OI" or "ORTHOPEDICIMPAIRED" => DisabilityCategory.OrthopedicImpairment,
            "TBI" => DisabilityCategory.TraumaticBrainInjury,
            "VI" or "VISUALIMPAIRMENTINCLUDINGBLINDNESS" or "BLINDNESS" => DisabilityCategory.VisualImpairment,
            "HI" or "HEARINGIMPAIRED" or "DEAFNESSHEARINGIMPAIRMENT" or "HEARINGIMPAIRMENTINCLUDINGDEAFNESS" => DisabilityCategory.HearingImpairment,
            "DD" or "PRESCHOOLDEVELOPMENTALDELAY" => DisabilityCategory.DevelopmentalDelay,
            "SLI" or "SPEECHANDLANGUAGEIMPAIRMENT" or "SPEECHLANGUAGEIMPAIRMENT" or "SPEECHIMPAIRMENT" or "LANGUAGEIMPAIRMENT" => DisabilityCategory.SpeechOrLanguageImpairment,
            "DB" or "DEAFBLIND" => DisabilityCategory.DeafBlindness,
            _ => null
        };
    }

    private static string Normalize(string s)
        => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
