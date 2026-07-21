using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Admin-authored State Document Template Engine (Phase 1). All access is gated to platform
/// <c>Admin</c> at the controller; templates carry no student PII. This phase covers creating and
/// listing templates for a <c>(state, documentType)</c> pairing and listing the active document-type
/// lookup rows for the create dropdown.
/// </summary>
public interface IDocumentTemplateService
{
    /// <summary>
    /// Creates a template for <paramref name="stateCode"/> (normalized to 2-letter uppercase, or
    /// null for the default template) + <paramref name="documentTypeId"/>, along with an initial empty
    /// Draft version (VersionNumber = 1). Validates the document type exists and is active, and rejects
    /// a duplicate (StateCode, DocumentTypeId) with a friendly error.
    /// </summary>
    Task<ServiceResult<DocumentTemplateModel>> CreateTemplateAsync(int userId, string? stateCode, int documentTypeId, string name, CancellationToken ct = default);

    /// <summary>Lists all templates with a summary of their latest version.</summary>
    Task<ServiceResult<List<DocumentTemplateModel>>> ListTemplatesAsync(CancellationToken ct = default);

    /// <summary>Lists active document-type lookup rows (for the create-template dropdown).</summary>
    Task<ServiceResult<List<DocumentTypeModel>>> ListDocumentTypesAsync(CancellationToken ct = default);
}
