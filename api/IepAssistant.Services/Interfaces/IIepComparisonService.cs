using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IIepComparisonService
{
    Task<TimelineResult?> GetTimelineAsync(int childId, int userId, CancellationToken ct = default);
    Task<ComparisonResult?> CompareAsync(int iepId, int otherIepId, int userId, CancellationToken ct = default);
}
