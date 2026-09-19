using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// The Virtual Advocate's read-only tools, closed over one (child, parent) pair for one turn. This class is
/// the whole boundary between the model and the family's record: every tool re-scopes its query to
/// <c>_childId</c>, wraps every free-text field with <see cref="PromptText.Data"/>, records each returned
/// <c>sourceRef</c> in <see cref="ReturnedRefs"/> (the citation allow-list), and caps its output — per call
/// (<see cref="PerToolCharCap"/>) and per turn (<see cref="PerTurnCharBudget"/>). Nothing staff-only has a
/// code path in.
/// </summary>
/// <remarks>
/// Adding a tool = one definition + one handler registered in the constructor. Unknown tools and inputs that
/// fail their schema throw <see cref="ToolExecutionException"/> (the model sees <c>is_error</c> and can
/// recover); any other failure inside a handler is logged and rethrown as a generic
/// <see cref="ToolExecutionException"/> so one bad lookup cannot kill the turn.
/// </remarks>
public sealed class AdvocateToolset : IToolExecutor
{
    public const int PerToolCharCap = 6_000;
    public const int PerTurnCharBudget = 30_000;
    public const int MaxKnowledgeBaseEntries = 8;
    public const int MaxSummaryChars = 600;
    private const int MaxStringInputLength = 300;

    private delegate Task<ToolPayload> ToolHandler(JsonElement input, CancellationToken ct);

    private sealed record ToolEntry(ClaudeToolDefinition Definition, ToolHandler Handler);

    /// <summary>A handler's result: the payload plus (optionally) the array to drop items from if it is over the per-tool cap.</summary>
    private sealed record ToolPayload(JsonObject Payload, JsonArray? Trimmable = null);

    private readonly ApplicationDbContext _context;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly ILogger _logger;
    private readonly int _childId;
    private readonly int _userId;
    private readonly string? _stateCode;
    private readonly Dictionary<string, ToolEntry> _tools;
    private int _remainingChars = PerTurnCharBudget;

    public AdvocateToolset(ApplicationDbContext context, IKnowledgeBaseService knowledgeBase, int childId, int userId, string? stateCode, ILogger? logger = null)
    {
        _context = context;
        _knowledgeBase = knowledgeBase;
        _childId = childId;
        _userId = userId;
        _stateCode = stateCode;
        _logger = logger ?? NullLogger.Instance;

        // Registration order is the order the model sees the tools in — keep it stable.
        var registry = new List<ToolEntry>
        {
            new(SearchKnowledgeBaseDefinition, SearchKnowledgeBaseAsync),
            new(GetChildSummaryDefinition, GetChildSummaryAsync)
        };
        _tools = registry.ToDictionary(t => t.Definition.Name, StringComparer.Ordinal);
        Definitions = registry.Select(t => t.Definition).ToList();
    }

    /// <summary>Tool definitions in registration order — stable so the cached tool block does not churn.</summary>
    public IReadOnlyList<ClaudeToolDefinition> Definitions { get; }

    /// <summary>Every <c>sourceRef</c> (<c>kind:id</c>) a tool returned this turn — the only refs the answer may cite.</summary>
    public HashSet<string> ReturnedRefs { get; } = new(StringComparer.Ordinal);

    /// <summary>Human labels for returned refs (KB titles, the child's first name), for citation chips.</summary>
    public Dictionary<string, string> Labels { get; } = new(StringComparer.Ordinal);

    public async Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            throw new ToolExecutionException($"Unknown tool '{PromptText.Data(PromptText.Truncate(toolName, 60))}'.");

        ToolInputValidator.Validate(input, tool.Definition.InputSchema, MaxStringInputLength);

        if (_remainingChars <= 0)
            return new JsonObject { ["truncated"] = true, ["reason"] = "budget" }.ToJsonString();

        ToolPayload result;
        try
        {
            result = await tool.Handler(input, cancellationToken);
        }
        catch (ToolExecutionException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advocate tool {Tool} failed for child {ChildId}", toolName, _childId);
            throw new ToolExecutionException("This lookup failed.");
        }

