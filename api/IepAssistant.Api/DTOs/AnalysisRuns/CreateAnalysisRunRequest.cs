using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.AnalysisRuns;

public class CreateAnalysisRunRequest
{
    [Required]
    public List<AnalysisRunSourceRefDto> Sources { get; set; } = [];
}

public class AnalysisRunSourceRefDto
{
    [Required]
    public string SourceType { get; set; } = string.Empty; // IepDocument | EtrDocument | ProgressReport
    public int SourceId { get; set; }
}
