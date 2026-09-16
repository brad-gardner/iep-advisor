namespace IepAssistant.Services.Models;

public class AskDraftQuestionModel
{
    public string Question { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
}

public class DraftCitationModel
{
    public Guid? FieldKey { get; set; }
    public string? RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}

/// <summary>The answer to one private parent question (plan 6, decision 3) — persisted as a <see cref="Domain.Entities.ParentDraftNote"/>.</summary>
public class DraftAnswerModel
{
    public int NoteId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<DraftCitationModel> Citations { get; set; } = new();
    public DateTime AnsweredAt { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>A parent's own private note (question + answer). Never exposed to staff.</summary>
public class ParentDraftNoteModel
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public DateTime CreatedAt { get; set; }
}
