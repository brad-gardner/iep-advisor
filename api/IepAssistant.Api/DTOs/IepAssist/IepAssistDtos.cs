using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.IepAssist;

// ---- Requests ----

/// <summary>Inline assist request. <see cref="Kind"/> is the string form of AssistKind
/// ("Rewrite" | "Improve" | "SuggestMeasurement").</summary>
public class AssistRequest
{
    [Required]
    public string Kind { get; set; } = string.Empty;
}

public class ChatRequest
{
    [Required]
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatMessageDto
{
    /// <summary>"user" or "assistant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;
    [Required]
    public string Content { get; set; } = string.Empty;
}

// ---- Responses ----

public class AssistResponse
{
    public string Suggestion { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
}
