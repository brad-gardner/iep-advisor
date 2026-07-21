using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

// ---- Read models ----

/// <summary>Active document-type lookup row surfaced for the admin create-template dropdown.</summary>
public class DocumentTypeModel
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// A template plus a summary of its versions (latest version number + status). Used by both the
/// create result and the list view.
/// </summary>
public class DocumentTemplateModel
{
    public int Id { get; set; }
    public string? StateCode { get; set; }
    public int DocumentTypeId { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>The most recent version (by VersionNumber) for this template, if any.</summary>
    public DocumentTemplateVersionSummaryModel? LatestVersion { get; set; }
}

public class DocumentTemplateVersionSummaryModel
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public TemplateVersionStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
}
