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

// ---- Authoring / full-tree models (Phase 2) ----

/// <summary>
/// A template version with its full section/field tree — the form-schema PREVIEW returned by the
/// authoring reads and after every mutation. <see cref="RowVersion"/> is the optimistic-concurrency
/// token the client echoes back on the next edit.
/// </summary>
public class TemplateVersionDetailModel
{
    public int Id { get; set; }
    public int DocumentTemplateId { get; set; }
    public int VersionNumber { get; set; }
    public TemplateVersionStatus Status { get; set; }
    public DateTime? PublishedAt { get; set; }
    public byte[]? RowVersion { get; set; }
    public List<TemplateSectionModel> Sections { get; set; } = new();
}

public class TemplateSectionModel
{
    public int Id { get; set; }
    public Guid SectionKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<TemplateFieldModel> Fields { get; set; } = new();
}

public class TemplateFieldModel
{
    public int Id { get; set; }
    public Guid FieldKey { get; set; }
    public FieldType FieldType { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? ConfigJson { get; set; }
    public int DisplayOrder { get; set; }
}
