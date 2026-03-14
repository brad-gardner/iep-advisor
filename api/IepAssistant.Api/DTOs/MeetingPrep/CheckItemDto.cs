using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.MeetingPrep;

public class CheckItemDto
{
    [Required(ErrorMessage = "Section is required")]
    [RegularExpression("^(questionsToAsk|documentsToBring|redFlagsToRaise|rightsToReference|goalGaps|generalTips)$",
        ErrorMessage = "Invalid section name")]
    public string Section { get; set; } = string.Empty;

    [Range(0, 100, ErrorMessage = "Index must be between 0 and 100")]
    public int Index { get; set; }

    public bool IsChecked { get; set; }
}
