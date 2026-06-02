using IepAssistant.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// QuestPDF document that renders a finalized <see cref="IepVersion"/> aggregate (P5b). Pure layout —
/// no I/O, no DB access; it receives a fully-loaded, already-ordered aggregate and composes a PDF.
/// The render service computes the bytes via <c>document.GeneratePdf()</c>.
/// </summary>
public sealed class IepVersionPdfDocument : IDocument
{
    private readonly IepVersion _version;

    public IepVersionPdfDocument(IepVersion version)
    {
        _version = version;
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
            col.Item().Text(string.IsNullOrWhiteSpace(_version.Title) ? "IEP" : _version.Title!)
                .FontSize(18).Bold();
            col.Item().Text($"Version {_version.VersionNumber}").FontSize(11).SemiBold();
            col.Item().Text($"Finalized: {_version.FinalizedAt:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Darken1);
            if (_version.EffectiveDate.HasValue)
                col.Item().Text($"Effective: {_version.EffectiveDate:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Darken1);
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

        col.Item().Element(c => SectionHeading(c, "Present Levels & Narrative"));
        foreach (var s in sections)
        {
            col.Item().Column(inner =>
            {
                inner.Item().Text(s.SectionKind.ToString()).Bold().FontSize(11);
                inner.Item().Text(s.RichText ?? string.Empty);
            });
        }
    }

    // ---------------------------------------------------------------- Goals

    private void ComposeGoals(ColumnDescriptor col)
    {
        var goals = _version.Goals.OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id).ToList();
        if (goals.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, "Goals"));
        var index = 1;
        foreach (var g in goals)
        {
            col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(inner =>
            {
                inner.Item().Text($"Goal {index}{(string.IsNullOrWhiteSpace(g.Domain) ? "" : $" — {g.Domain}")}").Bold();
                LabeledLine(inner, "Goal", g.GoalText);
                LabeledLine(inner, "Baseline", g.Baseline);
                LabeledLine(inner, "Target Criteria", g.TargetCriteria);
                LabeledLine(inner, "Measurement", g.MeasurementMethod);
                LabeledLine(inner, "Timeframe", g.Timeframe);
            });
            index++;
        }
    }

    // ---------------------------------------------------------------- Services

    private void ComposeServices(ColumnDescriptor col)
    {
        var services = _version.ServiceLines.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id).ToList();
        if (services.Count == 0) return;

        col.Item().Element(c => SectionHeading(c, "Services"));
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
                HeaderCell(header, "Service");
                HeaderCell(header, "Frequency");
                HeaderCell(header, "Duration");
                HeaderCell(header, "Location");
                HeaderCell(header, "Provider");
                HeaderCell(header, "Dates");
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

        col.Item().Element(c => SectionHeading(c, "Accommodations"));
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

        col.Item().Element(c => SectionHeading(c, "Transition"));
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
