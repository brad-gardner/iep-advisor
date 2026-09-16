using System.Text.RegularExpressions;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// The one place document/parent text is made safe to sit inside a prompt data tag. Every AI prompt
/// builder (<see cref="DocumentAssistService"/>, <see cref="DraftPromptBuilder"/>) shares these so a
/// hardening change lands everywhere at once instead of drifting between copies.
/// </summary>
public static class PromptText
{
    private static readonly Regex TagStripper = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>Collapses a value onto a single physical line — content can never open a new bracketed line.</summary>
    public static string OneLine(string value) => value.Replace("\r", " ").Replace("\n", " ");

    public static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(empty)";
        value = value.Trim();
        return value.Length <= max ? value : value[..max] + "…";
    }

    /// <summary>
    /// Every value placed inside a data tag (<c>&lt;draft&gt;</c>, <c>&lt;notes&gt;</c>, <c>&lt;field&gt;</c>,
    /// <c>&lt;document&gt;</c>, …) passes through here: tag delimiters are entity-encoded so the text can never
    /// close the tag and escape the data-not-instructions guard. Content is otherwise preserved.
    /// </summary>
    public static string Data(string? value) => value == null ? string.Empty : value.Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>Rich-text field HTML → plain text with paragraph/line breaks preserved as newlines.</summary>
    public static string StripHtml(string html)
    {
        var text = TagStripper.Replace(html.Replace("</p>", "\n").Replace("<br>", "\n").Replace("<br/>", "\n"), string.Empty);
        return System.Net.WebUtility.HtmlDecode(text).Trim();
    }
}
