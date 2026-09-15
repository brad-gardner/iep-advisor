using System.Text.Json.Nodes;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>Builds the initial value-document for a new template instance from the student's evidence.</summary>
public interface IDocumentPrefillService
{
    Task<JsonObject> BuildInitialValuesAsync(int documentTemplateVersionId, string documentTypeKey, StudentEvidenceBundle evidence, CancellationToken ct = default);
}
