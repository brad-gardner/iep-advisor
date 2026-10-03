using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Single source of truth for "which <c>_ownerUserId</c> values survive finalize" (plan 2026-10-02-002
/// review pass 2): <see cref="DocumentInstanceService.CoerceTable"/> re-validates an owner against active
/// team membership on every live SAVE, but a <see cref="DocumentInstance"/>'s frozen snapshot can still
/// carry a stale owner into an <see cref="AuthoredDocumentVersion"/> two ways that never go through
/// CoerceTable: (1) time simply passes between a save and its finalize, during which the owner's team
/// membership can be deactivated, and (2) <see cref="AuthoredDocumentVersionService.AmendAsync"/> copies a
/// prior version's ValuesJson verbatim into a new draft. <see cref="AuthoredDocumentVersionService.FinalizeAsync"/>
/// calls <see cref="StripInactiveOwners"/> (built from <see cref="LoadActiveTeamUserIdsAsync"/>) on every
/// owner-eligible table — not just Goals — immediately before freezing the snapshot, and
/// <see cref="GoalRecordService.ProjectOnFinalizeAsync"/> loads its own allow-list the same way, so the
/// finalized version, the rendered PDF (which reads the frozen ValuesJson) and the projected
/// <see cref="GoalRecord"/> row can never disagree about which owner survived.
/// </summary>
public static class OwnerEligibleRowSanitizer
{
    /// <summary>One query: every user id currently an ACTIVE <see cref="StudentTeamMember"/> of this
    /// student — the allow-list both finalize-time checks below validate a row's owner against.</summary>
    public static async Task<IReadOnlySet<int>> LoadActiveTeamUserIdsAsync(
        ApplicationDbContext context, int schoolStudentId, CancellationToken ct)
    {
        var ids = await context.StudentTeamMembers
            .AsNoTracking()
            .Where(m => m.SchoolStudentId == schoolStudentId && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>
    /// Returns a new, independent value-document (<paramref name="values"/> is not mutated) with every
    /// owner-eligible table's (goals, services, accommodations, transition) <c>_ownerUserId</c> removed
    /// wherever it is not a member of <paramref name="activeTeamUserIds"/>. A row with no owner, or whose
    /// owner IS an active member, is left exactly as-is. Mirrors the same table-walking pattern as
    /// <see cref="FamilyFacingValueRedactor.Redact"/>, but strips rather than replaces the owner (this runs
    /// on the staff-facing snapshot, not a family-facing read).
    /// </summary>
    public static JsonObject StripInactiveOwners(
        JsonObject values, IReadOnlyList<TemplateSectionModel> sections, IReadOnlySet<int> activeTeamUserIds)
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
                if (row[RowMetaKeys.OwnerUserId] is JsonValue v
                    && v.TryGetValue<int>(out var userId)
                    && !activeTeamUserIds.Contains(userId))
                {
                    row.Remove(RowMetaKeys.OwnerUserId);
                }
            }
        }

        return clone;
    }
}
