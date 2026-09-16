using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

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

/// <summary>Single shared <see cref="ObligationModel"/> -&gt; <see cref="ObligationDto"/> mapper, used by
/// every controller that surfaces obligations (Home, Obligations, Calendar) so the shape can't drift
/// between them (review-fix contract, todos/086 P3 #4).</summary>
public static class ObligationDtoMapper
{
    public static ObligationDto Map(ObligationModel o) => new()
    {
        Kind = o.Kind,
        DueDate = o.DueDate,
        Status = o.Status,
        SourceLabel = o.SourceLabel,
        OwnerUserId = o.OwnerUserId,
        OwnerName = o.OwnerName,
        SchoolStudentId = o.SchoolStudentId,
        StudentName = o.StudentName,
        DaysUntilDue = o.DaysUntilDue,
        RuleProfile = o.RuleProfile
    };
}
