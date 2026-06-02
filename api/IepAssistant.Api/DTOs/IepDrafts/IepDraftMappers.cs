using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.IepDrafts;

internal static class IepDraftMappers
{
    public static IepDraftDto MapDraft(IepDraftModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        Status = m.Status.ToString(),
        DocumentType = m.DocumentType.ToString(),
        Title = m.Title,
        LastEditedByUserId = m.LastEditedByUserId,
        LastEditedAt = m.LastEditedAt,
        CreatedAt = m.CreatedAt,
        Sections = m.Sections.Select(MapSection).ToList(),
        Goals = m.Goals.Select(MapGoal).ToList(),
        ServiceLines = m.ServiceLines.Select(MapServiceLine).ToList(),
        Accommodations = m.Accommodations.Select(MapAccommodation).ToList(),
        TransitionItems = m.TransitionItems.Select(MapTransitionItem).ToList()
    };

    public static SectionDto MapSection(IepDraftSectionModel m) => new()
    {
        Id = m.Id,
        IepDraftId = m.IepDraftId,
        SectionKind = m.SectionKind.ToString(),
        RichText = m.RichText,
        DisplayOrder = m.DisplayOrder,
        LineageId = m.LineageId,
        LastEditedByUserId = m.LastEditedByUserId,
        LastEditedAt = m.LastEditedAt
    };

    public static GoalDto MapGoal(IepDraftGoalModel m) => new()
    {
        Id = m.Id,
        IepDraftId = m.IepDraftId,
        Domain = m.Domain,
        GoalText = m.GoalText,
        Baseline = m.Baseline,
        TargetCriteria = m.TargetCriteria,
        MeasurementMethod = m.MeasurementMethod,
        Timeframe = m.Timeframe,
        DisplayOrder = m.DisplayOrder,
        LineageId = m.LineageId,
        LastEditedByUserId = m.LastEditedByUserId,
        LastEditedAt = m.LastEditedAt
    };

    public static ServiceLineDto MapServiceLine(IepDraftServiceLineModel m) => new()
    {
        Id = m.Id,
        IepDraftId = m.IepDraftId,
        ServiceType = m.ServiceType,
        Frequency = m.Frequency,
        Duration = m.Duration,
        Location = m.Location,
        ProviderRole = m.ProviderRole,
        StartDate = m.StartDate,
        EndDate = m.EndDate,
        DisplayOrder = m.DisplayOrder,
        LineageId = m.LineageId,
        LastEditedByUserId = m.LastEditedByUserId,
        LastEditedAt = m.LastEditedAt
    };

    public static AccommodationDto MapAccommodation(IepDraftAccommodationModel m) => new()
    {
        Id = m.Id,
        IepDraftId = m.IepDraftId,
        Category = m.Category,
        Text = m.Text,
        DisplayOrder = m.DisplayOrder,
        LineageId = m.LineageId,
        LastEditedByUserId = m.LastEditedByUserId,
        LastEditedAt = m.LastEditedAt
    };

    public static TransitionItemDto MapTransitionItem(IepDraftTransitionItemModel m) => new()
    {
        Id = m.Id,
        IepDraftId = m.IepDraftId,
        PostsecondaryGoalArea = m.PostsecondaryGoalArea,
        ServicesText = m.ServicesText,
        DisplayOrder = m.DisplayOrder,
        LineageId = m.LineageId,
        LastEditedByUserId = m.LastEditedByUserId,
        LastEditedAt = m.LastEditedAt
    };
}
