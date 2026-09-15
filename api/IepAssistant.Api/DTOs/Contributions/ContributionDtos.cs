using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Contributions;

public class ParentContributionDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    /// <summary>Strength | Concern | WorksAtHome | Priority | Other</summary>
    public string Kind { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveParentContributionRequest
{
    [Required] public string Kind { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
    public bool IsShared { get; set; }
}
