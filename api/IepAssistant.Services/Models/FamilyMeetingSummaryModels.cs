using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>Named "Family…" (not <c>MeetingSummaryModel</c>) to avoid colliding with the existing plan-4
/// anonymous-RSVP <see cref="MeetingSummaryModel"/> in this same namespace.</summary>
public class FamilyMeetingSummaryRecipientModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>A post-meeting, family-facing summary (plan 6, decision 6): AI-drafted, human-edited, explicitly sent.</summary>
public class FamilyMeetingSummaryModel
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public MeetingSummaryStatus Status { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime? GeneratedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string? SentByName { get; set; }

    /// <summary>"en" | "es" | null — the language this draft was generated in (multilingual plan
    /// 2026-10-06 phase 3); null means English, including every summary drafted before this was
    /// tracked.</summary>
    public string? GeneratedLanguage { get; set; }

    public List<FamilyMeetingSummaryRecipientModel> Recipients { get; set; } = new();
}
