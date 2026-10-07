using System.Text;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 7, "Success Metrics": "0 literal <c>FailureResult</c> messages in
/// <c>Services/Implementations</c>". A service that returns an inline English string literal as a
/// <see cref="Services.Models.ServiceResult"/>/<see cref="Services.Models.ServiceResult{T}"/> failure
/// message bypasses <c>Messages.resx</c>/<c>IStringLocalizer</c> entirely — it can never be translated,
/// and it is exactly the class of bug the <c>ParentPrepQuestionService</c>/<c>IepDraftService</c>/
/// <c>UserService</c>/<c>DocumentCompletenessService</c> sweep (this same phase) fixed. This is a
/// mechanical, textual guard (not a Roslyn/semantic check): it fails the build the moment a NEW inline
/// literal message lands in <c>Services/Implementations</c>, before it ships unlocalized. Allowlist: none
/// — every current file in that folder is clean; a future legitimate need (there should not be one — route
/// through a resx key instead) would have to edit this test, which is itself the point: it must never pass
/// silently.
///
/// <para><b>Review fix (2026-10-07):</b> originally only checked whether the FIRST argument immediately
/// after <c>FailureResult(</c>'s opening paren was a literal (missing e.g.
/// <c>FailureResult(ServiceErrorKind.NotFound, "Not found.")</c>, where the literal is the SECOND
/// argument), and only checked the <c>FailureResult</c> call itself — not the <c>ServiceResult</c>/
/// <c>ServiceResult&lt;T&gt;</c> shorthand helpers (<see cref="Services.Models.ServiceResult.NotFound"/>,
/// <see cref="Services.Models.ServiceResult.Forbidden"/>, <see cref="Services.Models.ServiceResult.Conflict"/>,
/// <see cref="Services.Models.ServiceResult.PaymentRequired"/>) that wrap it with a fixed
/// <see cref="Services.Models.ServiceErrorKind"/>. Now flags a literal (or interpolated string) as ANY
/// top-level argument of any of those five call forms.</para>
///
/// <para><b>Why source-text, not just string.Contains:</b> a literal split across lines
/// (<c>FailureResult(</c> on one line, <c>"text"</c> on the next) or built via string concatenation of a
/// literal (<c>FailureResult("a" + "b")</c>) would slip past a naive single-line regex. This strips
/// comments and collapses whitespace first, so a literal anywhere in the call's argument list is caught
/// regardless of line breaks or formatting — a const/field/variable/indexer argument
/// (<c>FailureResult(SomeConstant)</c>, <c>FailureResult(ServiceErrorKind.NotFound, _localizer["Key"])</c>)
/// is NOT flagged, since that is exactly the pattern the real fix uses (a resx-backed
/// <c>LocalizedString</c>).</para>
/// </summary>
public sealed class NoLiteralFailureResultMessagesTests
{
    /// <summary>The call forms scanned: the general factory plus every <c>ServiceErrorKind</c>-shorthand
    /// static helper <see cref="Services.Models.ServiceResult"/>/<see cref="Services.Models.ServiceResult{T}"/>
    /// expose (verified against ServiceResult.cs — there is no <c>Unprocessable</c> or <c>Validation</c>
    /// shorthand helper today; a literal passed to <c>FailureResult(ServiceErrorKind.Validation, ...)</c>
    /// is still caught by the general <c>FailureResult(</c> marker).</summary>
    private static readonly string[] CallMarkers =
    {
        "FailureResult(", "NotFound(", "Forbidden(", "Conflict(", "PaymentRequired("
    };

