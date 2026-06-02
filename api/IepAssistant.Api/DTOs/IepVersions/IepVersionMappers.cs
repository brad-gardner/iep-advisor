using IepAssistant.Services.Models;

namespace IepAssistant.Api.DTOs.IepVersions;

internal static class IepVersionMappers
{
    public static IepVersionSummaryDto MapSummary(IepVersionSummaryModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        SourceDraftId = m.SourceDraftId,
        VersionNumber = m.VersionNumber,
        DocumentType = m.DocumentType.ToString(),
        Title = m.Title,
        EffectiveDate = m.EffectiveDate,
        FinalizedByUserId = m.FinalizedByUserId,
        FinalizedAt = m.FinalizedAt,
        PdfRenderStatus = m.PdfRenderStatus?.ToString()
    };

    public static IepVersionDto MapFull(IepVersionModel m) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        SourceDraftId = m.SourceDraftId,
        VersionNumber = m.VersionNumber,
        DocumentType = m.DocumentType.ToString(),
        Title = m.Title,
        EffectiveDate = m.EffectiveDate,
        FinalizedByUserId = m.FinalizedByUserId,
        FinalizedAt = m.FinalizedAt,
        PdfRenderStatus = m.PdfRenderStatus?.ToString(),
        PdfBlobUri = m.PdfBlobUri,
        PdfRenderedAt = m.PdfRenderedAt,
        Sections = m.Sections.Select(s => new IepVersionSectionDto
        {
            Id = s.Id, SectionKind = s.SectionKind.ToString(), RichText = s.RichText,
            DisplayOrder = s.DisplayOrder, LineageId = s.LineageId
        }).ToList(),
        Goals = m.Goals.Select(g => new IepVersionGoalDto
        {
            Id = g.Id, Domain = g.Domain, GoalText = g.GoalText, Baseline = g.Baseline,
            TargetCriteria = g.TargetCriteria, MeasurementMethod = g.MeasurementMethod,
            Timeframe = g.Timeframe, DisplayOrder = g.DisplayOrder, LineageId = g.LineageId
        }).ToList(),
        ServiceLines = m.ServiceLines.Select(s => new IepVersionServiceLineDto
        {
            Id = s.Id, ServiceType = s.ServiceType, Frequency = s.Frequency, Duration = s.Duration,
            Location = s.Location, ProviderRole = s.ProviderRole, StartDate = s.StartDate,
            EndDate = s.EndDate, DisplayOrder = s.DisplayOrder, LineageId = s.LineageId
        }).ToList(),
        Accommodations = m.Accommodations.Select(a => new IepVersionAccommodationDto
        {
            Id = a.Id, Category = a.Category, Text = a.Text,
            DisplayOrder = a.DisplayOrder, LineageId = a.LineageId
        }).ToList(),
        TransitionItems = m.TransitionItems.Select(t => new IepVersionTransitionItemDto
        {
            Id = t.Id, PostsecondaryGoalArea = t.PostsecondaryGoalArea, ServicesText = t.ServicesText,
            DisplayOrder = t.DisplayOrder, LineageId = t.LineageId
        }).ToList()
    };
}
