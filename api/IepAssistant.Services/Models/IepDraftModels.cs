using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

// ---- Read models ----

public class IepDraftModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public IepDraftStatus Status { get; set; }
    public IepDocumentType DocumentType { get; set; }
    public string? Title { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<IepDraftSectionModel> Sections { get; set; } = new();
    public List<IepDraftGoalModel> Goals { get; set; } = new();
    public List<IepDraftServiceLineModel> ServiceLines { get; set; } = new();
    public List<IepDraftAccommodationModel> Accommodations { get; set; } = new();
    public List<IepDraftTransitionItemModel> TransitionItems { get; set; } = new();
}

public class IepDraftSectionModel
{
    public int Id { get; set; }
    public int IepDraftId { get; set; }
    public IepSectionKind SectionKind { get; set; }
    public string? RichText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

public class IepDraftGoalModel
{
    public int Id { get; set; }
    public int IepDraftId { get; set; }
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

public class IepDraftServiceLineModel
{
    public int Id { get; set; }
    public int IepDraftId { get; set; }
    public string? ServiceType { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Location { get; set; }
    public string? ProviderRole { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

public class IepDraftAccommodationModel
{
    public int Id { get; set; }
    public int IepDraftId { get; set; }
    public string? Category { get; set; }
    public string? Text { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

public class IepDraftTransitionItemModel
{
    public int Id { get; set; }
    public int IepDraftId { get; set; }
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

// ---- Write models (input) ----

public class UpsertIepDraftSectionModel
{
    public IepSectionKind SectionKind { get; set; }
    public string? RichText { get; set; }
}

public class UpsertIepDraftGoalModel
{
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
}

public class UpsertIepDraftServiceLineModel
{
    public string? ServiceType { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Location { get; set; }
    public string? ProviderRole { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpsertIepDraftAccommodationModel
{
    public string? Category { get; set; }
    public string? Text { get; set; }
}

public class UpsertIepDraftTransitionItemModel
{
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
}
