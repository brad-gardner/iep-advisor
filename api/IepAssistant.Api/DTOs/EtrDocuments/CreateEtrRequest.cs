using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.EtrDocuments;

public class CreateEtrRequest
{
    [Required(ErrorMessage = "Evaluation date is required")]
    public DateTime? EvaluationDate { get; set; }

    [Required(ErrorMessage = "Evaluation type is required")]
    [MaxLength(50)]
    public string EvaluationType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Document state is required")]
    [MaxLength(50)]
    public string DocumentState { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
