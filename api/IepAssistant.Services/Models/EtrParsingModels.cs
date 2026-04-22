using System.Text.Json.Serialization;

namespace IepAssistant.Services.Models;

public class ParsedEtr
{
    [JsonPropertyName("sections")]
    public List<ParsedEtrSection> Sections { get; set; } = [];
}

public class ParsedEtrSection
{
    [JsonPropertyName("section_type")]
    public string SectionType { get; set; } = string.Empty;

    [JsonPropertyName("raw_text")]
    public string? RawText { get; set; }

    // parsed_content is a free-form structured object — captured as JsonElement so we can re-serialize
    [JsonPropertyName("parsed_content")]
    public System.Text.Json.JsonElement? ParsedContent { get; set; }

    [JsonPropertyName("display_order")]
    public int DisplayOrder { get; set; }
}

public class EtrSectionModel
{
    public int Id { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public string? RawText { get; set; }
    public string? ParsedContent { get; set; }
    public int DisplayOrder { get; set; }
}
