using System.Text.Json.Nodes;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Review fix (plan 2026-10-02-002, P2-1): <c>RowContentEqual</c> previously excluded every
/// <see cref="RowMetaKeys.All"/> key, which silently hid objective and owner edits from every consumer
/// of <see cref="ChangeSummaryBuilder"/> (family re-share diff, converge, meeting brief). This exercises
/// the pure diff directly — no DB — against a single Goals-semantic table field.
/// </summary>
public sealed class ChangeSummaryBuilderTests
{
    private readonly Guid _goalsFieldKey = Guid.NewGuid();
    private readonly Guid _goalTextCol = Guid.NewGuid();
    private readonly Guid _rowId = Guid.NewGuid();

    private List<TemplateSectionModel> Sections() => new()
    {
        new TemplateSectionModel
        {
            Id = 1, Title = "Goals", DisplayOrder = 0,
            Fields = new List<TemplateFieldModel>
            {
                new()
                {
                    Id = 1, FieldKey = _goalsFieldKey, FieldType = FieldType.Table, Label = "Goals", DisplayOrder = 0,
                    ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals, (_goalTextCol, FieldType.Text, "Goal", ColumnSemantics.GoalText))
                }
            }
        }
    };

    private JsonObject Document(JsonObject row) => new()
    {
        [_goalsFieldKey.ToString()] = new JsonArray(row)
    };

    private JsonObject Row(string goalText, JsonArray? objectives = null, int? ownerUserId = null, Guid? rowId = null)
    {
        var row = new JsonObject
        {
            [RowMetaKeys.RowId] = (rowId ?? _rowId).ToString(),
            [_goalTextCol.ToString()] = goalText
        };
        if (objectives != null) row[RowMetaKeys.Objectives] = objectives;
        if (ownerUserId != null) row[RowMetaKeys.OwnerUserId] = ownerUserId.Value;
        return row;
    }

    private static JsonObject Objective(string? description = null, string? criteria = null, string? targetDate = null, Guid? rowId = null)
    {
        var o = new JsonObject { [RowMetaKeys.RowId] = (rowId ?? Guid.NewGuid()).ToString() };
        if (description != null) o["description"] = description;
        if (criteria != null) o["criteria"] = criteria;
        if (targetDate != null) o["targetDate"] = targetDate;
        return o;
    }

    [Fact]
    public void ObjectivesOnlyEdit_IsReportedAsChanged()
    {
        var previous = Document(Row("Read better", new JsonArray(Objective("Read a paragraph", "80%", "2026-12-01"))));
        var next = Document(Row("Read better", new JsonArray(Objective("Read two paragraphs", "80%", "2026-12-01"))));

        var diff = ChangeSummaryBuilder.Build(Sections(), previous, next);

        Assert.Contains(diff.ChangedRows, r => r.RowId == _rowId.ToString());
    }

    [Fact]
    public void ObjectivesReissuedIdsOnly_IsNotReported()
    {
        // Same description/criteria/targetDate; only the objective's OWN _rowId differs (re-issued
        // positionally by DocumentInstanceService on every save) — must not look like a change.
        var previous = Document(Row("Read better", new JsonArray(Objective("Read a paragraph", "80%", "2026-12-01", rowId: Guid.NewGuid()))));
        var next = Document(Row("Read better", new JsonArray(Objective("Read a paragraph", "80%", "2026-12-01", rowId: Guid.NewGuid()))));

        var diff = ChangeSummaryBuilder.Build(Sections(), previous, next);

        Assert.DoesNotContain(diff.ChangedRows, r => r.RowId == _rowId.ToString());
        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void OwnerChange_IsReportedAsChanged_WithNoIdentityInTheSummary()
    {
        var previous = Document(Row("Read better"));
        var next = Document(Row("Read better", ownerUserId: 42));

        var diff = ChangeSummaryBuilder.Build(Sections(), previous, next);

        var changed = Assert.Single(diff.ChangedRows);
        Assert.Equal(_rowId.ToString(), changed.RowId);
        // The row's label is its primary column text, not the owner — never a user id in the summary.
        Assert.Equal("Read better", changed.Label);
        Assert.DoesNotContain("42", changed.Label);
    }

    [Fact]
    public void OwnerRole_AloneNeverCountsAsAChange()
    {
        // _ownerRole is an output-only redaction artifact (FamilyFacingValueRedactor) that never appears
        // on a real saved value-document, but it must stay pure metadata if it ever does.
        var previousRow = Row("Read better");
        previousRow[RowMetaKeys.OwnerRole] = "Intervention Specialist";
        var nextRow = Row("Read better");
        nextRow[RowMetaKeys.OwnerRole] = "Speech-Language Pathologist";

        var diff = ChangeSummaryBuilder.Build(Sections(), Document(previousRow), Document(nextRow));

        Assert.True(diff.IsEmpty);
    }

    [Fact]
    public void CarryForwardMetadataAlone_NeverCountsAsAChange()
    {
        var previousRow = Row("Read better");
        previousRow[RowMetaKeys.Confirmed] = false;
        var nextRow = Row("Read better");
        nextRow[RowMetaKeys.Confirmed] = true;
        nextRow[RowMetaKeys.CarriedFrom] = new JsonObject { ["versionId"] = 1, ["rowId"] = Guid.NewGuid().ToString() };

        var diff = ChangeSummaryBuilder.Build(Sections(), Document(previousRow), Document(nextRow));

        Assert.True(diff.IsEmpty);
    }
}
