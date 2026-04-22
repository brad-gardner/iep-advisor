using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.EtrDocuments;

public class UpdateEtrMetadataRequest
{
    public DateTime? EvaluationDate { get; set; }

    [MaxLength(50)]
    public string? EvaluationType { get; set; }

    [MaxLength(50)]
    public string? DocumentState { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
