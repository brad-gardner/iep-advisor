using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Obligations;

public class ObligationDto
{
    public ObligationKind Kind { get; set; }
    public DateTime? DueDate { get; set; }
    public ObligationStatus Status { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public int? OwnerUserId { get; set; }
    public string? OwnerName { get; set; }
    public int SchoolStudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? DaysUntilDue { get; set; }
    public string RuleProfile { get; set; } = string.Empty;
}
