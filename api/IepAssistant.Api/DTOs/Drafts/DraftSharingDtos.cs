using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.Drafts;

// ---- Recipients / share ----

public class ShareRecipientDto
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>"Parent" | "Student".</summary>
    public string Relationship { get; set; } = string.Empty;
}

public class RecipientPreviewDto
{
    public List<ShareRecipientDto> Recipients { get; set; } = new();
    public bool PolicyEnabled { get; set; }
    public DateTime? LastSharedAt { get; set; }
    public int? WillSupersedeRevision { get; set; }
}

public class ShareDraftRequest
{
    [MaxLength(1000)]
    public string? Message { get; set; }
}

// ---- Change summary ----

public class ChangeRowDto
{
    public Guid FieldKey { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string RowId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class ChangeFieldDto
{
    public Guid FieldKey { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
}

public class ChangeSummaryDto
{
    public List<ChangeRowDto> AddedRows { get; set; } = new();
    public List<ChangeRowDto> RemovedRows { get; set; } = new();
    public List<ChangeRowDto> ChangedRows { get; set; } = new();
    public List<ChangeFieldDto> ChangedFields { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;
}

// ---- Revisions ----

public class AcknowledgementDto
{
    public string ParentName { get; set; } = string.Empty;
    public DateTime AcknowledgedAt { get; set; }
}

public class SharedDraftRevisionDto
{
    public int Id { get; set; }
    public int DocumentInstanceId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string DocumentTypeKey { get; set; } = string.Empty;
    public string DocumentTypeDisplayName { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public SharedDraftStatus Status { get; set; }
    public DateTime SharedAt { get; set; }
    public string SharedByName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public ChangeSummaryDto? ChangeSummary { get; set; }

    /// <summary>This parent's own acknowledgement stamp (parent-view callers); null for a staff view.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Every family acknowledgement (staff-view callers); empty for a parent view.</summary>
    public List<AcknowledgementDto> Acknowledgements { get; set; } = new();

    public int OpenResponseCount { get; set; }
    public int TemplateVersionId { get; set; }
}

/// <summary>A revision plus its frozen values and pinned template schema.</summary>
public class SharedDraftRevisionDetailDto : SharedDraftRevisionDto
{
    /// <summary>The frozen value-document (a JSON object keyed by field FieldKey).</summary>
    public JsonElement Values { get; set; }
    public TemplateVersionDetailDto TemplateVersion { get; set; } = new();
}

// ---- Explanations ----

public class DraftExplanationSectionDto
{
    public string SectionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class DraftExplanationItemDto
{
    public Guid FieldKey { get; set; }
    public string? RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class DraftExplanationDto
{
    public int RevisionId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<DraftExplanationSectionDto> Sections { get; set; } = new();
    public List<DraftExplanationItemDto> Items { get; set; } = new();
    public string Disclaimer { get; set; } = string.Empty;
}

// ---- Private Q&A ----

public class AskQuestionRequest
{
    [Required]
    [MaxLength(1000)]
    public string Question { get; set; } = string.Empty;

    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
}

public class DraftCitationDto
{
    public Guid? FieldKey { get; set; }
    public string? RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}

public class DraftAnswerDto
{
    public int NoteId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<DraftCitationDto> Citations { get; set; } = new();
    public DateTime AnsweredAt { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>PRIVATE to the asking parent — never exposed to any staff route.</summary>
public class ParentDraftNoteDto
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ---- Responses ----

public class CreateResponseRequest
{
    [Required]
    public DraftResponseKind Kind { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
}

/// <summary>At least one of <see cref="StaffReply"/>/<see cref="ResolvedInDraft"/> is required (validated by the service).</summary>
public class ResolveResponseRequest
{
    [MaxLength(2000)]
    public string? StaffReply { get; set; }
    public bool? ResolvedInDraft { get; set; }
}

public class DraftResponseDto
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public int ParentUserId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public string? TargetLabel { get; set; }
    public DraftResponseKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DraftResponseStatus Status { get; set; }
    public string? StaffReply { get; set; }
    public bool ResolvedInDraft { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// ---- Converge ----

public class ConvergeDto
{
    public int InstanceId { get; set; }
    public SharedDraftRevisionDto? LatestRevision { get; set; }
    public List<DraftResponseDto> OpenResponses { get; set; } = new();
    public List<DraftResponseDto> ResolvedResponses { get; set; } = new();
    public ChangeSummaryDto? ChangesSinceShare { get; set; }
    public List<AcknowledgementDto> Acknowledgements { get; set; } = new();
    public bool CanShare { get; set; }
    public bool PolicyEnabled { get; set; }
}
