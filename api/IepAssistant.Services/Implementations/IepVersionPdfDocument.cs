using IepAssistant.Domain.Entities;
using IepAssistant.Services.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// QuestPDF document that renders a finalized <see cref="IepVersion"/> aggregate (P5b). Pure layout —
/// no I/O, no DB access; it receives a fully-loaded, already-ordered aggregate and composes a PDF.
/// The render service computes the bytes via <c>document.GeneratePdf()</c>.
///
/// <para><b>Labels (multilingual plan phase 7):</b> <paramref name="labels"/> carries every app-generated
/// word this document prints (headings, column labels, the footer). Defaults to
/// <see cref="PdfLabels.English"/> — the exact pre-phase-7 literals — so every existing call site
/// (including every test) that omits it keeps producing byte-identical English output. The render
/// service passes the requester's resolved <see cref="PdfLabels"/> for an actual render. Document CONTENT
/// (section rich text, goal/service/accommodation/transition data) is never translated — only the labels
/// printed around it.</para>
/// </summary>
public sealed class IepVersionPdfDocument : IDocument
{
    private readonly IepVersion _version;
    private readonly PdfLabels _labels;

    public IepVersionPdfDocument(IepVersion version, PdfLabels? labels = null)
    {
        _version = version;
        _labels = labels ?? PdfLabels.English;
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
                text.Span(_labels.PagePrefix);
                text.CurrentPageNumber();
                text.Span(_labels.PageOfMiddle);
                text.TotalPages();
            });
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Text(string.IsNullOrWhiteSpace(_version.Title) ? "IEP" : _version.Title!)
                .FontSize(18).Bold();
            col.Item().Text(string.Format(_labels.Version, _version.VersionNumber)).FontSize(11).SemiBold();
            col.Item().Text(string.Format(_labels.Finalized, _version.FinalizedAt.ToString("yyyy-MM-dd")))
                .FontSize(9).FontColor(Colors.Grey.Darken1);
            if (_version.EffectiveDate.HasValue)
                col.Item().Text(string.Format(_labels.Effective, _version.EffectiveDate.Value.ToString("yyyy-MM-dd")))
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(col =>
        {
            col.Spacing(14);

            ComposeSections(col);
            ComposeGoals(col);
            ComposeServices(col);
            ComposeAccommodations(col);
            ComposeTransition(col);
        });
    }

    // ---------------------------------------------------------------- Narrative sections

    private void ComposeSections(ColumnDescriptor col)
    {
        var sections = _version.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();
        if (sections.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, _labels.PresentLevelsAndNarrative));
        foreach (var s in sections)
        {
            col.Item().Column(inner =>
            {
                inner.Item().Text(s.SectionKind.ToString()).Bold().FontSize(11);
                // s.RichText is markdown (same TipTap editors as the authored-document RichText field) —
                // flatten it structurally rather than printing raw "**"/"- " syntax. This legacy document
                // keeps its plain label/value look, so plain text (not the styled block renderer) fits.
                inner.Item().Text(MarkdownText.ToPlainText(s.RichText));
            });
        }
    }

    // ---------------------------------------------------------------- Goals

    private void ComposeGoals(ColumnDescriptor col)
    {
        var goals = _version.Goals.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id).ToList();
        if (goals.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, _labels.Goals));
        var index = 1;
        foreach (var g in goals)
        {
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(inner =>
            {
                inner.Item().Text($"{_labels.Goal} {index}{(string.IsNullOrWhiteSpace(g.Domain) ? "" : $" — {g.Domain}")}").Bold();
                LabeledLine(inner, _labels.Goal, g.GoalText);
                LabeledLine(inner, _labels.Baseline, g.Baseline);
                LabeledLine(inner, _labels.TargetCriteria, g.TargetCriteria);
                LabeledLine(inner, _labels.Measurement, g.MeasurementMethod);
                LabeledLine(inner, _labels.Timeframe, g.Timeframe);
            });
            index++;
        }
    }

    // ---------------------------------------------------------------- Services

    private void ComposeServices(ColumnDescriptor col)
    {
        var services = _version.ServiceLines.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();
        if (services.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, _labels.Services));
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2); // type
                c.RelativeColumn(2); // frequency
                c.RelativeColumn(2); // duration
                c.RelativeColumn(2); // location
                c.RelativeColumn(2); // provider
                c.RelativeColumn(3); // dates
            });

            table.Header(header =>
            {
                HeaderCell(header, _labels.Service);
                HeaderCell(header, _labels.Frequency);
                HeaderCell(header, _labels.Duration);
                HeaderCell(header, _labels.Location);
                HeaderCell(header, _labels.Provider);
                HeaderCell(header, _labels.Dates);
            });

            foreach (var s in services)
            {
                BodyCell(table, s.ServiceType);
                BodyCell(table, s.Frequency);
                BodyCell(table, s.Duration);
                BodyCell(table, s.Location);
                BodyCell(table, s.ProviderRole);
                BodyCell(table, FormatDateRange(s.StartDate, s.EndDate));
            }
        });
    }

    // ---------------------------------------------------------------- Accommodations

    private void ComposeAccommodations(ColumnDescriptor col)
    {
        var accommodations = _version.Accommodations.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Id).ToList();
        if (accommodations.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, _labels.Accommodations));
        foreach (var a in accommodations)
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(140).Text(a.Category ?? string.Empty).SemiBold();
                row.RelativeItem().Text(a.Text ?? string.Empty);
            });
        }
    }

    // ---------------------------------------------------------------- Transition

    private void ComposeTransition(ColumnDescriptor col)
    {
        var items = _version.TransitionItems.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).ToList();
        if (items.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, _labels.Transition));
        foreach (var t in items)
        {
            col.Item().Column(inner =>
            {
                inner.Item().Text(t.PostsecondaryGoalArea ?? string.Empty).Bold();
                inner.Item().Text(t.ServicesText ?? string.Empty);
            });
        }
    }

    // ---------------------------------------------------------------- Helpers

    private static void SectionHeading(IContainer container, string text)
        => container.PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
            .Text(text).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);

    private static void LabeledLine(ColumnDescriptor col, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        col.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold();
            text.Span(value);
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text)
        => header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold().FontSize(9);

    private static void BodyCell(TableDescriptor table, string? text)
        => table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(text ?? string.Empty).FontSize(9);

    private static string FormatDateRange(DateTime? start, DateTime? end)
    {
        if (start == null && end == null) return string.Empty;
        var s = start?.ToString("yyyy-MM-dd") ?? "—";
        var e = end?.ToString("yyyy-MM-dd") ?? "—";
        return $"{s} → {e}";
    }
}
