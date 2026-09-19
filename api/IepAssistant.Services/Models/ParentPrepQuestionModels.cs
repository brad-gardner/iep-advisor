namespace IepAssistant.Services.Models;

public class ParentPrepQuestionModel
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>"parent" | "advocate"</summary>
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedById { get; set; }
}

/// <summary>
/// Result of adding a question: the row now on the list, and whether it was already there (an add that
/// matched an existing question case-insensitively returns that row instead of creating a duplicate).
/// </summary>
public class ParentPrepQuestionAddResult
{
    public ParentPrepQuestionModel Question { get; set; } = null!;
    public bool AlreadyExisted { get; set; }
}
