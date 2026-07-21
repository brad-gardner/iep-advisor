using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Authoring surface for the State Document Template Engine (Phase 2): an admin builds a template's
/// section/field structure on its <b>Draft</b> version and publishes it into an immutable snapshot.
/// All access is gated to platform <c>Admin</c> at the controller; templates carry no student PII.
///
/// <para>Every mutation operates ONLY on a Draft version (edits to a Published version are rejected —
/// fork first via <see cref="CreateDraftFromPublishedAsync"/>), validates per-<see cref="FieldType"/>
/// config, rotates the version's optimistic-concurrency token, and returns the refreshed full tree
/// (which doubles as the form-schema preview). A stale <paramref name="rowVersion"/> yields a friendly
/// concurrency error rather than a 500.</para>
/// </summary>
public interface ITemplateAuthoringService
{
    /// <summary>Returns the full section/field tree for a version (form-schema preview). No concurrency/status gate.</summary>
    Task<ServiceResult<TemplateVersionDetailModel>> GetVersionAsync(int versionId, CancellationToken ct = default);

    // ---- Sections (Draft only) ----
    Task<ServiceResult<TemplateVersionDetailModel>> AddSectionAsync(int userId, int versionId, string title, byte[]? rowVersion, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionDetailModel>> UpdateSectionAsync(int userId, int sectionId, string title, byte[]? rowVersion, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionDetailModel>> DeleteSectionAsync(int userId, int sectionId, byte[]? rowVersion, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionDetailModel>> ReorderSectionsAsync(int userId, int versionId, IReadOnlyList<int> orderedSectionIds, byte[]? rowVersion, CancellationToken ct = default);

    // ---- Fields (Draft only) ----
    Task<ServiceResult<TemplateVersionDetailModel>> AddFieldAsync(int userId, int sectionId, FieldType fieldType, string label, bool required, string? configJson, byte[]? rowVersion, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionDetailModel>> UpdateFieldAsync(int userId, int fieldId, FieldType fieldType, string label, bool required, string? configJson, byte[]? rowVersion, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionDetailModel>> DeleteFieldAsync(int userId, int fieldId, byte[]? rowVersion, CancellationToken ct = default);
    Task<ServiceResult<TemplateVersionDetailModel>> ReorderFieldsAsync(int userId, int sectionId, IReadOnlyList<int> orderedFieldIds, byte[]? rowVersion, CancellationToken ct = default);

    // ---- Lifecycle ----
    /// <summary>Validates and publishes the template's Draft version (keeps its VersionNumber); makes it immutable.</summary>
    Task<ServiceResult<TemplateVersionDetailModel>> PublishAsync(int userId, int templateId, byte[]? rowVersion, CancellationToken ct = default);

    /// <summary>Forks a new Draft (VersionNumber = max+1) from the latest Published version, copying sections/fields verbatim (same keys). Rejected if a Draft already exists.</summary>
    Task<ServiceResult<TemplateVersionDetailModel>> CreateDraftFromPublishedAsync(int userId, int templateId, CancellationToken ct = default);
}
