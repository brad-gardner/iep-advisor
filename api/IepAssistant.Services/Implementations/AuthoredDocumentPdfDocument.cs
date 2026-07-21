using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// QuestPDF document that renders a finalized <see cref="AuthoredDocumentVersion"/> against its pinned,
/// frozen template version tree (State Document Template Engine, Phase 4). Pure layout — no I/O, no DB
/// access; it receives an already-loaded, already-ordered template tree plus the frozen value-document
/// and composes a PDF. The render service computes the bytes via <c>document.GeneratePdf()</c>.
///
/// <para><b>Determinism (cross-cutting G-d.5):</b> all dates/numbers are formatted with
/// <see cref="CultureInfo.InvariantCulture"/> and the PDF metadata Creation/Modified dates are pinned to
/// the version's <see cref="AuthoredDocumentVersion.FinalizedAt"/> (via <see cref="GetMetadata"/>), so
/// re-rendering the same version yields byte-identical output (identical SHA-256 checksum).</para>
///
/// <para><b>Empty-field / empty-section rules (G-d.1):</b> a field is rendered only when it holds a value;
/// an empty field is omitted (required fields always hold a value post-finalize). A checkbox counts as
/// "empty" only when its key is absent — a present <c>true</c>/<c>false</c> is a definite Yes/No answer
/// and is rendered. A Table is empty when it has no rows. A section that would render zero fields is
/// omitted entirely.</para>
///
/// <para><b>Exhaustiveness (G-d.3):</b> the per-field <c>switch</c> throws on an unhandled
/// <see cref="FieldType"/> so the render is marked Error rather than silently dropping content; the
/// worker never crashes because the render service swallows the throw into a retryable Error state.</para>
/// </summary>
public sealed class AuthoredDocumentPdfDocument : IDocument
{
    private static readonly Regex HtmlTag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly string _documentTypeDisplayName;
    private readonly int _versionNumber;
    private readonly DateTime _finalizedAt;
    private readonly TemplateVersionDetailModel _tree;
    private readonly JsonObject _values;

    public AuthoredDocumentPdfDocument(
        string documentTypeDisplayName, int versionNumber, DateTime finalizedAt,
        TemplateVersionDetailModel tree, string? valuesJson)
    {
        _documentTypeDisplayName = string.IsNullOrWhiteSpace(documentTypeDisplayName) ? "Document" : documentTypeDisplayName;
        _versionNumber = versionNumber;
        _finalizedAt = finalizedAt;
        _tree = tree;

        JsonObject values;
        try
        {
            values = (string.IsNullOrWhiteSpace(valuesJson)
                ? new JsonObject()
                : JsonNode.Parse(valuesJson) as JsonObject) ?? new JsonObject();
        }
        catch (JsonException)
        {
            values = new JsonObject();
        }
        _values = values;
    }

