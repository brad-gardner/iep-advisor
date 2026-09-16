using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Pure completeness math (see <see cref="IDocumentCompletenessService"/>) mirroring
/// <c>web/src/features/document-authoring/lib/completeness.ts</c>'s blank/required rules: a value is
/// blank when it is missing/JSON-null, an HTML-stripped-empty/whitespace string, or an empty array
/// (booleans and non-empty arrays are never blank). A Table field counts as filled once it has any row
/// at all — <c>RequiredMissing</c> separately counts each row's blank REQUIRED columns, on top of any
/// blank required top-level field, AND flags a Table whose row count falls below its configured
/// <c>minRows</c> even when the field itself isn't marked Required (mirrors
/// <see cref="AuthoredDocumentVersionService.ValidateTable"/>'s independent minRows check, so this
/// percent/count never understates what finalize will actually block on).
/// </summary>
public class DocumentCompletenessService : IDocumentCompletenessService
{
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);

    private readonly ApplicationDbContext _context;
    private readonly ITemplateAuthoringService _authoring;

    public DocumentCompletenessService(ApplicationDbContext context, ITemplateAuthoringService authoring)
    {
        _context = context;
        _authoring = authoring;
    }

    public DocumentCompletenessModel Compute(IReadOnlyList<TemplateSectionModel> sections, string? valuesJson)
    {
        var values = ParseValues(valuesJson);

        var total = 0;
        var filled = 0;
        var requiredMissing = 0;

        foreach (var section in sections.OrderBy(s => s.DisplayOrder))
        {
            foreach (var field in section.Fields.OrderBy(f => f.DisplayOrder))
            {
                total++;
                values.TryGetPropertyValue(field.FieldKey.ToString(), out var node);
                var blank = IsBlank(node);
                if (!blank)
                    filled++;

                if (field.FieldType == FieldType.Table)
                {
                    var (requiredColumns, minRows) = ParseTableRequirements(field.ConfigJson);
                    var rows = node as JsonArray;
                    var rowCount = rows?.Count ?? 0;

                    // Mirrors AuthoredDocumentVersionService.ValidateTable: minRows is an INDEPENDENT
                    // requirement from field.Required — a Table with minRows=2 and Required=false but
                    // only 1 row is still something finalize will reject, so it must count as
                    // requiredMissing here too (review-fix contract, todos/086 P3 #1). At most one
                    // increment per field regardless of how many of the two conditions fire.
                    if ((field.Required && blank) || (minRows is int min && rowCount < min))
                        requiredMissing++;

                    if (requiredColumns.Count > 0 && rows != null)
                    {
                        foreach (var rowNode in rows)
                        {
                            if (rowNode is not JsonObject row)
                                continue;
                            foreach (var columnKey in requiredColumns)
                            {
                                row.TryGetPropertyValue(columnKey.ToString(), out var cell);
                                if (IsBlank(cell))
                                    requiredMissing++;
                            }
                        }
                    }
                }
                else if (field.Required && blank)
                {
                    requiredMissing++;
                }
            }
        }

        var percent = total == 0 ? 0 : (int)Math.Round(filled * 100.0 / total, MidpointRounding.AwayFromZero);
        return new DocumentCompletenessModel
        {
            Percent = percent,
            FilledCount = filled,
            TotalCount = total,
            RequiredMissing = requiredMissing
        };
    }

    public async Task<ServiceResult<DocumentCompletenessModel>> ComputeAsync(int instanceId, CancellationToken ct = default)
    {
        var instance = await _context.DocumentInstances
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => new { i.ValuesJson, i.DocumentTemplateVersionId })
            .FirstOrDefaultAsync(ct);
        if (instance == null)
            return ServiceResult<DocumentCompletenessModel>.FailureResult("Document not found.");

        var tree = await _authoring.GetVersionAsync(instance.DocumentTemplateVersionId, ct);
        if (!tree.Success)
            return ServiceResult<DocumentCompletenessModel>.FailureResult(tree.Message ?? "The pinned template version could not be loaded.");

        return ServiceResult<DocumentCompletenessModel>.SuccessResult(Compute(tree.Data!.Sections, instance.ValuesJson));
    }

    // ----------------------------------------------------------------- Blank / parsing rules

    private static JsonObject ParseValues(string? valuesJson)
    {
        if (string.IsNullOrWhiteSpace(valuesJson))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(valuesJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>Mirrors the web's <c>isBlank</c>: missing/null is blank; a string is blank once HTML tags
    /// are stripped and it is empty/whitespace; a boolean is never blank; an array is blank when empty;
    /// anything else (number, object) is never blank.</summary>
    private static bool IsBlank(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return true;
            case JsonArray array:
                return array.Count == 0;
            case JsonValue value:
                if (value.TryGetValue<bool>(out _))
                    return false;
                if (value.TryGetValue<string>(out var s))
                    return HtmlTagPattern.Replace(s, string.Empty).Trim().Length == 0;
                return false;
            default:
                return false;
        }
    }

    private static (List<Guid> RequiredColumns, int? MinRows) ParseTableRequirements(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return (new List<Guid>(), null);
        try
        {
            var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            var requiredColumns = cfg?.Columns.Where(c => c.Required).Select(c => c.ColumnKey).ToList() ?? new List<Guid>();
            return (requiredColumns, cfg?.MinRows);
        }
        catch (JsonException)
        {
            return (new List<Guid>(), null);
        }
    }
}
