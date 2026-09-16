namespace IepAssistant.Domain.Entities;

/// <summary>Lifecycle of an <see cref="ExportJob"/> (plan 7, decision 8). Stored as a string.</summary>
public enum ExportJobStatus
{
    Queued,
    Running,
    Completed,
    Failed
}
