using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.IepDocuments;

public class CreateIepRequest
{
    [Required(ErrorMessage = "Meeting date is required")]
    public DateTime IepDate { get; set; }

    [Required(ErrorMessage = "Meeting type is required")]
    [MaxLength(50)]
    public string MeetingType { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Attendees { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
