using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.AnalysisRuns;

public class CreateAnalysisRunRequest
{
    [Required]
    [MaxLength(5, ErrorMessage = "Choose up to 5 documents for one analysis.")]
    public List<AnalysisRunSourceRefDto> Sources { get; set; } = [];
}

public class AnalysisRunSourceRefDto
{
    [Required]
    public string SourceType { get; set; } = string.Empty; // IepDocument | EtrDocument | ProgressReport
    public int SourceId { get; set; }
}
