using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Advocate;

public class AdvocateThreadDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
}

public class AdvocateThreadDetailDto : AdvocateThreadDto
{
    /// <summary>Oldest first.</summary>
    public List<AdvocateMessageDto> Messages { get; set; } = new();
    public string Disclaimer { get; set; } = string.Empty;
}

public class AdvocateMessageDto
{
    public int Id { get; set; }
    /// <summary>User | Assistant</summary>
    public string Role { get; set; } = string.Empty;
    public string ContentMarkdown { get; set; } = string.Empty;
    public List<AdvocateCitationDto> Citations { get; set; } = new();
    public List<AdvocateSuggestionDto> Suggestions { get; set; } = new();
    public bool Truncated { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdvocateCitationDto
{
    /// <summary>kb | child (Phase 3 adds goal, iep, etr, journal, …)</summary>
    public string Kind { get; set; } = string.Empty;
    public int Id { get; set; }
    public string? Label { get; set; }
}

public class AdvocateSuggestionDto
{
    /// <summary>prep_question | journal_entry | open_kb | open_goal</summary>
    public string Kind { get; set; } = string.Empty;
    public string? Text { get; set; }
    public int? Id { get; set; }
    /// <summary>yyyy-MM-dd, journal_entry only.</summary>
    public string? Date { get; set; }
}

public class AdvocateUsageDto
{
    public int Used { get; set; }
    public int Limit { get; set; }
    public bool SubscriptionActive { get; set; }
}

public class CreateAdvocateThreadRequest
{
    [MaxLength(120)] public string? Title { get; set; }
}

public class RenameAdvocateThreadRequest
{
    [Required, MaxLength(120)] public string Title { get; set; } = string.Empty;
}

public class SendAdvocateMessageRequest
{
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;

    /// <summary>Optional launcher context, e.g. <c>iep:12</c> or <c>goal:340</c>.</summary>
    [MaxLength(40)] public string? About { get; set; }
}

// ---- SSE frame payloads (event: delta | tool | done | error) ----

public class AdvocateDeltaFrame
{
    public string Text { get; set; } = string.Empty;
}

public class AdvocateToolFrame
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>started | finished | failed</summary>
    public string Status { get; set; } = string.Empty;
}

public class AdvocateDoneFrame
{
    public int MessageId { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public List<AdvocateCitationDto> Citations { get; set; } = new();
    public List<AdvocateSuggestionDto> Suggestions { get; set; } = new();
    public bool Truncated { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
}

public class AdvocateErrorFrame
{
    /// <summary>unavailable (mid-stream); validation | not_found | forbidden | usage_cap are returned as plain status codes before the stream starts.</summary>
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
