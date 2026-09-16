namespace IepAssistant.Domain.Entities;

/// <summary>
/// Controlled grade values for a <see cref="SchoolStudent"/> (plan 3, decision 2). Serialized as the
/// enum name (JsonStringEnumConverter) and stored as a string column via HasConversion&lt;string&gt;().
/// Display labels ("PK", "K", "1"…"12", "Ungraded") come from <see cref="GradeLevelExtensions"/>.
/// </summary>
public enum GradeLevel
{
    PK,
    K,
    G1,
    G2,
    G3,
    G4,
    G5,
    G6,
    G7,
    G8,
    G9,
    G10,
    G11,
    G12,
    Ungraded
}

public static class GradeLevelExtensions
{
    /// <summary>Human label: "PK", "K", "1"…"12", "Ungraded".</summary>
    public static string ToDisplay(this GradeLevel grade) => grade switch
    {
        GradeLevel.PK => "PK",
        GradeLevel.K => "K",
        GradeLevel.Ungraded => "Ungraded",
        _ => grade.ToString().Substring(1)
    };

    /// <summary>
    /// Best-effort parse of free text ("K", "Kindergarten", "PK", "Pre-K", "5", "05", "G5", "Grade 5",
    /// "5th", "Ungraded"). Returns null when the text is not recognizable.
    /// </summary>
    public static GradeLevel? TryParseDisplay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Trim().ToUpperInvariant();
        switch (t)
        {
            case "K":
            case "KG":
            case "KINDERGARTEN":
                return GradeLevel.K;
            case "PK":
            case "PREK":
            case "PRE-K":
            case "PRE K":
            case "PRESCHOOL":
            case "PRE-KINDERGARTEN":
                return GradeLevel.PK;
            case "UNGRADED":
            case "UG":
                return GradeLevel.Ungraded;
        }
        if (t.StartsWith("GRADE ")) t = t.Substring(6).Trim();
        else if (t.StartsWith("G") && t.Length <= 3) t = t.Substring(1);
        foreach (var suffix in new[] { "ST", "ND", "RD", "TH" })
            if (t.EndsWith(suffix)) { t = t.Substring(0, t.Length - suffix.Length); break; }
        if (int.TryParse(t, out var n) && n is >= 1 and <= 12)
            return (GradeLevel)Enum.Parse(typeof(GradeLevel), "G" + n);
        return null;
    }
}
