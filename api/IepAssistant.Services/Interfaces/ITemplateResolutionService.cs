using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Resolves which <b>Published</b> template version a new <c>DocumentInstance</c> should pin for a
/// <c>(state, documentType)</c> pairing (State Document Template Engine, Phase 3).
///
/// <para>Resolution order (mirrors the <c>KnowledgeBaseService</c> <c>State == null</c> default
/// precedent): the highest-numbered Published version of the state-specific template, else the highest
/// Published version of the default (state-less) template, else a friendly "no template available"
/// failure — it never silently pins a Draft or a wrong document type.</para>
/// </summary>
public interface ITemplateResolutionService
{
    /// <summary>
    /// Resolves the version to pin. <paramref name="stateCode"/> is normalized to a 2-letter uppercase
    /// code; null/blank (or a malformed code) resolves to the default template only. Returns a failure
    /// with a friendly message when no Published template is available for the document type.
    /// </summary>
    Task<ServiceResult<TemplateResolutionModel>> ResolveAsync(string? stateCode, int documentTypeId, CancellationToken ct = default);
}
