using System.ComponentModel.DataAnnotations;
using IepAssistant.Domain.Entities;

namespace IepAssistant.Api.DTOs.IepDrafts;

// ---- Requests ----

public class CreateIepDraftRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
}

public class UpsertSectionRequest
{
    [Required]
    public IepSectionKind SectionKind { get; set; }
    public string? RichText { get; set; }
}

public class UpsertGoalRequest
{
    [MaxLength(150)]
    public string? Domain { get; set; }
    public string? GoalText { get; set; }
    [MaxLength(2000)]
    public string? Baseline { get; set; }
    [MaxLength(2000)]
    public string? TargetCriteria { get; set; }
    [MaxLength(1000)]
    public string? MeasurementMethod { get; set; }
    [MaxLength(200)]
    public string? Timeframe { get; set; }
}

public class UpsertServiceLineRequest
{
    [MaxLength(200)]
    public string? ServiceType { get; set; }
    [MaxLength(150)]
    public string? Frequency { get; set; }
    [MaxLength(150)]
    public string? Duration { get; set; }
    [MaxLength(200)]
    public string? Location { get; set; }
    [MaxLength(150)]
    public string? ProviderRole { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class UpsertAccommodationRequest
{
    [MaxLength(150)]
    public string? Category { get; set; }
    public string? Text { get; set; }
}

public class UpsertTransitionItemRequest
{
    [MaxLength(200)]
    public string? PostsecondaryGoalArea { get; set; }
    public string? ServicesText { get; set; }
}

// ---- Responses ----

public class IepDraftDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Title { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<SectionDto> Sections { get; set; } = new();
    public List<GoalDto> Goals { get; set; } = new();
    public List<ServiceLineDto> ServiceLines { get; set; } = new();
    public List<AccommodationDto> Accommodations { get; set; } = new();
    public List<TransitionItemDto> TransitionItems { get; set; } = new();
}

public class SectionDto
{
    public int Id { get; set; }
    public int IepDraftId { get; set; }
    public string SectionKind { get; set; } = string.Empty;
    public string? RichText { get; set; }
    public int DisplayOrder { get; set; }
    public Guid LineageId { get; set; }
    public int? LastEditedByUserId { get; set; }
    public DateTime? LastEditedAt { get; set; }
}

public class GoalDto
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

public class ServiceLineDto
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

public class AccommodationDto
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

public class TransitionItemDto
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