        var json = FitToCap(result);
        _remainingChars -= json.Length;
        return json;
    }

    // ------------------------------------------------------------------ search_knowledge_base

    private static readonly ClaudeToolDefinition SearchKnowledgeBaseDefinition = new(
        "search_knowledge_base",
        "Search the plain-language knowledge base of special-education rights, processes, IEP/ETR provisions and " +
        "glossary terms. Returns federal (IDEA) entries plus entries for the child's state. Use it before " +
        "explaining a right, a timeline or a process. Each entry has a sourceRef to cite.",
        ToolSchema.Object(
            ("query", "string", "Keywords or a short phrase, e.g. \"prior written notice\" or \"evaluation timeline\".", true),
            ("category", "string", "Optional category filter: rights, process, provisions or glossary.", false)));

    private async Task<ToolPayload> SearchKnowledgeBaseAsync(JsonElement input, CancellationToken ct)
    {
        var query = input.GetProperty("query").GetString()!.Trim();
        if (query.Length == 0)
            throw new ToolExecutionException("query must not be empty.");
        var category = input.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()!.Trim() : null;
        if (string.IsNullOrEmpty(category)) category = null;

        var results = await _knowledgeBase.SearchAsync(query, category, _stateCode, ct);
        // Defence in depth: the service leaves state unfiltered when no state is given; the advocate only
        // ever sees federal entries plus the child's own state.
        var entries = results
            .Where(e => e.State == null || (_stateCode != null && string.Equals(e.State, _stateCode, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxKnowledgeBaseEntries)
            .ToList();

        var array = new JsonArray();
        foreach (var e in entries)
        {
            var sourceRef = $"kb:{e.Id}";
            ReturnedRefs.Add(sourceRef);
            Labels[sourceRef] = e.Title;
            array.Add(new JsonObject
            {
                ["sourceRef"] = sourceRef,
                ["title"] = PromptText.Data(e.Title),
                ["category"] = PromptText.Data(e.Category),
                ["legalReference"] = e.LegalReference == null ? null : PromptText.Data(e.LegalReference),
                ["state"] = e.State == null ? "federal" : PromptText.Data(e.State),
                ["summary"] = PromptText.Data(PromptText.Truncate(e.Content, MaxSummaryChars))
            });
        }

        var payload = new JsonObject
        {
            ["state"] = _stateCode == null ? "unknown" : PromptText.Data(_stateCode),
            ["entries"] = array
        };
        return new ToolPayload(payload, array);
    }

    // ------------------------------------------------------------------ get_child_summary

    private static readonly ClaudeToolDefinition GetChildSummaryDefinition = new(
        "get_child_summary",
        "Read the child's profile: first name, grade, disability category, school district, state, how many " +
        "IEPs, ETRs, progress reports and journal entries are on file, and the next scheduled meeting date.",
        ToolSchema.Object());

    private async Task<ToolPayload> GetChildSummaryAsync(JsonElement input, CancellationToken ct)
    {
        var child = await _context.ChildProfiles.AsNoTracking()
            .Where(c => c.Id == _childId)
            .Select(c => new { c.Id, c.FirstName, c.GradeLevel, c.DisabilityCategory, c.SchoolDistrict })
            .FirstOrDefaultAsync(ct);
        if (child == null)
            throw new ToolExecutionException("Not found.");

        var now = DateTime.UtcNow;
        var ieps = await _context.IepDocuments.CountAsync(d => d.ChildProfileId == _childId, ct);
        var etrs = await _context.EtrDocuments.CountAsync(d => d.ChildProfileId == _childId, ct);
        var progressReports = await _context.ProgressReports.CountAsync(r => r.ChildProfileId == _childId, ct);
        var journalEntries = await _context.JournalEntries.CountAsync(j => j.ChildProfileId == _childId, ct);

        var linkedStudentIds = _context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == _childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId);
        var nextMeeting = await _context.Meetings.AsNoTracking()
            .Where(m => linkedStudentIds.Contains(m.SchoolStudentId) && m.Status == MeetingStatus.Scheduled && m.StartsAtUtc >= now)
            .OrderBy(m => m.StartsAtUtc)
            .Select(m => (DateTime?)m.StartsAtUtc)
            .FirstOrDefaultAsync(ct);

        var sourceRef = $"child:{child.Id}";
        ReturnedRefs.Add(sourceRef);
        Labels[sourceRef] = child.FirstName;

        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["firstName"] = PromptText.Data(child.FirstName),
            ["gradeLevel"] = child.GradeLevel == null ? null : PromptText.Data(child.GradeLevel),
            ["disabilityCategory"] = child.DisabilityCategory == null ? null : PromptText.Data(child.DisabilityCategory),
            ["schoolDistrict"] = child.SchoolDistrict == null ? null : PromptText.Data(child.SchoolDistrict),
            ["state"] = _stateCode == null ? "unknown" : PromptText.Data(_stateCode),
            ["counts"] = new JsonObject
            {
                ["ieps"] = ieps,
                ["etrs"] = etrs,
                ["progressReports"] = progressReports,
                ["journalEntries"] = journalEntries
            },
            ["nextMeetingDate"] = nextMeeting?.ToString("yyyy-MM-dd")
        };
        return new ToolPayload(payload);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Serialises the payload under <see cref="PerToolCharCap"/>: drops trailing items from the trimmable
    /// array first (flagging <c>truncated: "size"</c>), and only if that is not enough hard-cuts the JSON.
    /// </summary>
    private static string FitToCap(ToolPayload result)
    {
        var json = result.Payload.ToJsonString();
        if (json.Length <= PerToolCharCap) return json;

        if (result.Trimmable != null)
        {
            result.Payload["truncated"] = true;
            result.Payload["reason"] = "size";
            while (json.Length > PerToolCharCap && result.Trimmable.Count > 0)
            {
                result.Trimmable.RemoveAt(result.Trimmable.Count - 1);
                json = result.Payload.ToJsonString();
            }
            if (json.Length <= PerToolCharCap) return json;
        }

        return json[..PerToolCharCap];
    }
}

