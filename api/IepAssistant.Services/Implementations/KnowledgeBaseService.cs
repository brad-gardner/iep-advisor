using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly ApplicationDbContext _context;

    public KnowledgeBaseService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<KnowledgeBaseEntryModel>> SearchAsync(string? query, string? category, string? state, CancellationToken ct = default)
    {
        var dbQuery = _context.KnowledgeBaseEntries
            .AsNoTracking()
            .Where(e => e.IsActive);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query}%";
            dbQuery = dbQuery.Where(e =>
                EF.Functions.Like(e.Title, pattern) ||
                EF.Functions.Like(e.Content, pattern) ||
                (e.Tags != null && EF.Functions.Like(e.Tags, pattern)) ||
                (e.LegalReference != null && EF.Functions.Like(e.LegalReference, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            dbQuery = dbQuery.Where(e => e.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            dbQuery = dbQuery.Where(e => e.State == null || e.State == state);
        }

        var entries = await dbQuery
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.Title)
            .ToListAsync(ct);

        return entries.Select(MapToModel);
    }

    public async Task<KnowledgeBaseEntryModel?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entry = await _context.KnowledgeBaseEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        return entry == null ? null : MapToModel(entry);
    }

    public async Task<IEnumerable<CategoryCount>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return await _context.KnowledgeBaseEntries
            .AsNoTracking()
            .Where(e => e.IsActive)
            .GroupBy(e => e.Category)
            .Select(g => new CategoryCount
            {
                Category = g.Key,
                Count = g.Count()
            })
            .OrderBy(c => c.Category)
            .ToListAsync(ct);
    }

    private static KnowledgeBaseEntryModel MapToModel(Domain.Entities.KnowledgeBaseEntry entry) => new()
    {
        Id = entry.Id,
        Title = entry.Title,
        Content = entry.Content,
        Category = entry.Category,
        LegalReference = entry.LegalReference,
        State = entry.State,
        Tags = string.IsNullOrWhiteSpace(entry.Tags)
            ? []
            : entry.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
    };
}
