using System.Text.Json;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Small builder shared by the template seeders. Produces the entity graph (version → sections →
/// fields) for a declaratively described template so a seed is one atomic <c>SaveChanges</c>, and
/// serializes the per-type ConfigJson shapes (with optional semantic tags) consistently.
/// </summary>
public static class TemplateGraphBuilder
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = TemplateFieldConfigValidator.JsonOptions;

    /// <summary>A field declaration inside a section.</summary>
    public sealed record FieldSpec(Guid FieldKey, FieldType Type, string Label, string? ConfigJson, bool Required = false);

    /// <summary>A section declaration with its ordered fields.</summary>
    public sealed record SectionSpec(Guid SectionKey, string Title, IReadOnlyList<FieldSpec> Fields);

    /// <summary>Builds a Published version with the given sections attached (not yet tracked).</summary>
    public static DocumentTemplateVersion BuildPublishedVersion(int versionNumber, IReadOnlyList<SectionSpec> sections, DateTime now)
    {
        var version = new DocumentTemplateVersion
        {
            VersionNumber = versionNumber,
            Status = TemplateVersionStatus.Published,
            PublishedAt = now,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = now,
            UpdatedAt = now
        };

        var sectionOrder = 0;
        foreach (var spec in sections)
        {
            var section = new TemplateSection
            {
                SectionKey = spec.SectionKey,
                Title = spec.Title,
                DisplayOrder = sectionOrder++,
                CreatedAt = now,
                UpdatedAt = now
            };
            var fieldOrder = 0;
            foreach (var f in spec.Fields)
            {
                section.Fields.Add(new TemplateField
                {
                    Version = version,
                    FieldKey = f.FieldKey,
                    FieldType = f.Type,
                    Label = f.Label,
                    Required = f.Required,
                    ConfigJson = f.ConfigJson,
                    DisplayOrder = fieldOrder++,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            version.Sections.Add(section);
        }
        return version;
    }

    public static string RichTextConfig(string? semantic)
        => semantic == null ? "{}" : JsonSerializer.Serialize(new RichTextFieldConfig { Semantic = semantic }, ConfigJsonOptions);

    public static string TextConfig(string? semantic, int? maxLength = null)
        => JsonSerializer.Serialize(new TextFieldConfig { Semantic = semantic, MaxLength = maxLength }, ConfigJsonOptions);

    public static string DateConfig(string? semantic)
        => JsonSerializer.Serialize(new DateFieldConfig { Semantic = semantic }, ConfigJsonOptions);

    public static string CheckboxConfig(string? semantic)
        => semantic == null ? "{}" : JsonSerializer.Serialize(new CheckboxFieldConfig { Semantic = semantic }, ConfigJsonOptions);

    public static string SelectConfig(string? semantic, params string[] options)
        => JsonSerializer.Serialize(new SelectFieldConfig
        {
            Semantic = semantic,
            Options = options.Select(o => new SelectOption { Value = o }).ToList()
        }, ConfigJsonOptions);

    /// <summary>Serializes a Table ConfigJson (semantic + columns; no row bounds so partial drafts save).</summary>
    public static string TableConfig(string? semantic, params (Guid ColumnKey, FieldType Type, string Label, string? Semantic)[] columns)
    {
        var config = new TableFieldConfig
        {
            Semantic = semantic,
            Columns = columns
                .Select(c => new TableColumn { ColumnKey = c.ColumnKey, Type = c.Type, Label = c.Label, Required = false, Semantic = c.Semantic })
                .ToList()
        };
        return JsonSerializer.Serialize(config, ConfigJsonOptions);
    }

    /// <summary>
    /// Deterministic, human-readable GUID for a seeded key. <paramref name="template"/> identifies the
    /// template (e.g. 0x0A for OH IEP), and section/field/column identify the node. Stable forever —
    /// instance values are keyed by these.
    /// </summary>
    public static Guid Key(byte template, byte section, byte field = 0, byte column = 0)
        => new($"{template:x2}ee0000-0000-0000-0000-{section:x2}{field:x2}{column:x2}000000");
}
