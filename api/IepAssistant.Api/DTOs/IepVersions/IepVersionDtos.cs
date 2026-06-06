namespace IepAssistant.Api.DTOs.IepVersions;

// ---- Requests ----

public class FinalizeIepDraftRequest
{
    public DateTime? EffectiveDate { get; set; }
}

// ---- Responses ----

/// <summary>
/// PDF availability for a version. When <see cref="Url"/> is non-null the PDF is rendered and the
/// URL is a short-lived download link; otherwise the UI shows the status (Pending/Error) instead.
/// </summary>
public class IepVersionPdfStatusDto
{
    public int VersionId { get; set; }
    public string RenderStatus { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime? RenderedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class IepVersionSummaryDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int SourceDraftId { get; set; }
    public int VersionNumber { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }
    public string? PdfRenderStatus { get; set; }
}

public class IepVersionDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int SourceDraftId { get; set; }
    public int VersionNumber { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public int FinalizedByUserId { get; set; }
    public DateTime FinalizedAt { get; set; }

    public string? PdfRenderStatus { get; set; }
    public string? PdfBlobUri { get; set; }
    public DateTime? PdfRenderedAt { get; set; }

    public List<IepVersionSectionDto> Sections { get; set; } = new();
    public List<IepVersionGoalDto> Goals { get; set; } = new();
    public List<IepVersionServiceLineDto> ServiceLines { get; set; } = new();
    public List<IepVersionAccommodationDto> Accommodations { get; set; } = new();
    public List<IepVersionTransitionItemDto> TransitionItems { get; set; } = new();
}

public class IepVersionSectionDto
{
    public int Id { get; set; }
    public string SectionKind { get; set; } = string.Empty;
    public string? RichText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionGoalDto
{
    public int Id { get; set; }
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    public string? Baseline { get; set; }
    public string? TargetCriteria { get; set; }
    public string? MeasurementMethod { get; set; }
    public string? Timeframe { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionServiceLineDto
{
    public int Id { get; set; }
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

public class IepVersionAccommodationDto
{
    public int Id { get; set; }
    public string? Category { get; set; }
    public string? Text { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}

public class IepVersionTransitionItemDto
{
    public int Id { get; set; }
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
}
