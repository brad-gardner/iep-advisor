using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.StudentWorkspace;

// ---- Responses ----

public class StudentWorkspaceDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public List<StudentWorkspaceEntryDto> Entries { get; set; } = new();
}

public class StudentWorkspaceEntryDto
{
    public int Id { get; set; }
    public string EntryKind { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsShareable { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class StudentInterviewSuggestionDto
{
    public string Suggestion { get; set; } = string.Empty;
}

// ---- Requests ----

public class CreateWorkspaceEntryRequest
{
    /// <summary>One of: Strength, Interest, AccommodationRequest, MeetingStatement, AiInterviewAnswer.</summary>
    [Required]
    public string EntryKind { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Private (false) until the student chooses to share.</summary>
    public bool IsShareable { get; set; }
}

public class UpdateWorkspaceEntryRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsShareable { get; set; }
}

public class StudentInterviewRequest
{
    [Required]
    public string Prompt { get; set; } = string.Empty;
}
