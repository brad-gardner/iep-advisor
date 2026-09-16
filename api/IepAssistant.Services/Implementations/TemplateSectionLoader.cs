using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Loads a pinned template version's section/field tree as plain models (no DbContext tracking), shared by every plan-6 service that needs the schema without the full <c>ITemplateAuthoringService</c> read.</summary>
public static class TemplateSectionLoader
{
    public static async Task<List<TemplateSectionModel>> LoadAsync(ApplicationDbContext context, int templateVersionId, CancellationToken ct)
    {
        var sections = await context.TemplateSections.AsNoTracking()
            .Where(s => s.DocumentTemplateVersionId == templateVersionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        return sections.Select(s => new TemplateSectionModel
        {
            Id = s.Id,
            SectionKey = s.SectionKey,
            Title = s.Title,
            DisplayOrder = s.DisplayOrder,
            Fields = s.Fields.OrderBy(f => f.DisplayOrder).Select(f => new TemplateFieldModel
            {
                Id = f.Id,
                FieldKey = f.FieldKey,
                FieldType = f.FieldType,
                Label = f.Label,
                Required = f.Required,
                ConfigJson = f.ConfigJson,
                DisplayOrder = f.DisplayOrder
            }).ToList()
        }).ToList();
    }
}
