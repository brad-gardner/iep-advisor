using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Templates;

// ---- Requests ----

public class CreateDocumentTemplateRequest
{
    /// <summary>2-letter state code (normalized server-side), or null/blank for the default template.</summary>
    [MaxLength(2)]
    public string? StateCode { get; set; }

    [Required]
    public int DocumentTypeId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}

// ---- Responses ----

public class DocumentTypeDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class DocumentTemplateDto
{
    public int Id { get; set; }
    public string? StateCode { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DocumentTemplateVersionSummaryDto? LatestVersion { get; set; }
}

public class DocumentTemplateVersionSummaryDto
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    /// <summary>Serialized as a string (Draft | Published).</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
}
