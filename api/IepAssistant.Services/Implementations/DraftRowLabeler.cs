using System.Text.Json.Nodes;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Resolves a human-readable label for a table row or a scalar field from a value-document, shared by
/// <see cref="DraftPromptBuilder"/> (AI prompt lines), <c>ChangeSummaryBuilder</c> (diff row labels) and
/// <c>DraftSharingService</c>/<c>DraftResponseService</c> (a response's <c>targetLabel</c>).
/// </summary>
public static class DraftRowLabeler
{
    /// <summary>The "primary" column semantic shown as a row-block's label (mirrors StudentEvidenceService's inline choices).</summary>
    public static string? PrimaryColumnSemantic(string? fieldSemantic) => fieldSemantic switch
    {
        FieldSemantics.Goals => ColumnSemantics.GoalText,
        FieldSemantics.Services => ColumnSemantics.ServiceType,
        FieldSemantics.Accommodations => ColumnSemantics.Accommodation,
        FieldSemantics.Transition => ColumnSemantics.GoalArea,
        FieldSemantics.Participants => ColumnSemantics.ParticipantName,
        FieldSemantics.EvaluatorReports => ColumnSemantics.EvaluatorName,
        _ => null
    };

    /// <summary>Best-effort human label for a table row: the primary column's text, else the first non-empty cell.</summary>
    public static string LabelForRow(
        JsonObject row,
        IReadOnlyDictionary<Guid, string> columnLabels,
        IReadOnlyDictionary<string, Guid> columnSemantics,
        string? primarySemantic)
    {
        if (primarySemantic != null && columnSemantics.TryGetValue(primarySemantic, out var primaryColKey))
        {
            var text = CellText(row, primaryColKey);
            if (!string.IsNullOrWhiteSpace(text))
                return text!;
        }
        foreach (var colKey in columnLabels.Keys)
        {
            var text = CellText(row, colKey);
            if (!string.IsNullOrWhiteSpace(text))
                return text!;
        }
        return "(untitled row)";
    }

    public static string? CellText(JsonObject row, Guid columnKey)
    {
        var cell = row[columnKey.ToString()];
        return cell is JsonValue v ? v.ToString() : cell?.ToJsonString();
    }

    public static JsonObject? FindRow(JsonObject values, Guid fieldKey, string rowId)
    {
        if (values[fieldKey.ToString()] is not JsonArray rows) return null;
        foreach (var row in rows.OfType<JsonObject>())
        {
            if (row[RowMetaKeys.RowId] is JsonValue v && string.Equals(v.ToString(), rowId, StringComparison.OrdinalIgnoreCase))
                return row;
        }
        return null;
    }

    /// <summary>Best-effort human label for a <c>DraftResponse</c>'s (or citation's) target field/row,
    /// resolved from the revision's pinned schema + frozen values. Null when the field no longer resolves
    /// (should not happen for a well-formed target, but never throws).</summary>
    public static string? ResolveTargetLabel(IReadOnlyList<TemplateSectionModel> sections, JsonObject values, Guid fieldKey, string? rowId)
    {
        foreach (var section in sections)
        {
            var field = section.Fields.FirstOrDefault(f => f.FieldKey == fieldKey);
            if (field == null) continue;

            if (rowId == null)
                return field.Label;

            var row = FindRow(values, fieldKey, rowId);
            if (row == null)
                return field.Label;

            var (semantic, _) = TemplateSemanticsReader.ReadField(field.FieldType, field.ConfigJson);
            var columnLabels = TemplateSemanticsReader.ReadColumnLabels(field.ConfigJson);
            var columnSemantics = TemplateSemanticsReader.ReadColumns(field.FieldType, field.ConfigJson);
            return LabelForRow(row, columnLabels, columnSemantics, PrimaryColumnSemantic(semantic));
        }
        return null;
    }
}
