using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface IKnowledgeBaseService
{
    Task<IEnumerable<KnowledgeBaseEntryModel>> SearchAsync(string? query, string? category, string? state, CancellationToken ct = default);
    Task<KnowledgeBaseEntryModel?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<CategoryCount>> GetCategoriesAsync(CancellationToken ct = default);
}
