using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class GoalObservationModel
{
    public int Id { get; set; }
    public int GoalRecordId { get; set; }
    public DateTime ObservedAt { get; set; }
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
}

public class GoalTrajectoryPointModel
{
    public DateTime ObservedAt { get; set; }
    public decimal Value { get; set; }
}

/// <summary>The last 12 numeric observations, ascending, plus whether there are too few (&lt; 2) to plot honestly.</summary>
public class GoalTrajectoryModel
{
    public List<GoalTrajectoryPointModel> Points { get; set; } = new();
    public bool InsufficientData { get; set; }
}

public class GoalRecordModel
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
    public List<GoalObservationModel> Observations { get; set; } = new();
    public GoalTrajectoryModel Trajectory { get; set; } = new();
}

/// <summary>One goal's full history across finalizes/amendments, newest record first.</summary>
public class GoalLineageModel
{
    public Guid LineageId { get; set; }
    public List<GoalRecordModel> Records { get; set; } = new();
}

public class CreateGoalObservationModel
{
    public DateTime? ObservedAt { get; set; }
    public decimal? Value { get; set; }
    public string? Unit { get; set; }
    public string? Note { get; set; }
}

public class UpdateGoalStatusModel
{
    public GoalRecordStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class CreateGoalRetirementModel
{
    public Guid LineageId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
