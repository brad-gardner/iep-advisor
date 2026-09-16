using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

/// <summary>One recipient a share would notify: an accepted, active family user or the student's own account.</summary>
public class ShareRecipientModel
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>"Parent" | "Student".</summary>
    public string Relationship { get; set; } = string.Empty;
}

public class RecipientPreviewModel
{
    public List<ShareRecipientModel> Recipients { get; set; } = new();
    public bool PolicyEnabled { get; set; }
    public DateTime? LastSharedAt { get; set; }
    public int? WillSupersedeRevision { get; set; }
}

/// <summary>A single added/removed/changed row in a <see cref="ChangeSummaryModel"/>.</summary>
public class ChangeRowModel
{
    public Guid FieldKey { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string RowId { get; set; } = string.Empty;

    /// <summary>The row's primary-column text (e.g. the goal text, the service type).</summary>
    public string Label { get; set; } = string.Empty;
}

public class ChangeFieldModel
{
    public Guid FieldKey { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
}

/// <summary>
/// Semantic diff between two value-documents keyed by `_rowId` for row-block fields (goals, services,
/// accommodations, transition, …) and by field key for narrative/scalar fields (plan 6, decision 1).
/// </summary>
public class ChangeSummaryModel
{
    public List<ChangeRowModel> AddedRows { get; set; } = new();
    public List<ChangeRowModel> RemovedRows { get; set; } = new();
    public List<ChangeRowModel> ChangedRows { get; set; } = new();
    public List<ChangeFieldModel> ChangedFields { get; set; } = new();
    public string SummaryText { get; set; } = string.Empty;

    public bool IsEmpty => AddedRows.Count == 0 && RemovedRows.Count == 0 && ChangedRows.Count == 0 && ChangedFields.Count == 0;
}

public class AcknowledgementModel
{
    public string ParentName { get; set; } = string.Empty;
    public DateTime AcknowledgedAt { get; set; }
}

public class SharedDraftRevisionModel
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
    public ChangeSummaryModel? ChangeSummary { get; set; }

    /// <summary>The asking parent's own acknowledgement stamp (parent-view callers only; null for a staff view).</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Every family member's acknowledgement (staff-view callers only; empty for a parent view).</summary>
    public List<AcknowledgementModel> Acknowledgements { get; set; } = new();

    public int OpenResponseCount { get; set; }
    public int TemplateVersionId { get; set; }
}

/// <summary>A revision plus its frozen values and pinned template schema (for the parent reading view / staff detail).</summary>
public class SharedDraftRevisionDetailModel : SharedDraftRevisionModel
{
    public string ValuesJson { get; set; } = "{}";
    public TemplateVersionDetailModel TemplateVersion { get; set; } = new();
}

/// <summary>Staff converge view for one instance (plan 6, decision 5).</summary>
public class ConvergeModel
{
    public int InstanceId { get; set; }
    public SharedDraftRevisionModel? LatestRevision { get; set; }
    public List<DraftResponseModel> OpenResponses { get; set; } = new();
    public List<DraftResponseModel> ResolvedResponses { get; set; } = new();

    /// <summary>Diff of the LIVE draft vs. the latest shared revision (what "Share again" would produce); null if nothing has changed or nothing has ever been shared.</summary>
    public ChangeSummaryModel? ChangesSinceShare { get; set; }
    public List<AcknowledgementModel> Acknowledgements { get; set; } = new();
    public bool CanShare { get; set; }
    public bool PolicyEnabled { get; set; }
}
