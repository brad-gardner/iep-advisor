using System.Text.Json;
using System.Text.Json.Nodes;

namespace IepAssistant.Services.Implementations;

/// <summary>Tolerant parse of a stored value-document JSON string; a blank/invalid store starts fresh (never throws).</summary>
public static class ValueDocumentJson
{
    public static JsonObject Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}
