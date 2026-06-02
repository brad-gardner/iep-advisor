using IepAssistant.Domain.Entities;

namespace IepAssistant.Services.Models;

// ---- Read models ----

public class IepVersionModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int SourceDraftId { get; set; }
    public int VersionNumber { get; set; }
    public IepDocumentType DocumentType { get; set; }
    public string? Title { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }

    public PdfRenderStatus? PdfRenderStatus { get; set; }
    public string? PdfBlobUri { get; set; }
    public DateTime? PdfRenderedAt { get; set; }

    public List<IepVersionSectionModel> Sections { get; set; } = new();
    public List<IepVersionGoalModel> Goals { get; set; } = new();
    public List<IepVersionServiceLineModel> ServiceLines { get; set; } = new();
    public List<IepVersionAccommodationModel> Accommodations { get; set; } = new();
    public List<IepVersionTransitionItemModel> TransitionItems { get; set; } = new();
}

public class IepVersionSectionModel
{
    public int Id { get; set; }
    public int IepVersionId { get; set; }
    public IepSectionKind SectionKind { get; set; }
    public string? RichText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionGoalModel
{
    public int Id { get; set; }
    public int IepVersionId { get; set; }
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionServiceLineModel
{
    public int Id { get; set; }
    public int IepVersionId { get; set; }
    public string? ServiceType { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Location { get; set; }
    public string? ProviderRole { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionAccommodationModel
{
    public int Id { get; set; }
    public int IepVersionId { get; set; }
    public string? Category { get; set; }
    public string? Text { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionTransitionItemModel
{
    public int Id { get; set; }
    public int IepVersionId { get; set; }
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

/// <summary>PDF availability for a version. Url is set only when RenderStatus is Rendered.</summary>
public class IepVersionPdfStatusModel
{
    public int VersionId { get; set; }
    public PdfRenderStatus RenderStatus { get; set; }
    public string? Url { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Lightweight summary returned by list endpoints and by FinalizeAsync.</summary>
public class IepVersionSummaryModel
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int SourceDraftId { get; set; }
    public int VersionNumber { get; set; }
    public IepDocumentType DocumentType { get; set; }
    public string? Title { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }
    public PdfRenderStatus? PdfRenderStatus { get; set; }
}
