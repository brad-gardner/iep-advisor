using System.Globalization;
using System.Text.RegularExpressions;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>The stripped answer plus what the trailing contract blocks yielded.</summary>
public sealed record AdvocateParsedAnswer(string Markdown, List<AdvocateCitation> Citations, List<AdvocateSuggestion> Suggestions);

/// <summary>
/// Pulls the <c>&lt;sources&gt;</c> and <c>&lt;suggest&gt;</c> contract blocks out of a Virtual Advocate answer
/// (see <see cref="AdvocatePrompts.System"/>) and returns the markdown without them. Tolerant by design —
/// missing, malformed or unclosed blocks yield nothing and never fail the turn — and strict about provenance:
/// a citation or <c>open_*</c> id survives only if the toolset actually returned that ref this turn.
/// </summary>
public static class AdvocateAnswerParser
{
    public const int MaxSuggestionTextLength = 500;

    private static readonly Regex SourcesBlock = new(@"<sources\s*>(.*?)</sources\s*>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex UnclosedSources = new(@"<sources\s*>(.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SourceRef = new(@"^([a-z_]+):(\d{1,9})$", RegexOptions.Compiled);
    private static readonly Regex RefSeparator = new(@"[;,\s]+", RegexOptions.Compiled);

    private static readonly Regex SuggestBlock = new(@"<suggest\b([^>]*?)(?:/>|>(.*?)</suggest\s*>)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex UnclosedSuggest = new(@"<suggest\b[^>]*>(?:(?!</suggest).)*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex Attribute = new(@"([a-zA-Z_]+)\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    /// <param name="returnedRefs">The toolset's <c>ReturnedRefs</c> — the only refs allowed to survive.</param>
    /// <param name="labels">Optional titles per ref, copied onto citations.</param>
    /// <param name="parents">Optional parent record per ref (the toolset's <c>Parents</c>), copied onto citations so the UI can deep-link them.</param>
    public static AdvocateParsedAnswer Parse(string? text, IReadOnlySet<string> returnedRefs, IReadOnlyDictionary<string, string>? labels = null, IReadOnlyDictionary<string, AdvocateCitationParent>? parents = null)
    {
        if (string.IsNullOrEmpty(text))
            return new AdvocateParsedAnswer(string.Empty, new List<AdvocateCitation>(), new List<AdvocateSuggestion>());

        var citations = new List<AdvocateCitation>();
        var seenRefs = new HashSet<string>(StringComparer.Ordinal);
        var suggestions = new List<AdvocateSuggestion>();

        var stripped = SourcesBlock.Replace(text, m =>
        {
            CollectRefs(m.Groups[1].Value, returnedRefs, labels, parents, citations, seenRefs);
            return string.Empty;
        });
        stripped = UnclosedSources.Replace(stripped, m =>
        {
            CollectRefs(m.Groups[1].Value, returnedRefs, labels, parents, citations, seenRefs);
            return string.Empty;
        });

        stripped = SuggestBlock.Replace(stripped, m =>
        {
            var suggestion = ParseSuggestion(m.Groups[1].Value, m.Groups[2].Success ? m.Groups[2].Value : null, returnedRefs);
            if (suggestion != null) suggestions.Add(suggestion);
            return string.Empty;
        });
        stripped = UnclosedSuggest.Replace(stripped, string.Empty);

        return new AdvocateParsedAnswer(stripped.TrimEnd(), citations, suggestions);
    }

    private static void CollectRefs(string body, IReadOnlySet<string> returnedRefs, IReadOnlyDictionary<string, string>? labels, IReadOnlyDictionary<string, AdvocateCitationParent>? parents, List<AdvocateCitation> citations, HashSet<string> seen)
    {
        foreach (var token in RefSeparator.Split(body))
        {
            var candidate = token.Trim();
            if (candidate.Length == 0) continue;
            var match = SourceRef.Match(candidate);
            if (!match.Success) continue;
            if (!returnedRefs.Contains(candidate) || !seen.Add(candidate)) continue;
            if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)) continue;

            string? label = null;
            labels?.TryGetValue(candidate, out label);
            AdvocateCitationParent? parent = null;
            parents?.TryGetValue(candidate, out parent);
            citations.Add(new AdvocateCitation(match.Groups[1].Value, id, label, parent));
        }
    }

    private static AdvocateSuggestion? ParseSuggestion(string attributeText, string? innerText, IReadOnlySet<string> returnedRefs)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match a in Attribute.Matches(attributeText))
            attributes[a.Groups[1].Value] = a.Groups[2].Value;

        if (!attributes.TryGetValue("kind", out var kind) || !AdvocateSuggestionKinds.All.Contains(kind))
            return null;

        var sourceKind = AdvocateSuggestionKinds.SourceKindFor(kind);
        if (sourceKind != null)
        {
            if (!attributes.TryGetValue("id", out var rawId)
                || !int.TryParse(rawId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || !returnedRefs.Contains($"{sourceKind}:{id}"))
                return null;
            return new AdvocateSuggestion(kind, null, id, null);
        }

        var body = (innerText ?? string.Empty).Trim();
        if (body.Length == 0 || body.Length > MaxSuggestionTextLength) return null;

        string? date = null;
        if (attributes.TryGetValue("date", out var rawDate)
            && DateOnly.TryParseExact(rawDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            date = parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return new AdvocateSuggestion(kind, body, null, date);
    }
}
