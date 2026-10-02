using System.Text.Json;

namespace IepAssistant.Services.Models;

/// <summary>
/// The shared rule for "this one document's own red flags" out of an <c>AnalysisRun</c>, used by every
/// consumer that used to read a single document's <c>OverallRedFlags</c> off the retired per-document
/// IepAnalysis row (<see cref="Implementations.IepComparisonService"/>, <see cref="Implementations.AdvocateToolset"/>):
/// <list type="bullet">
/// <item>the run's own <c>OverallRedFlags</c> ONLY when the run has exactly one source — a single-source
/// run promotes that source's own red flags to the run level (see <c>AnalysisRunService.ExecuteRunAsync</c>),
/// so they are still document-specific; a multi-source run's <c>OverallRedFlags</c> is the cross-document
/// synthesis view and is not specific to any one document, so it is excluded;</item>
/// <item>unioned with the red flags embedded in each of that source's own ordinary (non-<c>iep_goals</c>)
/// sections, which are always generated per-source and so are always specific to this document, regardless
/// of how many sources the run has.</item>
/// </list>
/// </summary>
public static class AnalysisRunRedFlagRollup
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <param name="runOverallRedFlagsJson">The run's <c>OverallRedFlags</c> JSON column.</param>
    /// <param name="sourceCount">How many sources the run has in total.</param>
    /// <param name="ordinarySectionAnalysisJson">The <c>Analysis</c> JSON of each of this source's
    /// ordinary (non-<c>iep_goals</c>) sections.</param>
    public static List<RedFlag> Combine(
        string? runOverallRedFlagsJson, int sourceCount, IEnumerable<string?> ordinarySectionAnalysisJson)
    {
        var result = new List<RedFlag>();

        if (sourceCount == 1)
            result.AddRange(DeserializeRedFlags(runOverallRedFlagsJson));

        foreach (var json in ordinarySectionAnalysisJson)
        {
            var section = DeserializeSection(json);
            if (section?.RedFlags is { Count: > 0 })
                result.AddRange(section.RedFlags);
        }

        return result;
    }

    private static List<RedFlag> DeserializeRedFlags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<RedFlag>>(json, CaseInsensitiveOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static AnalysisRunSectionResult? DeserializeSection(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<AnalysisRunSectionResult>(json, CaseInsensitiveOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
