using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.AdvocacyGoals;

public class ReorderAdvocacyGoalsRequest
{
    [Required]
    public List<ReorderItem> Items { get; set; } = [];
}

public class ReorderItem
{
    public int Id { get; set; }
    public int DisplayOrder { get; set; }
}
