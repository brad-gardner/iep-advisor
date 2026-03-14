using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.AdvocacyGoals;

public class UpdateAdvocacyGoalRequest
{
    [MinLength(10, ErrorMessage = "Goal text must be at least 10 characters")]
    [MaxLength(500, ErrorMessage = "Goal text must be at most 500 characters")]
    public string? GoalText { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }
}
