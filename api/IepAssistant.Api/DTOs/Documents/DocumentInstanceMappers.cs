using System.Text.Json;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Documents;

internal static class DocumentInstanceMappers
{
    public static DocumentInstanceDetailDto MapDetail(DocumentInstanceDetailModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        DocumentTypeId = m.DocumentTypeId,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        DocumentTemplateVersionId = m.DocumentTemplateVersionId,
        Status = m.Status.ToString(),
        Values = ParseValues(m.ValuesJson),
        RowVersion = m.RowVersion is null ? null : Convert.ToBase64String(m.RowVersion),
        CreatedAt = m.CreatedAt,
        LastEditedAt = m.LastEditedAt,
        LastEditedByUserId = m.LastEditedByUserId,
        TemplateVersion = DocumentTemplateMappers.MapVersionDetail(m.TemplateVersion)
    };

    public static DocumentInstanceValuesDto MapValues(DocumentInstanceValuesModel m) => new()
    {
        Values = ParseValues(m.ValuesJson),
        RowVersion = m.RowVersion is null ? null : Convert.ToBase64String(m.RowVersion)
    };

    public static DocumentInstanceSummaryDto MapSummary(DocumentInstanceSummaryModel m) => new()
    {
        Id = m.Id,
        DocumentTypeId = m.DocumentTypeId,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        Status = m.Status.ToString(),
        DocumentTemplateVersionId = m.DocumentTemplateVersionId,
        TemplateVersionNumber = m.TemplateVersionNumber,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        LastEditedAt = m.LastEditedAt
    };

    /// <summary>Emits the stored value-document as a JSON object; a blank/invalid store becomes <c>{}</c>.</summary>
    private static JsonElement ParseValues(string? valuesJson)
    {
        if (!string.IsNullOrWhiteSpace(valuesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(valuesJson);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // fall through to empty object
            }
        }

        using var empty = JsonDocument.Parse("{}");
        return empty.RootElement.Clone();
    }
}
