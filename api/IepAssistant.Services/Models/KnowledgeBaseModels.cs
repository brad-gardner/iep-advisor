namespace IepAssistant.Services.Models;

public class KnowledgeBaseEntryModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? LegalReference { get; set; }
    public string? State { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class CategoryCount
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}
