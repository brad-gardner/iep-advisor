using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class ParentContributionModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public ParentContributionKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SaveParentContributionModel
{
    public ParentContributionKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsShared { get; set; }
}