    /// <summary>Pin metadata dates to FinalizedAt so re-rendering the same version is byte-deterministic.</summary>
    public DocumentMetadata GetMetadata()
    {
        var metadata = DocumentMetadata.Default;
        metadata.Title = $"{_documentTypeDisplayName} v{_versionNumber}";
        metadata.CreationDate = _finalizedAt;
        metadata.ModifiedDate = _finalizedAt;
        return metadata;
    }

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(40);
            page.Size(PageSizes.Letter);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text(_documentTypeDisplayName).FontSize(18).Bold();
            col.Item().Text($"Version {_versionNumber}").FontSize(11).SemiBold();
            col.Item().Text($"Finalized: {_finalizedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(col =>
        {
            col.Spacing(14);

            foreach (var section in _tree.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
            {
                var fields = section.Fields
                    .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id)
                    .Where(f => !IsFieldEmpty(f))
                    .ToList();

                // Empty-section rule: omit a section that would render no fields.
                if (fields.Count == 0)
                    continue;

                col.Item().Element(c => SectionHeading(c, section.Title));
                foreach (var field in fields)
                    col.Item().Element(c => ComposeField(c, field));
            }
        });
    }

    // ---------------------------------------------------------------- Fields

    private void ComposeField(IContainer container, TemplateFieldModel field)
    {
        var node = GetValue(field.FieldKey);

        switch (field.FieldType)
        {
            case FieldType.Text:
                LabeledText(container, field.Label, AsString(node) ?? string.Empty);
                break;

            case FieldType.RichText:
                // Render as plain, sanitized text — never execute markup (G-d.3 / defense-in-depth).
                LabeledText(container, field.Label, ToPlainText(AsString(node)));
                break;

            case FieldType.Date:
                LabeledText(container, field.Label, FormatDate(AsString(node)));
                break;

            case FieldType.Select:
                LabeledText(container, field.Label, SelectDisplay(field.ConfigJson, AsString(node)));
                break;

            case FieldType.Checkbox:
                LabeledText(container, field.Label, AsBool(node) == true ? "Yes" : "No");
                break;

            case FieldType.Table:
                ComposeTable(container, field, node as JsonArray);
                break;

            default:
                // Exhaustive switch: an unhandled type marks the render Error rather than dropping content.
                throw new InvalidOperationException($"Unsupported field type '{field.FieldType}' for field '{field.Label}'.");
        }
    }

    private void ComposeTable(IContainer container, TemplateFieldModel field, JsonArray? rows)
    {
        var columns = ParseColumns(field.ConfigJson);

        container.Column(col =>
        {
            col.Item().Text(field.Label).SemiBold();

            // A Table with columns but no rows still shows the header (structure); the field is only
            // reached here when non-empty (has >= 1 row) or has columns, but guard defensively.
            if (columns.Count == 0)
            {
                col.Item().Text("—");
                return;
            }

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    foreach (var _ in columns)
                        def.RelativeColumn();
                });

                // Header repeats on each page (no clipping across page breaks — G-d.2).
                table.Header(header =>
                {
                    foreach (var column in columns)
                        HeaderCell(header, column.Label);
                });

                if (rows != null)
                {
                    foreach (var rowNode in rows)
                    {
                        var row = rowNode as JsonObject;
                        foreach (var column in columns)
                        {
                            JsonNode? cell = null;
                            row?.TryGetPropertyValue(column.ColumnKey.ToString(), out cell);
                            BodyCell(table, FormatCell(column, cell));
                        }
                    }
                }
            });
        });
    }

    // ---------------------------------------------------------------- Emptiness

    private bool IsFieldEmpty(TemplateFieldModel field)
    {
        var node = GetValue(field.FieldKey);
        return field.FieldType switch
        {
            FieldType.Table => (node as JsonArray) is not { Count: > 0 },
            FieldType.Checkbox => AsBool(node) == null, // present true/false is a definite answer
            _ => string.IsNullOrWhiteSpace(AsString(node))
        };
    }

    // ---------------------------------------------------------------- Value + formatting helpers

    private JsonNode? GetValue(Guid fieldKey)
    {
        _values.TryGetPropertyValue(fieldKey.ToString(), out var node);
        return node;
    }

    private static string? AsString(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? AsBool(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static string FormatDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : raw; // fall back to the stored string if somehow unparseable
    }

    private static string SelectDisplay(string? configJson, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var options = ParseSelectOptions(configJson);
        var match = options.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.Ordinal));
        if (match == null)
            return value; // fall back to the raw value
        return string.IsNullOrWhiteSpace(match.Label) ? match.Value : match.Label!;
    }

    private static string FormatCell(TableColumn column, JsonNode? cell)
    {
        return column.Type switch
        {
            FieldType.Date => FormatDate(AsString(cell)),
            FieldType.Select => SelectDisplay(column.ConfigJson, AsString(cell)),
            FieldType.Checkbox => AsBool(cell) == true ? "Yes" : (AsBool(cell) == false ? "No" : string.Empty),
            _ => AsString(cell) ?? string.Empty
        };
    }

    private static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;
        var noTags = HtmlTag.Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        return Whitespace.Replace(decoded, " ").Trim();
    }

    private static List<SelectOption> ParseSelectOptions(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return new List<SelectOption>();
        try
        {
            var cfg = JsonSerializer.Deserialize<SelectFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            return cfg?.Options ?? new List<SelectOption>();
        }
        catch (JsonException)
        {
            return new List<SelectOption>();
        }
    }

    private static List<TableColumn> ParseColumns(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return new List<TableColumn>();
        try
        {
            var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            return cfg?.Columns ?? new List<TableColumn>();
        }
        catch (JsonException)
        {
            return new List<TableColumn>();
        }
    }

    // ---------------------------------------------------------------- QuestPDF cell helpers

    private static void SectionHeading(IContainer container, string text)
        => container.PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
            .Text(text).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);

    private static void LabeledText(IContainer container, string label, string value)
    {
        container.Column(col =>
        {
            col.Item().Text(label).SemiBold().FontSize(11);
            col.Item().Text(value);
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
        => header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold().FontSize(9);

    private static void BodyCell(TableDescriptor table, string text)
        => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).FontSize(9);
}
