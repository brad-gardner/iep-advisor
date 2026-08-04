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

    public static TemplateVersionDetailDto MapVersionDetail(TemplateVersionDetailModel m) => new()
    {
        Id = m.Id,
        DocumentTemplateId = m.DocumentTemplateId,
        VersionNumber = m.VersionNumber,
        Status = m.Status.ToString(),
        PublishedAt = m.PublishedAt,
        RowVersion = m.RowVersion is null ? null : Convert.ToBase64String(m.RowVersion),
        Sections = m.Sections.Select(s => new TemplateSectionDto
        {
            Id = s.Id,
            SectionKey = s.SectionKey,
            Title = s.Title,
            DisplayOrder = s.DisplayOrder,
            Fields = s.Fields.Select(f => new TemplateFieldDto
            {
                Id = f.Id,
                FieldKey = f.FieldKey,
                FieldType = f.FieldType.ToString(),
                Label = f.Label,
                Required = f.Required,
                ConfigJson = f.ConfigJson,
                DisplayOrder = f.DisplayOrder
            }).ToList()
        }).ToList()
    };
}
