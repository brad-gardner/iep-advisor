namespace IepAssistant.Api.DTOs.AdvocacyGoals;

public class AdvocacyGoalDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
