using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.IepAssist;

/// <summary>Assist request for a template document field (and, for Table fields, one row).</summary>
public class DocumentAssistRequest
{
    [Required]
    public Guid FieldKey { get; set; }

    /// <summary>Required for Table fields — the row's stable <c>_rowId</c>.</summary>
    public Guid? RowId { get; set; }

    /// <summary>"Rewrite" | "Improve" | "SuggestMeasurement".</summary>
    [Required]
    public string Kind { get; set; } = string.Empty;
}
