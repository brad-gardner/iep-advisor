namespace IepAssistant.Services.Models;

public class ParentAdvocacyGoalModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateAdvocacyGoalModel
{
    public string GoalText { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class UpdateAdvocacyGoalModel
{
    public string? GoalText { get; set; }
    public string? Category { get; set; }
}

public class ReorderAdvocacyGoalItem
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
}
