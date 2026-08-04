using System.Text.Json;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Educator authoring of a document instance from a resolved template (State Document Template Engine,
/// Phase 3). Every operation is authorized via <c>IOrgAccessService.CanActOnStudentAsync</c> at
/// <c>Collaborator+</c>. Works identically for every document type (IEP, ETR, 504, …) — the type is
/// data, not code. Finalize / versioning / PDF are Phase 4 and NOT part of this surface.
/// </summary>
public interface IDocumentInstanceService
{
    /// <summary>
    /// Creates a Draft instance for the student: resolves + pins a Published template version for the
    /// student's state and <paramref name="documentTypeId"/> (via <c>ITemplateResolutionService</c>);
    /// returns a blocked failure if none is available. Value-document starts empty (<c>{}</c>). The
    /// result includes the pinned template version tree so the client can render the form.
    /// </summary>
    Task<ServiceResult<DocumentInstanceDetailModel>> CreateAsync(int schoolStudentId, int documentTypeId, int actingUserId, CancellationToken ct = default);

    /// <summary>Returns the instance plus its pinned template version tree and value-document.</summary>
    Task<ServiceResult<DocumentInstanceDetailModel>> GetAsync(int instanceId, int actingUserId, CancellationToken ct = default);

    /// <summary>Lists the student's instances (id, doc type, status, template version, timestamps).</summary>
    Task<ServiceResult<List<DocumentInstanceSummaryModel>>> ListForStudentAsync(int schoolStudentId, int actingUserId, CancellationToken ct = default);

    /// <summary>
    /// Merges a <c>{fieldKey: value}</c> patch into the value-document. Blocked unless Status == Draft.
    /// Uses optimistic concurrency (a stale <paramref name="rowVersion"/> yields a friendly concurrency
    /// error, not a 500). Each provided key is validated against the pinned schema: unknown keys are
    /// stripped, values are type-checked per FieldType, RichText is sanitized. Required-ness,
    /// select-option membership and table row bounds are NOT enforced here (finalize-time, Phase 4).
    /// </summary>
    /// <remarks>Returns a lightweight <see cref="DocumentInstanceValuesModel"/> (normalized values + the
    /// rotated concurrency token), not the full tree — the pinned schema is immutable and already client-side.</remarks>
    Task<ServiceResult<DocumentInstanceValuesModel>> SaveValuesAsync(int instanceId, IReadOnlyDictionary<string, JsonElement> valuesPatch, byte[]? rowVersion, int actingUserId, CancellationToken ct = default);

    /// <summary>Deletes the instance; permitted only while Status == Draft.</summary>
    Task<ServiceResult> DeleteAsync(int instanceId, int actingUserId, CancellationToken ct = default);
}
