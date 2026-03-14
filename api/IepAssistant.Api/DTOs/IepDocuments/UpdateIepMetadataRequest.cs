using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.IepDocuments;

public class UpdateIepMetadataRequest
{
    public DateTime? IepDate { get; set; }

    [MaxLength(50)]
    public string? MeetingType { get; set; }

    [MaxLength(1000)]
    public string? Attendees { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
