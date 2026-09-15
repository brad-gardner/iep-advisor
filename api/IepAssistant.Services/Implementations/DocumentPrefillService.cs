using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// "The draft is never blank." Builds the initial value-document for a new instance from the student's
/// evidence bundle, addressed purely by semantics so it works for any template:
/// <list type="bullet">
/// <item><b>studentProfile</b> ← identity + team (plain text lines).</item>
/// <item><b>presentLevels</b> ← the latest present-levels narrative (prior IEP, else ETR team summary /
/// findings), prefixed with a provenance line so nothing reads as freshly written.</item>
/// <item><b>goals / services / accommodations / transition</b> ← rows carried from the latest finalized
/// version of the same document type, keeping their <c>_rowId</c> (lineage) and stamping
/// <c>_carriedFrom</c> so the editor flags them as stale until kept or revised.</item>
/// </list>
/// Nothing is invented: a field with no evidence stays empty.
/// </summary>
public sealed class DocumentPrefillService : IDocumentPrefillService
{
    private readonly ApplicationDbContext _context;

    public DocumentPrefillService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<JsonObject> BuildInitialValuesAsync(int documentTemplateVersionId, string documentTypeKey, StudentEvidenceBundle evidence, CancellationToken ct = default)
    {
        var sections = await _context.TemplateSections.AsNoTracking()
            .Where(s => s.DocumentTemplateVersionId == documentTemplateVersionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);
        var semantics = TemplateSemanticsReader.Read(sections);
        var values = new JsonObject();

        // ---- Student profile: identity + team, plain text.
        if (semantics.TryGetValue(FieldSemantics.StudentProfile, out var profile) && profile.FieldType is FieldType.RichText or FieldType.Text)
        {
            var identity = evidence.Items.FirstOrDefault(i => i.Kind == EvidenceKind.Identity);
            var team = evidence.Items.Where(i => i.Kind == EvidenceKind.TeamMember).Select(i => i.Text).ToList();
            var lines = new List<string>();
            if (identity != null) lines.Add(identity.Text);
            if (team.Count > 0) lines.Add("IEP team: " + string.Join("; ", team));
            if (lines.Count > 0)
                values[profile.FieldKey.ToString()] = string.Join("\n", lines);
        }

        // ---- Present levels: prior IEP narrative first, else ETR findings.
        if (semantics.TryGetValue(FieldSemantics.PresentLevels, out var plaafp) && plaafp.FieldType is FieldType.RichText or FieldType.Text)
        {
            var narrative = evidence.Items.FirstOrDefault(i => i.Kind == EvidenceKind.PresentLevels)
                ?? evidence.Items.FirstOrDefault(i => i.Kind == EvidenceKind.EtrFinding);
            if (narrative != null && !string.Equals(documentTypeKey, "ETR", StringComparison.OrdinalIgnoreCase))
            {
                var stamp = narrative.SourceDate is { } d ? $"{narrative.SourceLabel}, {d:yyyy-MM-dd}" : narrative.SourceLabel;
                values[plaafp.FieldKey.ToString()] = $"[Carried from {stamp} — review and update]\n\n{narrative.Text}";
            }
        }

        // ---- Structured rows carried from the latest finalized version of the SAME document type.
        var sameType = evidence.Sources.FirstOrDefault(s => string.Equals(s.DocumentTypeKey, documentTypeKey, StringComparison.OrdinalIgnoreCase));
        if (sameType != null)
        {
            CarryRows(values, semantics, FieldSemantics.Goals, EvidenceKind.PriorGoal, evidence, sameType);
            CarryRows(values, semantics, FieldSemantics.Services, EvidenceKind.PriorService, evidence, sameType);
            CarryRows(values, semantics, FieldSemantics.Accommodations, EvidenceKind.PriorAccommodation, evidence, sameType);
            CarryRows(values, semantics, FieldSemantics.Transition, EvidenceKind.PriorTransition, evidence, sameType);
        }

        return values;
    }

    private static void CarryRows(
        JsonObject values, IReadOnlyDictionary<string, SemanticField> semantics, string fieldSemantic,
        EvidenceKind kind, StudentEvidenceBundle evidence, EvidenceSource source)
    {
        if (!semantics.TryGetValue(fieldSemantic, out var field) || field.FieldType != FieldType.Table || field.Columns.Count == 0)
            return;

        var rows = new JsonArray();
        foreach (var item in evidence.Items.Where(i => i.Kind == kind && i.SourceType == source.SourceType && i.SourceId == source.SourceId && i.Fields != null))
        {
            var row = new JsonObject();
            var any = false;
            foreach (var (colSem, colKey) in field.Columns)
            {
                if (item.Fields!.TryGetValue(colSem, out var text) && !string.IsNullOrWhiteSpace(text))
                {
                    row[colKey.ToString()] = text;
                    any = true;
                }
            }
            if (!any) continue;

            // Keep the lineage id when the source row had one; CoerceTable assigns a fresh id otherwise.
            if (Guid.TryParse(item.RowId, out var lineage) && lineage != Guid.Empty)
                row[RowMetaKeys.RowId] = lineage.ToString();
            var provenance = new JsonObject
            {
                ["versionId"] = source.SourceId,
                ["rowId"] = item.RowId ?? Guid.Empty.ToString(),
                ["label"] = source.Label,
            };
            if (source.Date is { } d) provenance["date"] = d.ToString("yyyy-MM-dd");
            row[RowMetaKeys.CarriedFrom] = provenance;
            row[RowMetaKeys.Confirmed] = false;
            rows.Add(row);
        }
        if (rows.Count > 0)
            values[field.FieldKey.ToString()] = rows;
    }
}