/// <summary>Builds the strict JSON-Schema objects the advocate tools declare (<c>additionalProperties: false</c>, explicit <c>required</c>).</summary>
public static class ToolSchema
{
    public static JsonNode Object(params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, type, description, isRequired) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
            if (isRequired) required.Add(name);
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }
}

/// <summary>
/// Validates model-supplied tool input against the tool's declared schema (object shape, required keys,
/// scalar types, no unknown keys, bounded string length). Throws <see cref="ToolExecutionException"/> so
/// the model sees an <c>is_error</c> result and can retry with corrected input.
/// </summary>
public static class ToolInputValidator
{
    public static void Validate(JsonElement input, JsonNode schema, int maxStringLength)
    {
        if (input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            input = JsonDocument.Parse("{}").RootElement;
        if (input.ValueKind != JsonValueKind.Object)
            throw new ToolExecutionException("Tool input must be a JSON object.");

        var properties = schema["properties"] as JsonObject ?? new JsonObject();
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in schema["required"] as JsonArray ?? new JsonArray())
        {
            if (node?.GetValue<string>() is { } name) required.Add(name);
        }

        foreach (var name in required)
        {
            if (!input.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                throw new ToolExecutionException($"Missing required input '{name}'.");
        }

        foreach (var member in input.EnumerateObject())
        {
            if (!properties.TryGetPropertyValue(member.Name, out var propSchema) || propSchema == null)
                throw new ToolExecutionException($"Unknown input '{PromptText.Data(PromptText.Truncate(member.Name, 60))}'.");
            if (member.Value.ValueKind == JsonValueKind.Null) continue;

            var expected = propSchema["type"]?.GetValue<string>();
            var ok = expected switch
            {
                "string" => member.Value.ValueKind == JsonValueKind.String,
                "integer" => member.Value.ValueKind == JsonValueKind.Number && member.Value.TryGetInt32(out _),
                "number" => member.Value.ValueKind == JsonValueKind.Number,
                "boolean" => member.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => true
            };
            if (!ok)
                throw new ToolExecutionException($"Input '{member.Name}' must be a {expected}.");
            if (member.Value.ValueKind == JsonValueKind.String && member.Value.GetString()!.Length > maxStringLength)
                throw new ToolExecutionException($"Input '{member.Name}' must be {maxStringLength} characters or fewer.");
        }
    }
}
