using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>
/// Strongly-typed shapes for the per-<see cref="FieldType"/> <c>ConfigJson</c> stored on a
/// <see cref="TemplateField"/> (State Document Template Engine). The raw JSON is validated by
/// <see cref="TemplateFieldConfigValidator"/> on save and again at publish. Types that need no
/// configuration (RichText, Checkbox) have no shape — their ConfigJson is expected to be null/empty.
/// </summary>
public static class TemplateFieldConfigShapes
{
    // Marker namespace holder; the concrete records live below.
}

/// <summary>Text field config. <see cref="MaxLength"/> is optional; when present it must be &gt;= 0.</summary>
public sealed record TextFieldConfig
{
    public int? MaxLength { get; init; }
}

/// <summary>Date field config. <see cref="Format"/> is an optional .NET date format string; when present it must be valid.</summary>
public sealed record DateFieldConfig
{
    public string? Format { get; init; }
}

/// <summary>Select field config. Requires at least one option; option <see cref="SelectOption.Value"/>s must be non-empty and unique.</summary>
public sealed record SelectFieldConfig
{
    public List<SelectOption> Options { get; init; } = new();
}

public sealed record SelectOption
{
    public string Value { get; init; } = string.Empty;
    /// <summary>Optional display label; falls back to <see cref="Value"/> when blank.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Table (repeating-group) field config. Requires at least one column; each column is a typed
/// sub-field of a non-Table, non-RichText type. Optional row bounds must satisfy
/// 0 &lt;= <see cref="MinRows"/> &lt;= <see cref="MaxRows"/>.
/// </summary>
public sealed record TableFieldConfig
{
    public List<TableColumn> Columns { get; init; } = new();
    public int? MinRows { get; init; }
    public int? MaxRows { get; init; }
}

/// <summary>
/// A single column of a <see cref="TableFieldConfig"/> — itself a typed field keyed by a stable
/// <see cref="ColumnKey"/> (values within a table row are keyed by it). <see cref="Type"/> must be a
/// non-Table, non-RichText <see cref="FieldType"/>; <see cref="ConfigJson"/> holds that column type's
/// own config (e.g. a Select column's options).
/// </summary>
public sealed record TableColumn
{
    public Guid ColumnKey { get; init; }
    public FieldType Type { get; init; }
    public string Label { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string? ConfigJson { get; init; }
}
