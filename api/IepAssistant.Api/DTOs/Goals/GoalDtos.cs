using System.ComponentModel.DataAnnotations;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Goals;

public class GoalObservationDto
{
    public int Id { get; set; }
    public int GoalRecordId { get; set; }
    public DateTime ObservedAt { get; set; }
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
}

public class GoalTrajectoryPointDto
{
    public DateTime ObservedAt { get; set; }
    public decimal Value { get; set; }
}

public class GoalTrajectoryDto
{
    public List<GoalTrajectoryPointDto> Points { get; set; } = new();
    public bool InsufficientData { get; set; }
}

public class GoalRecordDto
{
    public int Id { get; set; }
    public Guid LineageId { get; set; }
    public int VersionId { get; set; }
    public int VersionNumber { get; set; }
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string GoalText { get; set; } = string.Empty;
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public GoalRecordStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime ProjectedAt { get; set; }
    public DateTime? LastObservedAt { get; set; }
    public int StaleAfterDays { get; set; }
    public bool IsStale { get; set; }
    public List<GoalObservationDto> Observations { get; set; } = new();
    public GoalTrajectoryDto Trajectory { get; set; } = new();
}

/// <summary>One goal's full record history across finalizes/amendments, newest record first.</summary>
public class GoalLineageDto
{
    public Guid LineageId { get; set; }
    public List<GoalRecordDto> Records { get; set; } = new();
}

public class CreateGoalObservationRequest
{
    public DateTime? ObservedAt { get; set; }
    public decimal? Value { get; set; }

    [MaxLength(32)]
    public string? Unit { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }
}

public class UpdateGoalStatusRequest
{
    [Required]
    public GoalRecordStatus Status { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }
}

public class CreateGoalRetirementRequest
{
    [Required]
    public Guid LineageId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}
