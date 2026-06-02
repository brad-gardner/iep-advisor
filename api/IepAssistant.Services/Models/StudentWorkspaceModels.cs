using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

// ---- Read models ----

public class StudentWorkspaceModel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public List<StudentWorkspaceEntryModel> Entries { get; set; } = new();
}

public class StudentWorkspaceEntryModel
{
    public int Id { get; set; }
    public int StudentWorkspaceId { get; set; }
    public StudentEntryKind EntryKind { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsShareable { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>AI-interview suggestion: a polished first-person statement; NOT auto-saved.</summary>
public class StudentInterviewSuggestionModel
{
    public string Suggestion { get; set; } = string.Empty;
}
