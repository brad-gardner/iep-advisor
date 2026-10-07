namespace IepAssistant.Services;

/// <summary>
/// Marker type for <c>IStringLocalizer&lt;Pdf&gt;</c>, resolving to <c>Resources/Pdf.resx</c> (English) and
/// <c>Resources/Pdf.es.resx</c> (Spanish) via the <c>ResourcesPath = "Resources"</c> configured in
/// <c>AddLocalization</c> (Api/Program.cs) — see <see cref="Messages"/> for why this lives in the
/// project's root namespace rather than <c>IepAssistant.Services.Resources</c>.
///
/// Multilingual plan (2026-10-06) phase 7: holds the APP-GENERATED labels/headings/footer text that
/// <c>IepVersionPdfDocument</c> and <c>AuthoredDocumentPdfDocument</c> print around the document/district
/// content (e.g. "Goals", "Baseline", "Page {0} of {1}", "Not addressed", "Responsible: {0}"). Deliberately
/// a SEPARATE resx from <c>Messages.resx</c> (that one holds <c>ServiceResult</c>/<c>ApiResponse</c> text,
/// not PDF body copy) — see <see cref="Localization.PdfLabels"/>, the one place that reads this resource
/// into a plain-data record so the QuestPDF document classes stay DI-free ("pure layout — no I/O").
/// Document CONTENT and district-authored template labels are NEVER read from here — only text the PDF
/// composer itself writes.
/// </summary>
public sealed class Pdf
{
}
