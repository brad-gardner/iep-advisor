using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Goals;

internal static class GoalMappers
{
    public static GoalObservationDto MapObservation(GoalObservationModel m) => new()
    {
        Id = m.Id,
        GoalRecordId = m.GoalRecordId,
        ObservedAt = m.ObservedAt,
        Value = m.Value,
        Unit = m.Unit,
        Note = m.Note,
        RecordedByUserId = m.RecordedByUserId
    };

    public static GoalRecordDto MapRecord(GoalRecordModel m) => new()
    {
        Id = m.Id,
        LineageId = m.LineageId,
        VersionId = m.VersionId,
        VersionNumber = m.VersionNumber,
        DocumentTypeKey = m.DocumentTypeKey,
        Domain = m.Domain,
        GoalText = m.GoalText,
        Baseline = m.Baseline,
        TargetCriteria = m.TargetCriteria,
        MeasurementMethod = m.MeasurementMethod,
        Timeframe = m.Timeframe,
        Status = m.Status,
        StatusReason = m.StatusReason,
        ReviewedAt = m.ReviewedAt,
        ProjectedAt = m.ProjectedAt,
        LastObservedAt = m.LastObservedAt,
        StaleAfterDays = m.StaleAfterDays,
        IsStale = m.IsStale,
        Observations = m.Observations.Select(MapObservation).ToList(),
        Trajectory = new GoalTrajectoryDto
        {
            Points = m.Trajectory.Points.Select(p => new GoalTrajectoryPointDto { ObservedAt = p.ObservedAt, Value = p.Value }).ToList(),
            InsufficientData = m.Trajectory.InsufficientData
        }
    };

    public static GoalLineageDto MapLineage(GoalLineageModel m) => new()
    {
        LineageId = m.LineageId,
        Records = m.Records.Select(MapRecord).ToList()
    };
}
