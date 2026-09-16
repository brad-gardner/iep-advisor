using System.Text.Json.Nodes;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Pure semantic diff between two value-documents of the SAME pinned template version (plan 6, decision
/// 1): row-block Table fields (goals, services, accommodations, transition, …) are matched by their
/// stable `_rowId` — a row surviving under the same id but with different cell content is "changed", not
/// "removed + added" — and every other field is compared as a whole narrative/scalar value. No DbContext;
/// takes already-loaded template sections and parsed value-documents.
/// </summary>
public static class ChangeSummaryBuilder
{
    public static ChangeSummaryModel Build(IReadOnlyList<TemplateSectionModel> sections, JsonObject previous, JsonObject next)
    {
        var added = new List<ChangeRowModel>();
        var removed = new List<ChangeRowModel>();
        var changed = new List<ChangeRowModel>();
        var changedFields = new List<ChangeFieldModel>();

        foreach (var section in sections.OrderBy(s => s.DisplayOrder))
        {
            foreach (var field in section.Fields.OrderBy(f => f.DisplayOrder))
            {
                var (semantic, _) = TemplateSemanticsReader.ReadField(field.FieldType, field.ConfigJson);
                if (field.FieldType == FieldType.Table)
                    DiffTable(field, semantic, previous, next, added, removed, changed);
                else
                    DiffScalar(field, previous, next, changedFields);
            }
        }

        return new ChangeSummaryModel
        {
            AddedRows = added,
            RemovedRows = removed,
            ChangedRows = changed,
            ChangedFields = changedFields,
            SummaryText = BuildSummaryText(added, removed, changed, changedFields)
        };
    }

    private static void DiffTable(
        TemplateFieldModel field, string? semantic, JsonObject previous, JsonObject next,
        List<ChangeRowModel> added, List<ChangeRowModel> removed, List<ChangeRowModel> changed)
    {
        var prevRows = RowsById(previous, field.FieldKey);
        var nextRows = RowsById(next, field.FieldKey);
        var columnLabels = TemplateSemanticsReader.ReadColumnLabels(field.ConfigJson);
        var columnSemantics = TemplateSemanticsReader.ReadColumns(field.FieldType, field.ConfigJson);
        var primarySemantic = DraftRowLabeler.PrimaryColumnSemantic(semantic);

        foreach (var (rowId, row) in nextRows)
        {
            var label = DraftRowLabeler.LabelForRow(row, columnLabels, columnSemantics, primarySemantic);
            if (!prevRows.TryGetValue(rowId, out var prevRow))
                added.Add(new ChangeRowModel { FieldKey = field.FieldKey, FieldLabel = field.Label, RowId = rowId, Label = label });
            else if (!RowContentEqual(prevRow, row))
                changed.Add(new ChangeRowModel { FieldKey = field.FieldKey, FieldLabel = field.Label, RowId = rowId, Label = label });
        }

        foreach (var (rowId, row) in prevRows)
        {
            if (!nextRows.ContainsKey(rowId))
            {
                var label = DraftRowLabeler.LabelForRow(row, columnLabels, columnSemantics, primarySemantic);
                removed.Add(new ChangeRowModel { FieldKey = field.FieldKey, FieldLabel = field.Label, RowId = rowId, Label = label });
            }
        }
    }

    private static Dictionary<string, JsonObject> RowsById(JsonObject values, Guid fieldKey)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (values[fieldKey.ToString()] is not JsonArray rows) return result;
        foreach (var row in rows.OfType<JsonObject>())
        {
            var rowId = row[RowMetaKeys.RowId]?.ToString();
            if (rowId != null) result[rowId] = row;
        }
        return result;
    }

    /// <summary>Content-only equality: row metadata (`_rowId`, `_carriedFrom`, `_confirmed`) never counts as a change.</summary>
    private static bool RowContentEqual(JsonObject a, JsonObject b)
    {
        var aCells = a.Where(kv => !RowMetaKeys.All.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value?.ToJsonString());
        var bCells = b.Where(kv => !RowMetaKeys.All.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value?.ToJsonString());
        if (aCells.Count != bCells.Count) return false;
        foreach (var (key, value) in aCells)
        {
            if (!bCells.TryGetValue(key, out var otherValue) || !string.Equals(value, otherValue, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static void DiffScalar(TemplateFieldModel field, JsonObject previous, JsonObject next, List<ChangeFieldModel> changedFields)
    {
        var prevText = ScalarString(previous[field.FieldKey.ToString()]);
        var nextText = ScalarString(next[field.FieldKey.ToString()]);
        if (!string.Equals(prevText, nextText, StringComparison.Ordinal))
            changedFields.Add(new ChangeFieldModel { FieldKey = field.FieldKey, FieldLabel = field.Label });
    }

    private static string? ScalarString(JsonNode? node) => node is JsonValue v ? v.ToString() : node?.ToJsonString();

    private static string BuildSummaryText(List<ChangeRowModel> added, List<ChangeRowModel> removed, List<ChangeRowModel> changed, List<ChangeFieldModel> changedFields)
    {
        var parts = new List<string>();
        if (added.Count > 0) parts.Add($"{added.Count} row{(added.Count == 1 ? "" : "s")} added");
        if (removed.Count > 0) parts.Add($"{removed.Count} row{(removed.Count == 1 ? "" : "s")} removed");
        if (changed.Count > 0) parts.Add($"{changed.Count} row{(changed.Count == 1 ? "" : "s")} changed");
        if (changedFields.Count > 0) parts.Add($"{changedFields.Count} section{(changedFields.Count == 1 ? "" : "s")} updated");
        return parts.Count == 0 ? "No changes since the last share." : string.Join(", ", parts) + ".";
    }
}
