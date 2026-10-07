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
/// <c>FailureResult("...")</c> call lands in <c>Services/Implementations</c>, before it ships
/// unlocalized. Allowlist: none — every current file in that folder is clean (see the 4-file sweep in
/// this phase); a future legitimate need (there should not be one — route through a resx key instead)
/// would have to edit this test, which is itself the point: it must never pass silently.
///
/// <para><b>Why source-text, not just string.Contains:</b> a literal split across lines
/// (<c>FailureResult(</c> on one line, <c>"text"</c> on the next) or built via string concatenation of a
/// literal (<c>FailureResult("a" + "b")</c>) would slip past a naive single-line regex. This strips
/// comments and collapses whitespace first, so a literal immediately following the call's opening paren is
/// caught regardless of line breaks or formatting — a const/field/variable argument
/// (<c>FailureResult(SomeConstant)</c>) is NOT flagged, since that is exactly the pattern the real fix
/// uses (a <c>LocalizedString</c>-typed property backed by the resx).</para>
/// </summary>
public sealed class NoLiteralFailureResultMessagesTests
{
    [Fact]
    public void Implementations_ContainNoInlineStringLiteral_PassedToFailureResult()
    {
        var implementationsDir = FindImplementationsDirectory();
        var offenders = new List<string>();

        foreach (var path in Directory.EnumerateFiles(implementationsDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var source = StripComments(File.ReadAllText(path));
            foreach (var column in FindLiteralFailureResultColumns(source))
            {
                var lineNumber = source.Take(column).Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetFileName(path)}:{lineNumber}");
            }
        }

        Assert.True(offenders.Count == 0,
            "Found literal FailureResult(\"...\") message(s) in Services/Implementations — route through " +
            "Messages.resx + IStringLocalizer<Messages> instead (see ParentPrepQuestionService/IepDraftService/" +
            "UserService/DocumentCompletenessService for the pattern). Offending locations: " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// Every index in <paramref name="source"/> immediately after a <c>FailureResult(</c> call's opening
    /// parenthesis where the next non-whitespace character starts a C# string literal (<c>"</c> or the
    /// start of an interpolated/verbatim string: <c>$"</c>, <c>@"</c>, <c>$@"</c>/<c>@$"</c>).
    /// </summary>
    private static IEnumerable<int> FindLiteralFailureResultColumns(string source)
    {
        const string marker = "FailureResult(";
        var index = 0;
        while (true)
        {
            var found = source.IndexOf(marker, index, StringComparison.Ordinal);
            if (found < 0) yield break;

            var argStart = found + marker.Length;
            var i = argStart;
            while (i < source.Length && char.IsWhiteSpace(source[i])) i++;

            if (i < source.Length && IsStringLiteralStart(source, i))
                yield return found;

            index = argStart;
        }
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

    /// <summary>Blanks out //-line and /* */-block comments (preserving newlines/positions so line numbers
    /// stay correct) without touching string/char literal content — adequate for this mechanical scan;
    /// not a full C# tokenizer.</summary>
    private static string StripComments(string source)
    {
        var sb = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];

            if (c == '"' || c == '\'')
            {
                var quote = c;
                sb.Append(c);
                i++;
                while (i < source.Length)
                {
                    sb.Append(source[i]);
                    if (source[i] == '\\' && i + 1 < source.Length) { i++; sb.Append(source[i]); }
                    else if (source[i] == quote) { i++; break; }
                    i++;
                }
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
