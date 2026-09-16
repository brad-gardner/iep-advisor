using System.Text;
using System.Text.Json;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Shared best-effort parser for a Claude JSON-object reply: strips an optional ```json fence, repairs
/// raw line breaks inside string literals (models frequently emit them, which breaks strict JSON), and
/// parses. Extracted from <c>DocumentAssistService.ParseGrounded</c> so every AI JSON-contract caller
/// (document assist citations, draft explanations, draft Q&amp;A) shares the same tolerant behavior and
/// prose-fallback contract: never throws, callers fall back to treating the raw text as prose on a miss.
/// </summary>
public static class TolerantJsonParser
{
    /// <summary>Strips a leading/trailing ```json (or plain ```) fence if present; otherwise a no-op (trimmed).</summary>
    public static string StripFence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
            return text;

        var firstNewline = text.IndexOf('\n');
        if (firstNewline > 0) text = text[(firstNewline + 1)..];
        if (text.EndsWith("```", StringComparison.Ordinal)) text = text[..^3];
        return text.Trim();
    }

    /// <summary>
    /// Escapes raw newline/CR/tab characters that appear INSIDE JSON string literals (left untouched
    /// outside strings) so a document with literal line breaks in a string value — what models actually
    /// emit — still parses. Already-escaped sequences are left alone.
    /// </summary>
    public static string RepairRawNewlines(string json)
    {
        var sb = new StringBuilder(json.Length + 16);
        var inString = false;
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                inString = !inString;
            if (inString && c is '\n' or '\r' or '\t')
            {
                sb.Append(c switch { '\n' => "\\n", '\r' => "\\r", _ => "\\t" });
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Best-effort parse of a Claude JSON-object reply: strips a fence, repairs raw newlines, and
    /// parses. Returns <c>null</c> (never throws) when the text is not a JSON object or fails to parse —
    /// callers fall back to treating the raw reply as prose.
    /// </summary>
    public static JsonDocument? TryParseObject(string raw)
    {
        var text = StripFence(raw);
        if (!text.StartsWith('{'))
            return null;
        try
        {
            return JsonDocument.Parse(RepairRawNewlines(text));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
