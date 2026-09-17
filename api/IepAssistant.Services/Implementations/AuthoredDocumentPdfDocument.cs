using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IepAssistant.Services.Implementations;

/// <summary>One participant line for the OH form's participants block (plan 7, decision 9) — resolved by
/// the render service from the student's latest Held meeting; the PDF document itself does no I/O.</summary>
public sealed record AuthoredDocumentPdfParticipant(string Name, string Role, bool? Attended);

/// <summary>
/// Everything the PDF composer needs beyond the frozen values/template tree, resolved by
/// <see cref="AuthoredDocumentPdfService"/> (the document itself stays DB-free). <see cref="StateCode"/>
/// selects the layout: <c>"OH"</c> renders the form-style layout (plan 7, decision 9); anything else
/// (including null) keeps the original generic layout.
/// </summary>
public sealed record AuthoredDocumentPdfHeaderContext(
    string? StateCode,
    string DocumentTypeKey,
    string StudentFirstName,
    string? StudentLastName,
    DateTime? StudentDateOfBirth,
    string? DistrictName,
    DateTime? IepDate,
    DateTime? EtrDate,
    DateTime? MeetingDate,
    IReadOnlyList<AuthoredDocumentPdfParticipant> Participants,
    int? AmendsVersionNumber,
    DateTime? EffectiveDate)
{
    public static readonly AuthoredDocumentPdfHeaderContext Empty =
        new(null, string.Empty, string.Empty, null, null, null, null, null, null, Array.Empty<AuthoredDocumentPdfParticipant>(), null, null);
}

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
/// <para><b>Generic (state-less) layout — Empty-field / empty-section rules (G-d.1):</b> a field is
/// rendered only when it holds a value; an empty field is omitted (required fields always hold a value
/// post-finalize). A checkbox counts as "empty" only when its key is absent — a present
/// <c>true</c>/<c>false</c> is a definite Yes/No answer and is rendered. A Table is empty when it has no
/// rows. A section that would render zero fields is omitted entirely.</para>
///
/// <para><b>OH form layout (plan 7, decision 9):</b> selected when <see cref="AuthoredDocumentPdfHeaderContext.StateCode"/>
/// is <c>"OH"</c>. Renders an ODE-style header block, every section in template order (numbered, NEVER
/// omitted — an entirely-empty section prints "Not addressed" instead of being skipped, and an
/// individually-empty required field within an otherwise-populated section prints "Not addressed" for
/// that field), goals as numbered blocks (one column-label-per-line, not a grid), services as a table,
/// a participants block from the latest Held meeting, fixed signature blocks, and an amendment banner
/// when this version amends another.</para>
///
/// <para><b>Exhaustiveness (G-d.3):</b> the per-field <c>switch</c> throws on an unhandled
/// <see cref="FieldType"/> so the render is marked Error rather than silently dropping content; the
/// worker never crashes because the render service swallows the throw into a retryable Error state.</para>
///
/// <para><b>Outline (test seam):</b> <see cref="Outline"/> records one entry per heading/structural block
/// emitted during <see cref="Compose"/>, in emission order, so a golden-structure test can assert layout
/// shape without parsing PDF bytes. Populated only after <c>GeneratePdf()</c>/<c>Compose</c> has run.</para>
/// </summary>
public sealed class AuthoredDocumentPdfDocument : IDocument
{
    private const string OhStateCode = "OH";
    private const int StudentSignatureMinAge = 14;

    private readonly string _documentTypeDisplayName;
    private readonly int _versionNumber;
    private readonly DateTime _finalizedAt;
    private readonly TemplateVersionDetailModel _tree;
    private readonly JsonObject _values;
    private readonly AuthoredDocumentPdfHeaderContext _header;
    private readonly bool _isOhForm;
    private readonly List<string> _outline = new();

