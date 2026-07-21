using System.ComponentModel.DataAnnotations;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Templates;

// ---- Requests ----
// RowVersion is the base64-encoded optimistic-concurrency token echoed from the last version read;
// optional, but supplying it lets the server reject a stale edit with a friendly concurrency error.

public class AddSectionRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

public class UpdateSectionRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
}

/// <summary>Reorder payload for sections (within a version) or fields (within a section): the full id list in the desired order.</summary>
public class ReorderRequest
{
    [Required]
    public List<int> OrderedIds { get; set; } = new();
    public string? RowVersion { get; set; }
}

public class AddFieldRequest
{
    /// <summary>Field type serialized as a string (Text | RichText | Date | Select | Checkbox | Table).</summary>
    [Required]
    public FieldType FieldType { get; set; }
    [Required, MaxLength(300)]
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    /// <summary>Per-type configuration JSON (Select options, Table columns/row bounds, Date format, Text max length).</summary>
    public string? ConfigJson { get; set; }
    public string? RowVersion { get; set; }
}

public class UpdateFieldRequest
{
    [Required]
    public FieldType FieldType { get; set; }
    [Required, MaxLength(300)]
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? ConfigJson { get; set; }
    public string? RowVersion { get; set; }
}

public class PublishRequest
{
    public string? RowVersion { get; set; }
}

// ---- Responses ----

public class TemplateVersionDetailDto
{
    public int Id { get; set; }
    public int DocumentTemplateId { get; set; }
    public int VersionNumber { get; set; }
    /// <summary>Serialized as a string (Draft | Published).</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    /// <summary>Base64-encoded optimistic-concurrency token to echo on the next edit; null until the version has been edited.</summary>
    public string? RowVersion { get; set; }
    public List<TemplateSectionDto> Sections { get; set; } = new();
}

public class TemplateSectionDto
{
    public int Id { get; set; }
    public Guid SectionKey { get; set; }
    public string Title { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public List<TemplateFieldDto> Fields { get; set; } = new();
}

public class TemplateFieldDto
{
    public int Id { get; set; }
    public Guid FieldKey { get; set; }
    /// <summary>Serialized as a string (Text | RichText | Date | Select | Checkbox | Table).</summary>
    public string FieldType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? ConfigJson { get; set; }
    public int DisplayOrder { get; set; }
}
