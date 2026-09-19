namespace IepAssistant.Api.DTOs.MeetingPrep;

public class ParentPrepQuestionDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    /// <summary>Plain text, at most 500 characters.</summary>
    public string Text { get; set; } = string.Empty;
    public bool IsChecked { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>parent | advocate</summary>
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>
    /// Only set on the add response: <c>true</c> when the text matched a question already on the list
    /// (that row is returned with a 200 instead of creating a duplicate), <c>false</c> on a 201.
    /// </summary>
    public bool? AlreadyExisted { get; set; }
}

public class AddParentPrepQuestionRequest
{
    /// <summary>Markup is stripped; the plain text must be 1–500 characters.</summary>
    public string? Text { get; set; }
    /// <summary>parent (default) | advocate</summary>
    public string? Source { get; set; }
}

public class UpdateParentPrepQuestionRequest
{
    /// <summary>New text (markup stripped, 1–500 characters). Omit to leave unchanged.</summary>
    public string? Text { get; set; }
    /// <summary>Omit to leave unchanged.</summary>
    public bool? IsChecked { get; set; }
}

public class ReorderParentPrepQuestionsRequest
{
    /// <summary>Question ids in the wanted order; each must belong to the child. Unlisted questions follow.</summary>
    public List<int> Ids { get; set; } = new();
}
