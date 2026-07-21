using System.Text.Json;
using System.Text.Json.Serialization;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Validates a <see cref="TemplateField"/>'s per-<see cref="FieldType"/> <c>ConfigJson</c> (State
/// Document Template Engine). Runs on every save of a field and again for every field at publish, so
/// a Draft can never be published with a structurally invalid field. Returns a friendly, field-facing
/// message (null when valid); callers prefix it with the field's label for a field-level error.
/// </summary>
public static class TemplateFieldConfigValidator
{
    /// <summary>Shared options: enums as strings (mirrors JsonStringEnumConverter), case-insensitive.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Column types permitted inside a Table (Table-in-Table and RichText-in-Table are forbidden).</summary>
    private static readonly HashSet<FieldType> AllowedTableColumnTypes = new()
    {
        FieldType.Text, FieldType.Date, FieldType.Select, FieldType.Checkbox
    };

    /// <summary>Validates a top-level field's config. Returns null when valid, else a friendly error.</summary>
    public static string? Validate(FieldType type, string? configJson) => type switch
    {
        FieldType.Text => ValidateText(configJson),
        FieldType.Date => ValidateDate(configJson),
        FieldType.Select => ValidateSelect(configJson),
        FieldType.Table => ValidateTable(configJson),
        // No configuration required; any provided config is ignored.
        FieldType.RichText or FieldType.Checkbox => null,
        _ => $"Unsupported field type '{type}'."
    };

    private static bool IsBlank(string? s) => string.IsNullOrWhiteSpace(s);

    private static string? ValidateText(string? configJson)
    {
        if (IsBlank(configJson)) return null; // config optional
        return TryParse<TextFieldConfig>(configJson, out var cfg, out var error)
            ? (cfg!.MaxLength is < 0 ? "Max length must be 0 or greater." : null)
            : error;
    }

    private static string? ValidateDate(string? configJson)
    {
        if (IsBlank(configJson)) return null; // config optional
        if (!TryParse<DateFieldConfig>(configJson, out var cfg, out var error)) return error;
        if (IsBlank(cfg!.Format)) return null;

        // A format string is valid if it round-trips a formatting call without throwing.
        try
        {
            _ = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc)
                .ToString(cfg.Format, System.Globalization.CultureInfo.InvariantCulture);
            return null;
        }
        catch (FormatException)
        {
            return $"'{cfg.Format}' is not a valid date format.";
        }
    }

    private static string? ValidateSelect(string? configJson)
    {
        if (IsBlank(configJson)) return "A dropdown must have at least one option.";
        if (!TryParse<SelectFieldConfig>(configJson, out var cfg, out var error)) return error;
        return ValidateSelectConfig(cfg!);
    }

    private static string? ValidateSelectConfig(SelectFieldConfig cfg)
    {
        if (cfg.Options.Count == 0)
            return "A dropdown must have at least one option.";
        if (cfg.Options.Any(o => IsBlank(o.Value)))
            return "Dropdown option values cannot be blank.";

        var values = cfg.Options.Select(o => o.Value).ToList();
        if (values.Count != values.Distinct().Count())
            return "Dropdown option values must be unique.";

        return null;
    }

    private static string? ValidateTable(string? configJson)
    {
        if (IsBlank(configJson)) return "A table must have at least one column.";
        if (!TryParse<TableFieldConfig>(configJson, out var cfg, out var error)) return error;

        if (cfg!.Columns.Count == 0)
            return "A table must have at least one column.";

        // ColumnKey is the stable identity table-row values are keyed by (like FieldKey for a field),
        // so it must be present and unique within the table.
        if (cfg.Columns.Any(c => c.ColumnKey == Guid.Empty))
            return "Every table column must have a stable key.";
        var columnKeys = cfg.Columns.Select(c => c.ColumnKey).ToList();
        if (columnKeys.Count != columnKeys.Distinct().Count())
            return "Table column keys must be unique.";

        foreach (var column in cfg.Columns)
        {
            if (IsBlank(column.Label))
                return "Every table column must have a label.";

            if (column.Type == FieldType.Table)
                return $"Table column '{column.Label}' cannot itself be a table.";
            if (column.Type == FieldType.RichText)
                return $"Table column '{column.Label}' cannot be rich text.";
            if (!AllowedTableColumnTypes.Contains(column.Type))
                return $"Table column '{column.Label}' has an unsupported type.";

            // Recurse into the column's own config (e.g. a Select column needs options).
            var columnError = Validate(column.Type, column.ConfigJson);
            if (columnError != null)
                return $"Table column '{column.Label}': {columnError}";
        }

        if (cfg.MinRows is < 0 || cfg.MaxRows is < 0)
            return "Table row counts must be 0 or greater.";
        if (cfg.MinRows is int min && cfg.MaxRows is int max && min > max)
            return "Table minimum rows cannot exceed maximum rows.";

        return null;
    }

    private static bool TryParse<T>(string? json, out T? value, out string? error)
    {
        value = default;
        error = null;
        try
        {
            value = JsonSerializer.Deserialize<T>(json!, JsonOptions);
            if (value == null)
            {
                error = "The field configuration is invalid.";
                return false;
            }
            return true;
        }
        catch (JsonException)
        {
            error = "The field configuration is not valid JSON.";
            return false;
        }
    }
}
