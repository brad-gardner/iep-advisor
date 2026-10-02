namespace IepAssistant.Domain.Entities;

/// <summary>
/// Per-source execution status within an <see cref="AnalysisRun"/>. Mirrors
/// <see cref="AnalysisRunStatus"/>'s values deliberately (one Claude call per source can fail
/// independently of the run as a whole), but is a distinct type: a source's lifecycle is driven by
/// <c>AnalysisRunService.ExecuteRunAsync</c>'s per-source loop, not by the run's own status.
/// </summary>
public enum AnalysisRunSourceStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Error = 3
}
