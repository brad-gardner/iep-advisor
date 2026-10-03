using System.Text.Json.Nodes;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Strips raw <c>_ownerUserId</c> values out of a value-document before any family/student-facing surface
/// sees it (plan 2026-10-02-002, design "Resolved Questions" #1: owners appear by role only, never by
/// name, on family-facing outputs). Every goals/services/accommodations/transition row's owner is removed
/// and — when it resolves against the supplied role map — replaced with a role-only
/// <see cref="RowMetaKeys.OwnerRole"/> string, e.g. "Intervention Specialist".
/// </summary>
public static class FamilyFacingValueRedactor
{
    /// <summary>Returns a new, independent value-document; <paramref name="values"/> is not mutated.</summary>
    public static JsonObject Redact(JsonObject values, IReadOnlyList<TemplateSectionModel> sections, IReadOnlyDictionary<int, string> roleByUserId)
    {
        var clone = (JsonNode.Parse(values.ToJsonString()) as JsonObject) ?? new JsonObject();

        foreach (var field in sections.SelectMany(s => s.Fields))
        {
            if (field.FieldType != FieldType.Table)
                continue;

            var semantic = TemplateSemanticsReader.ReadField(field.FieldType, field.ConfigJson).Semantic;
            if (semantic == null || !FieldSemantics.OwnerEligible.Contains(semantic))
                continue;

            if (clone[field.FieldKey.ToString()] is not JsonArray rows)
                continue;

            foreach (var row in rows.OfType<JsonObject>())
            {
                var ownerNode = row[RowMetaKeys.OwnerUserId];
                if (ownerNode == null)
                    continue;
                row.Remove(RowMetaKeys.OwnerUserId);

                if (ownerNode is JsonValue v && v.TryGetValue<int>(out var userId) && roleByUserId.TryGetValue(userId, out var role))
                    row[RowMetaKeys.OwnerRole] = role;
                // Unresolvable owner (no team record at all for that user): dropped with no replacement —
                // never a raw id, never a guess at who it was.
            }
        }

        return clone;
    }
}
