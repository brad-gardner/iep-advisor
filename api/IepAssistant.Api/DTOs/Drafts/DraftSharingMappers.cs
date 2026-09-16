using System.Text.Json;
using IepAssistant.Api.DTOs.Templates;
using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.Drafts;

internal static class DraftSharingMappers
{
    public static RecipientPreviewDto MapPreview(RecipientPreviewModel m) => new()
    {
        Recipients = m.Recipients.Select(r => new ShareRecipientDto
        {
            UserId = r.UserId,
            DisplayName = r.DisplayName,
            Email = r.Email,
            Relationship = r.Relationship
        }).ToList(),
        PolicyEnabled = m.PolicyEnabled,
        LastSharedAt = m.LastSharedAt,
        WillSupersedeRevision = m.WillSupersedeRevision
    };

    public static ChangeSummaryDto? MapChangeSummary(ChangeSummaryModel? m) => m == null ? null : new ChangeSummaryDto
    {
        AddedRows = m.AddedRows.Select(MapChangeRow).ToList(),
        RemovedRows = m.RemovedRows.Select(MapChangeRow).ToList(),
        ChangedRows = m.ChangedRows.Select(MapChangeRow).ToList(),
        ChangedFields = m.ChangedFields.Select(f => new ChangeFieldDto { FieldKey = f.FieldKey, FieldLabel = f.FieldLabel }).ToList(),
        SummaryText = m.SummaryText
    };

    private static ChangeRowDto MapChangeRow(ChangeRowModel r) => new()
    {
        FieldKey = r.FieldKey,
        FieldLabel = r.FieldLabel,
        RowId = r.RowId,
        Label = r.Label
    };

    public static SharedDraftRevisionDto MapRevision(SharedDraftRevisionModel m) => new()
    {
        Id = m.Id,
        DocumentInstanceId = m.DocumentInstanceId,
        StudentId = m.StudentId,
        StudentName = m.StudentName,
        DocumentTypeKey = m.DocumentTypeKey,
        DocumentTypeDisplayName = m.DocumentTypeDisplayName,
        RevisionNumber = m.RevisionNumber,
        Status = m.Status,
        SharedAt = m.SharedAt,
        SharedByName = m.SharedByName,
        Message = m.Message,
        WithdrawnAt = m.WithdrawnAt,
        ChangeSummary = MapChangeSummary(m.ChangeSummary),
        AcknowledgedAt = m.AcknowledgedAt,
        Acknowledgements = m.Acknowledgements.Select(a => new AcknowledgementDto { ParentName = a.ParentName, AcknowledgedAt = a.AcknowledgedAt }).ToList(),
        OpenResponseCount = m.OpenResponseCount,
        TemplateVersionId = m.TemplateVersionId
    };

    public static SharedDraftRevisionDetailDto MapDetail(SharedDraftRevisionDetailModel m)
    {
        var revision = MapRevision(m);
        return new SharedDraftRevisionDetailDto
        {
            Id = revision.Id,
            DocumentInstanceId = revision.DocumentInstanceId,
            StudentId = revision.StudentId,
            StudentName = revision.StudentName,
            DocumentTypeKey = revision.DocumentTypeKey,
            DocumentTypeDisplayName = revision.DocumentTypeDisplayName,
            RevisionNumber = revision.RevisionNumber,
            Status = revision.Status,
            SharedAt = revision.SharedAt,
            SharedByName = revision.SharedByName,
            Message = revision.Message,
            WithdrawnAt = revision.WithdrawnAt,
            ChangeSummary = revision.ChangeSummary,
            AcknowledgedAt = revision.AcknowledgedAt,
            Acknowledgements = revision.Acknowledgements,
            OpenResponseCount = revision.OpenResponseCount,
            TemplateVersionId = revision.TemplateVersionId,
            Values = ParseValues(m.ValuesJson),
            TemplateVersion = DocumentTemplateMappers.MapVersionDetail(m.TemplateVersion)
        };
    }

    public static DraftExplanationDto MapExplanation(DraftExplanationModel m) => new()
    {
        RevisionId = m.RevisionId,
        GeneratedAt = m.GeneratedAt,
        Sections = m.Sections.Select(s => new DraftExplanationSectionDto { SectionId = s.SectionId, Title = s.Title, Explanation = s.Explanation }).ToList(),
        Items = m.Items.Select(i => new DraftExplanationItemDto { FieldKey = i.FieldKey, RowId = i.RowId, Label = i.Label, Explanation = i.Explanation }).ToList(),
        Disclaimer = m.Disclaimer
    };

    public static DraftAnswerDto MapAnswer(DraftAnswerModel m) => new()
    {
        NoteId = m.NoteId,
        Question = m.Question,
        Answer = m.Answer,
        Citations = m.Citations.Select(c => new DraftCitationDto { FieldKey = c.FieldKey, RowId = c.RowId, Label = c.Label, Excerpt = c.Excerpt }).ToList(),
        AnsweredAt = m.AnsweredAt,
        Disclaimer = m.Disclaimer
    };

    public static ParentDraftNoteDto MapNote(ParentDraftNoteModel m) => new()
    {
        Id = m.Id,
        RevisionId = m.RevisionId,
        Question = m.Question,
        Answer = m.Answer,
        TargetFieldKey = m.TargetFieldKey,
        TargetRowId = m.TargetRowId,
        Citations = m.Citations.Select(c => new DraftCitationDto { FieldKey = c.FieldKey, RowId = c.RowId, Label = c.Label, Excerpt = c.Excerpt }).ToList(),
        CreatedAt = m.CreatedAt
    };

    public static DraftResponseDto MapResponse(DraftResponseModel m) => new()
    {
        Id = m.Id,
        RevisionId = m.RevisionId,
        ParentUserId = m.ParentUserId,
        ParentName = m.ParentName,
        TargetFieldKey = m.TargetFieldKey,
        TargetRowId = m.TargetRowId,
        TargetLabel = m.TargetLabel,
        Kind = m.Kind,
        Text = m.Text,
        CreatedAt = m.CreatedAt,
        Status = m.Status,
        StaffReply = m.StaffReply,
        ResolvedInDraft = m.ResolvedInDraft,
        ResolvedByName = m.ResolvedByName,
        ResolvedAt = m.ResolvedAt
    };

    public static ConvergeDto MapConverge(ConvergeModel m) => new()
    {
        InstanceId = m.InstanceId,
        LatestRevision = m.LatestRevision == null ? null : MapRevision(m.LatestRevision),
        OpenResponses = m.OpenResponses.Select(MapResponse).ToList(),
        ResolvedResponses = m.ResolvedResponses.Select(MapResponse).ToList(),
        ChangesSinceShare = MapChangeSummary(m.ChangesSinceShare),
        Acknowledgements = m.Acknowledgements.Select(a => new AcknowledgementDto { ParentName = a.ParentName, AcknowledgedAt = a.AcknowledgedAt }).ToList(),
        CanShare = m.CanShare,
        PolicyEnabled = m.PolicyEnabled
    };

    /// <summary>Emits the frozen value-document as a JSON object; a blank/invalid store becomes <c>{}</c>.</summary>
    private static JsonElement ParseValues(string? valuesJson)
    {
        if (!string.IsNullOrWhiteSpace(valuesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(valuesJson);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // fall through to empty object
            }
        }

        using var empty = JsonDocument.Parse("{}");
        return empty.RootElement.Clone();
    }
}
