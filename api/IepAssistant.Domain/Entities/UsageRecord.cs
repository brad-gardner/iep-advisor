namespace IepAssistant.Domain.Entities;

public class UsageRecord : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>Nullable since plan 6: a staff-triggered operation (e.g. a meeting summary for a student
    /// with no linked family) has no parent-side child profile to attribute the row to.</summary>
    public int? ChildProfileId { get; set; }

    /// <summary>Plan 6: set when the operation is billed to a school/district (e.g. AI draft explanations
    /// and questions, meeting summaries) rather than — or in addition to — the parent's own subscription,
    /// so district-sponsored usage never counts against <c>SubscriptionService.CanPerformAnalysisAsync</c>.</summary>
    public int? DistrictId { get; set; }

    public string OperationType { get; set; } = string.Empty; // "analysis", "meeting_prep", "draft_explanation", "draft_question", "meeting_summary"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public ChildProfile? ChildProfile { get; set; }
    public District? District { get; set; }
}
