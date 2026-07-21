using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Templates;

internal static class DocumentTemplateMappers
{
    public static DocumentTypeDto MapDocumentType(DocumentTypeModel m) => new()
    {
        Id = m.Id,
        Key = m.Key,
        DisplayName = m.DisplayName,
        IsActive = m.IsActive
    };

    public static DocumentTemplateDto MapTemplate(DocumentTemplateModel m) => new()
    {
        Id = m.Id,
        StateCode = m.StateCode,
        DocumentTypeId = m.DocumentTypeId,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        Name = m.Name,
        CreatedAt = m.CreatedAt,
        LatestVersion = m.LatestVersion is null ? null : new DocumentTemplateVersionSummaryDto
        {
            Id = m.LatestVersion.Id,
            VersionNumber = m.LatestVersion.VersionNumber,
            Status = m.LatestVersion.Status.ToString(),
            PublishedAt = m.LatestVersion.PublishedAt
        }
    };
}