    public AuthoredDocumentPdfDocument(
        string documentTypeDisplayName, int versionNumber, DateTime finalizedAt,
        TemplateVersionDetailModel tree, string? valuesJson,
        AuthoredDocumentPdfHeaderContext? header = null)
    {
        _documentTypeDisplayName = string.IsNullOrWhiteSpace(documentTypeDisplayName) ? "Document" : documentTypeDisplayName;
        _versionNumber = versionNumber;
        _finalizedAt = finalizedAt;
        _tree = tree;
        _header = header ?? AuthoredDocumentPdfHeaderContext.Empty;
        _isOhForm = string.Equals(_header.StateCode, OhStateCode, StringComparison.OrdinalIgnoreCase);

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

    /// <summary>One entry per heading/structural block emitted while composing, in order — a test seam
    /// (see the type doc's "Outline" section). Empty until <c>Compose</c> has run.</summary>
    public IReadOnlyList<string> Outline => _outline;

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

            page.Header().Element(_isOhForm ? ComposeOhHeader : ComposeHeader);
            page.Content().Element(_isOhForm ? ComposeOhContent : ComposeContent);
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    // =================================================================== Generic (state-less) layout

    private void ComposeHeader(IContainer container)
    {
        Note("Header");
        container.Column(col =>
        {
            col.Item().Text(_documentTypeDisplayName).FontSize(18).Bold();
            col.Item().Text($"Version {_versionNumber}").FontSize(11).SemiBold();
            col.Item().Text($"Finalized: {_finalizedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}")
                .FontSize(9).FontColor(Colors.Grey.Darken1);
            if (_header.AmendsVersionNumber.HasValue)
                col.Item().Element(c => AmendmentBanner(c));
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

                Note($"Section: {section.Title}");
                col.Item().Element(c => SectionHeading(c, section.Title));
                foreach (var field in fields)
                    col.Item().Element(c => ComposeField(c, field));
            }
        });
    }

    // =================================================================== OH form layout

