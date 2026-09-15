using System.Text.Json;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>A template field resolved by its semantic tag, with its Table column semantics (if any).</summary>
public sealed record SemanticField(
    Guid FieldKey,
    FieldType FieldType,
    string Label,
    string SectionTitle,
    string Semantic,
    /// <summary>column semantic → columnKey (Table fields only; empty otherwise).</summary>
    IReadOnlyDictionary<string, Guid> Columns);

/// <summary>
/// Reads the optional <c>semantic</c> tags out of a template version's fields so callers (AI assist,
/// prefill, PDF layout, projections) can locate "the goals table" / "the baseline column" without
/// knowing FieldKey GUIDs. Pure parsing over already-loaded entities — no DbContext.
/// </summary>
public static class TemplateSemanticsReader
{
    private static readonly JsonSerializerOptions Options = TemplateFieldConfigValidator.JsonOptions;

    /// <summary>Returns every semantically-tagged field, keyed by semantic (first occurrence wins).</summary>
    public static IReadOnlyDictionary<string, SemanticField> Read(IEnumerable<TemplateSection> sections)
    {
        var result = new Dictionary<string, SemanticField>(StringComparer.Ordinal);
        foreach (var section in sections.OrderBy(s => s.DisplayOrder))
        {
            foreach (var field in section.Fields.OrderBy(f => f.DisplayOrder))
            {
                var (semantic, columns) = ReadField(field.FieldType, field.ConfigJson);
                if (semantic == null || result.ContainsKey(semantic))
                    continue;
                result[semantic] = new SemanticField(field.FieldKey, field.FieldType, field.Label, section.Title, semantic, columns);
            }
        }
        return result;
    }

    /// <summary>Semantic for one field plus (for Tables) its column semantic map.</summary>
    public static (string? Semantic, IReadOnlyDictionary<string, Guid> Columns) ReadField(FieldType type, string? configJson)
    {
        var empty = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(configJson))
            return (null, empty);
        try
        {
            switch (type)
            {
                case FieldType.Table:
                {
                    var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, Options);
                    if (cfg == null) return (null, empty);
                    var columns = new Dictionary<string, Guid>(StringComparer.Ordinal);
                    foreach (var c in cfg.Columns)
                        if (!string.IsNullOrWhiteSpace(c.Semantic) && !columns.ContainsKey(c.Semantic))
                            columns[c.Semantic] = c.ColumnKey;
                    return (Normalize(cfg.Semantic), columns);
                }
                case FieldType.Text:
                    return (Normalize(JsonSerializer.Deserialize<TextFieldConfig>(configJson, Options)?.Semantic), empty);
                case FieldType.RichText:
                    return (Normalize(JsonSerializer.Deserialize<RichTextFieldConfig>(configJson, Options)?.Semantic), empty);
                case FieldType.Date:
                    return (Normalize(JsonSerializer.Deserialize<DateFieldConfig>(configJson, Options)?.Semantic), empty);
                case FieldType.Select:
                    return (Normalize(JsonSerializer.Deserialize<SelectFieldConfig>(configJson, Options)?.Semantic), empty);
                case FieldType.Checkbox:
                    return (Normalize(JsonSerializer.Deserialize<CheckboxFieldConfig>(configJson, Options)?.Semantic), empty);
                default:
                    return (null, empty);
            }
        }
        catch (JsonException)
        {
            return (null, empty);
        }
    }

    /// <summary>Reads the column semantic map for a Table field's config (empty for anything else).</summary>
    public static IReadOnlyDictionary<string, Guid> ReadColumns(FieldType type, string? configJson)
        => ReadField(type, configJson).Columns;

    /// <summary>Reads the semantic → label map for a Table field's columns (for human-readable renderings).</summary>
    public static IReadOnlyDictionary<Guid, string> ReadColumnLabels(string? configJson)
    {
        var map = new Dictionary<Guid, string>();
        if (string.IsNullOrWhiteSpace(configJson)) return map;
        try
        {
            var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, Options);
            if (cfg == null) return map;
            foreach (var c in cfg.Columns) map[c.ColumnKey] = c.Label;
        }
        catch (JsonException) { }
        return map;
    }

    private static string? Normalize(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
