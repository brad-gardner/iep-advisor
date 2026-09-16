using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

public class CreateDraftResponseModel
{
    public DraftResponseKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
}

/// <summary>At least one of <see cref="StaffReply"/>/<see cref="ResolvedInDraft"/> is required (validated by the service).</summary>
public class ResolveDraftResponseModel
{
    public string? StaffReply { get; set; }
    public bool? ResolvedInDraft { get; set; }
}

public class DraftResponseModel
{
    public int Id { get; set; }
    public int RevisionId { get; set; }
    public int ParentUserId { get; set; }
    public string ParentName { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }

    /// <summary>The target field/row's human-readable label, resolved server-side from the revision's pinned schema + frozen values.</summary>
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
