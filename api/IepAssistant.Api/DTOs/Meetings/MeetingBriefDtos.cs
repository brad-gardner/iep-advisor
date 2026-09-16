using System.ComponentModel.DataAnnotations;
using IepAssistant.Api.DTOs.Drafts;
using IepAssistant.Api.DTOs.FamilyContact;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Meetings;

// ---- Brief ----

public class BriefSourceDto
{
    public BriefSourceKind Kind { get; set; }
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class ResourceCommitmentDto
{
    public Guid FieldKey { get; set; }
    public string? RowId { get; set; }
    public string Label { get; set; } = string.Empty;
    public ResourceCommitmentKind Kind { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public class BriefChecklistItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool? Satisfied { get; set; }
    public string? Detail { get; set; }
}

public class MeetingBriefDto
{
    public int MeetingId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public BriefSourceDto? Source { get; set; }
    public string Summary { get; set; } = string.Empty;
    public ChangeSummaryDto? Changes { get; set; }
    public List<ResourceCommitmentDto> ResourceCommitments { get; set; } = new();
    public List<BriefChecklistItemDto> Checklist { get; set; } = new();
    public List<DraftResponseDto> OpenFamilyResponses { get; set; } = new();
    public List<OfflineFamilyInputDto> OfflineInput { get; set; } = new();
    public List<FamilyContactAttemptDto> ContactAttempts { get; set; } = new();
    public string Disclaimer { get; set; } = string.Empty;
}

// ---- Decisions ----

public class MeetingDecisionDto
{
    public int Id { get; set; }
    public int MeetingId { get; set; }
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public string? TargetLabel { get; set; }
    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }
    public int RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public class CreateMeetingDecisionRequest
{
    public Guid? TargetFieldKey { get; set; }
    [MaxLength(64)] public string? TargetRowId { get; set; }
    [MaxLength(500)] public string? TargetLabel { get; set; }
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
    [Required] public MeetingDecisionOutcome? Outcome { get; set; }
}

public class UpdateMeetingDecisionRequest
{
    [Required, MaxLength(2000)] public string Text { get; set; } = string.Empty;
    [Required] public MeetingDecisionOutcome? Outcome { get; set; }
}

public class ProposedEditDto
{
    public int DecisionId { get; set; }
    public int MeetingId { get; set; }
    public string MeetingTitle { get; set; } = string.Empty;
    public Guid? TargetFieldKey { get; set; }
    public string? TargetRowId { get; set; }
    public string? TargetLabel { get; set; }
    public string Text { get; set; } = string.Empty;
    public MeetingDecisionOutcome Outcome { get; set; }
    public DateTime RecordedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
}

internal static class MeetingBriefMappers
{
    public static MeetingBriefDto MapBrief(MeetingBriefModel m) => new()
    {
        MeetingId = m.MeetingId,
        GeneratedAt = m.GeneratedAt,
        Source = m.Source == null ? null : new BriefSourceDto { Kind = m.Source.Kind, Id = m.Source.Id, Label = m.Source.Label },
        Summary = m.Summary,
        Changes = DraftSharingMappers.MapChangeSummary(m.Changes),
        ResourceCommitments = m.ResourceCommitments.Select(r => new ResourceCommitmentDto
        {
            FieldKey = r.FieldKey, RowId = r.RowId, Label = r.Label, Kind = r.Kind, Detail = r.Detail
        }).ToList(),
        Checklist = m.Checklist.Select(c => new BriefChecklistItemDto { Key = c.Key, Label = c.Label, Satisfied = c.Satisfied, Detail = c.Detail }).ToList(),
        OpenFamilyResponses = m.OpenFamilyResponses.Select(DraftSharingMappers.MapResponse).ToList(),
        OfflineInput = m.OfflineInput.Select(FamilyContactMappers.MapInput).ToList(),
        ContactAttempts = m.ContactAttempts.Select(FamilyContactMappers.MapAttempt).ToList(),
        Disclaimer = m.Disclaimer
    };

    public static MeetingDecisionDto MapDecision(MeetingDecisionModel m) => new()
    {
        Id = m.Id,
        MeetingId = m.MeetingId,
        TargetFieldKey = m.TargetFieldKey,
        TargetRowId = m.TargetRowId,
        TargetLabel = m.TargetLabel,
        Text = m.Text,
        Outcome = m.Outcome,
        RecordedByUserId = m.RecordedByUserId,
        RecordedByName = m.RecordedByName,
        CreatedAt = m.CreatedAt,
        AppliedAt = m.AppliedAt
    };

    public static ProposedEditDto MapProposedEdit(ProposedEditModel m) => new()
    {
        DecisionId = m.DecisionId,
        MeetingId = m.MeetingId,
        MeetingTitle = m.MeetingTitle,
        TargetFieldKey = m.TargetFieldKey,
        TargetRowId = m.TargetRowId,
        TargetLabel = m.TargetLabel,
        Text = m.Text,
        Outcome = m.Outcome,
        RecordedAt = m.RecordedAt,
        AppliedAt = m.AppliedAt
    };
}