    private void ComposeOhHeader(IContainer container)
    {
        Note("Header");
        var formId = _header.DocumentTypeKey switch
        {
            "IEP" => "PR-07",
            "ETR" => "PR-06",
            _ => null
        };
        var studentName = string.IsNullOrWhiteSpace(_header.StudentLastName)
            ? _header.StudentFirstName
            : $"{_header.StudentFirstName} {_header.StudentLastName}".Trim();

        container.Column(col =>
        {
            col.Item().Text(_documentTypeDisplayName).FontSize(18).Bold();
            if (formId != null)
                col.Item().Text($"Ohio Department of Education Form {formId}").FontSize(10).SemiBold();
            col.Item().Text($"Student: {studentName}").FontSize(10);
            col.Item().Text($"Date of birth: {FormatDate(_header.StudentDateOfBirth)}").FontSize(9);
            col.Item().Text($"District: {_header.DistrictName ?? "—"}").FontSize(9);
            col.Item().Text($"IEP date: {FormatDate(_header.IepDate)}   ETR date: {FormatDate(_header.EtrDate)}").FontSize(9);
            col.Item().Text($"Meeting date: {FormatDate(_header.MeetingDate)}").FontSize(9);
            col.Item().Text($"Version {_versionNumber} — Form version: template v{_tree.VersionNumber}").FontSize(9).FontColor(Colors.Grey.Darken1);
            col.Item().Text($"Finalized: {FormatDate(_finalizedAt)}").FontSize(9).FontColor(Colors.Grey.Darken1);

            if (_header.AmendsVersionNumber.HasValue)
                col.Item().Element(c => AmendmentBanner(c));

            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeOhContent(IContainer container)
    {
        container.PaddingVertical(10).Column(col =>
        {
            col.Spacing(14);

            var sectionNumber = 0;
            var goalNumber = 0;
            foreach (var section in _tree.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
            {
                sectionNumber++;
                // Seeded OH titles already read "Section 6: Measurable Annual Goals" — don't print "7. Section 6: …".
                var heading = AlreadyNumbered.IsMatch(section.Title) ? section.Title : $"{sectionNumber}. {section.Title}";
                Note($"Section {sectionNumber}: {section.Title}");
                col.Item().Element(c => SectionHeading(c, heading));

                var orderedFields = section.Fields.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id).ToList();
                var rendered = 0;
                foreach (var field in orderedFields)
                {
                    if (IsFieldEmpty(field))
                    {
                        // OH never silently omits a required field — it prints "Not addressed" in place.
                        if (field.Required)
                        {
                            Note($"Not addressed: {field.Label}");
                            col.Item().Element(c => LabeledText(c, field.Label, "Not addressed"));
                            rendered++;
                        }
                        continue;
                    }

                    rendered++;
                    if (IsGoalsField(field))
                    {
                        foreach (var goalBlock in ComposeGoalBlocks(field, ref goalNumber))
                            col.Item().Element(goalBlock);
                    }
                    else if (field.FieldType == FieldType.Table)
                    {
                        Note($"Table: {field.Label}");
                        col.Item().Element(c => ComposeTable(c, field, GetValue(field.FieldKey) as JsonArray));
                    }
                    else
                    {
                        col.Item().Element(c => ComposeField(c, field));
                    }
                }

                // Section-level rule: an entirely-empty section still appears (never omitted), marked
                // "Not addressed" as a whole rather than as N individual field placeholders.
                if (rendered == 0)
                {
                    Note($"Not addressed: {section.Title}");
                    col.Item().Text("Not addressed").Italic().FontColor(Colors.Grey.Darken1);
                }
            }

            Note("Participants");
            col.Item().Element(ComposeParticipants);

            Note("Signatures");
            col.Item().Element(ComposeSignatures);
        });
    }

    private bool IsGoalsField(TemplateFieldModel field) =>
        field.FieldType == FieldType.Table
        && TemplateSemanticsReader.ReadField(field.FieldType, field.ConfigJson).Semantic == FieldSemantics.Goals;

    /// <summary>Renders the Goals table as numbered blocks — one column-label-per-line — rather than the
    /// generic grid table (plan 7, decision 9).</summary>
    private IEnumerable<Action<IContainer>> ComposeGoalBlocks(TemplateFieldModel field, ref int goalNumber)
    {
        var rows = GetValue(field.FieldKey) as JsonArray;
        var columnLabels = TemplateSemanticsReader.ReadColumnLabels(field.ConfigJson);
        var blocks = new List<Action<IContainer>>();
        if (rows == null)
            return blocks;

        foreach (var rowNode in rows.OfType<JsonObject>())
        {
            goalNumber++;
            var n = goalNumber;
            Note($"Goal {n}");
            blocks.Add(container => container.Column(col =>
            {
                col.Item().Text($"Goal {n}").Bold().FontSize(11);
                foreach (var (columnKey, label) in columnLabels)
                {
                    var cell = rowNode[columnKey.ToString()];
                    var text = cell is JsonValue v ? v.ToString() : cell?.ToJsonString();
                    if (string.IsNullOrWhiteSpace(text))
                        continue;
                    col.Item().Text($"{label}: {text}").FontSize(9);
                }
            }));
        }
        return blocks;
    }

    private void ComposeParticipants(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text("Participants").SemiBold().FontSize(12);
            if (_header.Participants.Count == 0)
            {
                col.Item().Text("Not addressed").Italic().FontColor(Colors.Grey.Darken1);
                return;
            }
            foreach (var p in _header.Participants)
            {
                var attended = p.Attended switch { true => "Attended", false => "Did not attend", _ => "Attendance not recorded" };
                col.Item().Text($"{p.Name} — {p.Role} — {attended}").FontSize(9);
            }
        });
    }

    private void ComposeSignatures(IContainer container)
    {
        var lines = new List<string> { "Parent/Guardian" };
        if (IsStudentOldEnoughToSign())
            lines.Add("Student");
        lines.Add("District Representative");
        lines.Add("Teacher");

        container.Column(col =>
        {
            col.Item().Text("Signatures").SemiBold().FontSize(12);
            foreach (var line in lines)
            {
                col.Item().PaddingTop(8).Text(line).FontSize(9).SemiBold();
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text("Name: _______________________").FontSize(9);
                    row.RelativeItem().Text("Signature: _______________________").FontSize(9);
                    row.RelativeItem().Text("Date: __________").FontSize(9);
                });
            }
        });
    }

    private bool IsStudentOldEnoughToSign()
    {
        if (_header.StudentDateOfBirth is not { } dob)
            return false;
        var reference = _finalizedAt;
        var age = reference.Year - dob.Year;
        if (dob.Date > reference.AddYears(-age)) age--;
        return age >= StudentSignatureMinAge;
    }

    private void AmendmentBanner(IContainer container)
    {
        Note("Amendment");
        var effective = _header.EffectiveDate.HasValue ? $" — effective {FormatDate(_header.EffectiveDate)}" : string.Empty;
        container.Background(Colors.Yellow.Lighten3).Padding(4)
            .Text($"Amendment to v{_header.AmendsVersionNumber}{effective}").Bold().FontSize(10);
    }

    // =================================================================== Shared fields

    private void ComposeField(IContainer container, TemplateFieldModel field)
    {
        var node = GetValue(field.FieldKey);

        switch (field.FieldType)
        {
            case FieldType.Text:
                LabeledText(container, field.Label, AsString(node) ?? string.Empty);
                break;

            case FieldType.RichText:
                // Render the stored markdown structurally (bold/italic/strike, headings, lists, quotes,
                // links) rather than flattening it to one line of "**"/"- " syntax (G-d.3 / defense in
                // depth: unknown/HTML nodes still degrade to plain text — see MarkdownPdfPlanBuilder).
                RichTextBlock(container, field.Label, AsString(node));
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

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";

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

    private static readonly System.Text.RegularExpressions.Regex AlreadyNumbered =
        new(@"^\s*(section\s*)?\d+[.:)]", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

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

    // ---------------------------------------------------------------- Markdown (RichText) rendering
    //
    // Renders from a MarkdownPdfPlan (MarkdownPdfPlan.cs) rather than walking the Markdig AST directly:
    // MarkdownPdfPlanBuilder.Build does the "what does this markdown mean" work (paragraphs/headings/
    // lists/quotes, bold/italic/strike flags, link-scheme validation, nesting-depth capping) as a pure,
    // unit-testable step; this class only turns that plan into QuestPDF elements. Any block/inline type
    // the plan builder doesn't specifically recognize (including a GFM table, or raw HTML that slipped
    // past RichTextSanitizer) still degrades to plain text there — defense in depth, never a dropped
    // field (G-d.3) — and list/quote nesting beyond MarkdownPdfPlanBuilder.MaxNestingDepth renders flat
    // instead of compounding indentation/padding without bound.

    private static void RichTextBlock(IContainer container, string label, string? markdown)
    {
        container.Column(col =>
        {
            col.Item().Text(label).SemiBold().FontSize(11);
            col.Item().Element(c => ComposeMarkdown(c, markdown));
        });
    }

    private static void ComposeMarkdown(IContainer container, string? markdown)
    {
        var plan = MarkdownPdfPlanBuilder.Build(markdown);
        if (plan.Blocks.Count == 0)
        {
            container.Text(string.Empty);
            return;
        }

        container.Column(col =>
        {
            col.Spacing(4);
            RenderPlanBlocks(col, plan.Blocks);
        });
    }

    private static void RenderPlanBlocks(ColumnDescriptor col, IReadOnlyList<MarkdownPdfBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case MarkdownPdfParagraph paragraph:
                    col.Item().Text(text => RenderRuns(text, paragraph.Runs));
                    break;

                case MarkdownPdfHeading heading:
                {
                    var size = heading.Level switch { 1 => 15f, 2 => 14f, _ => 12f };
                    col.Item().Text(text =>
                    {
                        text.DefaultTextStyle(s => s.FontSize(size).SemiBold());
                        RenderRuns(text, heading.Runs);
                    });
                    break;
                }

                case MarkdownPdfQuote quote:
                    // At most MaxNestingDepth of these actually get nested (see MarkdownPdfPlanBuilder),
                    // so this fixed-per-level padding can never compound into an impossible constraint.
                    col.Item().PaddingLeft(4).BorderLeft(2).BorderColor(Colors.Grey.Lighten1).PaddingLeft(8)
                        .Column(inner =>
                        {
                            inner.Spacing(2);
                            RenderPlanBlocks(inner, quote.Blocks);
                        });
                    break;

                case MarkdownPdfListItem item:
                    // Flat: one Column item per list item at every depth, indent-only via PaddingLeft —
                    // no Row/Column nested per nesting level (see MarkdownPdfPlanBuilder's remarks on the
                    // exponential-time defect this replaces).
                    col.Item().PaddingLeft(item.Depth * 14).Text(text =>
                    {
                        text.Span(item.Marker + " ");
                        RenderRuns(text, item.Runs);
                    });
                    break;
            }
        }
    }

    private static void RenderRuns(TextDescriptor text, IReadOnlyList<MarkdownPdfRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.Href != null)
                EmitMarkdownLink(text, run);
            else
                EmitMarkdownSpan(text, run);
        }
    }

    private static void EmitMarkdownSpan(TextDescriptor text, MarkdownPdfRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
            return;
        var span = text.Span(run.Text);
        if (run.Bold) span.Bold();
        if (run.Italic) span.Italic();
        if (run.Strike) span.Strikethrough();
    }

    private static void EmitMarkdownLink(TextDescriptor text, MarkdownPdfRun run)
    {
        if (string.IsNullOrEmpty(run.Text))
            return;

        // Defense in depth: MarkdownPdfPlanBuilder already checked RichTextSanitizer.IsSafeUrl before
        // setting Href, but this is the sink that actually makes a url actionable (QuestPDF's
        // Hyperlink() annotation), so re-validate here too — a disallowed scheme renders as a plain,
        // non-linked span rather than ever reaching Hyperlink() (reviewer pass1 P1: link scheme bypass).
        var span = RichTextSanitizer.IsSafeUrl(run.Href!) ? text.Hyperlink(run.Text, run.Href!) : text.Span(run.Text);
        span.Underline();
        if (run.Bold) span.Bold();
        if (run.Italic) span.Italic();
        if (run.Strike) span.Strikethrough();
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
        => header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold().FontSize(9);

    private static void BodyCell(TableDescriptor table, string text)
        => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text).FontSize(9);

    // ---------------------------------------------------------------- Outline (test seam)

    private void Note(string entry) => _outline.Add(entry);
}