    [Fact]
    public void Implementations_ContainNoInlineStringLiteral_PassedToFailureResultOrItsShorthands()
    {
        var implementationsDir = FindImplementationsDirectory();
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(implementationsDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var source = StripComments(File.ReadAllText(path));
            foreach (var column in FindLiteralArgumentColumns(source))
            {
                var lineNumber = source.Take(column).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(path)}:{lineNumber}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Found literal failure-message argument(s) (FailureResult/NotFound/Forbidden/Conflict/" +
            "PaymentRequired) in Services/Implementations — route through Messages.resx + " +
            "IStringLocalizer<Messages> instead (see ParentPrepQuestionService/IepDraftService/UserService/" +
            "DocumentCompletenessService for the pattern). Offending locations: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// Every index in <paramref name="source"/> at the start of a call to one of <see cref="CallMarkers"/>
    /// where ANY top-level argument (comma-separated, respecting nested parens/brackets/braces and string
    /// literals) trims to a C# string literal (<c>"</c>) or the start of an interpolated/verbatim string
    /// (<c>$"</c>, <c>@"</c>, <c>$@"</c>/<c>@$"</c>).
    /// </summary>
    private static IEnumerable<int> FindLiteralArgumentColumns(string source)
    {
        foreach (var marker in CallMarkers)
        {
            var index = 0;
            while (true)
            {
                var found = source.IndexOf(marker, index, StringComparison.Ordinal);
                if (found < 0) break;

                var argStart = found + marker.Length;
                if (AnyTopLevelArgumentIsStringLiteral(source, argStart))
                    yield return found;

                index = argStart;
            }
        }
    }

    /// <summary>
    /// Walks the argument list starting right after a call's opening paren (<paramref name="argStart"/>),
    /// splitting on top-level commas (depth 0 for (), [], {}) and skipping over string/char literal
    /// content so a comma or paren inside one never confuses the split. Returns true as soon as any
    /// argument segment trims to a string-literal start.
    /// </summary>
    private static bool AnyTopLevelArgumentIsStringLiteral(string source, int argStart)
    {
        var depth = 0;
        var segmentStart = argStart;
        var i = argStart;

        while (i < source.Length)
        {
            var c = source[i];

            if (c == '"' || c == '\'')
            {
                i = SkipStringLiteral(source, i);
                continue;
            }

            if (c is '(' or '[' or '{')
            {
                depth++;
                i++;
                continue;
            }

            if (c is ')' or ']' or '}')
            {
                if (depth == 0)
                {
                    // The call's own closing paren (or a stray bracket — doesn't matter which, since a
                    // mismatched ]/} here would mean the source wasn't valid C# to begin with).
                    return IsLiteralArgument(source, segmentStart, i);
                }
                depth--;
                i++;
                continue;
            }

            if (c == ',' && depth == 0)
            {
                if (IsLiteralArgument(source, segmentStart, i)) return true;
                segmentStart = i + 1;
                i++;
                continue;
            }

            i++;
        }

        // Unterminated call — shouldn't happen in valid, comment-stripped C#; check whatever remains.
        return IsLiteralArgument(source, segmentStart, i);
    }

    private static bool IsLiteralArgument(string source, int start, int end)
    {
        var i = start;
        while (i < end && char.IsWhiteSpace(source[i])) i++;
        return i < end && IsStringLiteralStart(source, i);
    }

    private static bool IsStringLiteralStart(string source, int i)
    {
        if (source[i] == '"') return true;
        if (source[i] is '$' or '@')
        {
            // Skip up to one more prefix character ($@ or @$), then require a quote.
            var j = i + 1;
            if (j < source.Length && source[j] is '$' or '@') j++;
            return j < source.Length && source[j] == '"';
        }
        return false;
    }

    /// <summary>
    /// Returns the index just past the string/char literal starting at <paramref name="i"/>
    /// (<c>source[i]</c> is the opening quote). Handles a verbatim string (<c>@"..."</c>, including
    /// <c>$@"</c>/<c>@$"</c>) correctly: inside one, <c>\</c> is an ordinary character (never an escape)
    /// and <c>""</c> is an embedded literal quote that does NOT end the string — getting this wrong is
    /// exactly how a naive scanner desyncs on a verbatim string containing a backslash or an escaped quote
    /// (e.g. <c>@"C:\Users\""</c>) and starts misreading everything after it as code.
    /// </summary>
    private static int SkipStringLiteral(string source, int i)
    {
        var quote = source[i];
        var verbatim = quote == '"' && IsVerbatimStringStart(source, i);
        i++;

        while (i < source.Length)
        {
            if (!verbatim && source[i] == '\\' && i + 1 < source.Length) { i += 2; continue; }

            if (source[i] == quote)
            {
                if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { i += 2; continue; }
                return i + 1;
            }

            i++;
        }

        return i; // Unterminated — return end of source rather than loop forever.
    }

    /// <summary>True when the <c>"</c> at <paramref name="quoteIndex"/> opens a verbatim string: preceded
    /// by <c>@</c> directly, or by <c>@$</c> (two characters back) for the <c>@$"</c> interpolated form —
    /// the <c>$@"</c> form is already covered by the direct <c>@</c> check.</summary>
    private static bool IsVerbatimStringStart(string source, int quoteIndex)
    {
        var j = quoteIndex - 1;
        if (j < 0) return false;
        if (source[j] == '@') return true;
        return source[j] == '$' && j - 1 >= 0 && source[j - 1] == '@';
    }

    /// <summary>Blanks out //-line and /* */-block comments (preserving newlines/positions so line numbers
    /// stay correct) without touching string/char literal content — including verbatim strings, via the
    /// same <see cref="SkipStringLiteral"/> used for argument parsing, so the two never disagree about
    /// where a string ends. Adequate for this mechanical scan; not a full C# tokenizer.</summary>
    private static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];

            if (c == '"' || c == '\'')
            {
                var end = SkipStringLiteral(source, i);
                sb.Append(source, i, end - i);
                i = end;
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') { sb.Append(' '); i++; }
                continue;
            }

            if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                sb.Append("  ");
                i += 2;
                while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/'))
                {
                    sb.Append(source[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                if (i < source.Length) { sb.Append("  "); i += 2; }
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Walks up from the test assembly's own output directory to find
    /// <c>IepAssistant.Services/Implementations</c> — robust to Debug/Release and target-framework
    /// directory depth, rather than a fixed count of "..".
    /// </summary>
    private static string FindImplementationsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "IepAssistant.Services", "Implementations");
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate IepAssistant.Services/Implementations by walking up from {AppContext.BaseDirectory}");
    }
}
