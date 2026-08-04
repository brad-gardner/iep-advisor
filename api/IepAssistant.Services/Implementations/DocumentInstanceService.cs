using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Educator authoring of document instances (see <see cref="IDocumentInstanceService"/>). Authorization
/// is delegated to <see cref="IOrgAccessService.CanActOnStudentAsync"/> at <c>Collaborator+</c> for
/// every operation. Value-document edits are validated against the pinned template schema and guarded by
/// a service-rotated optimistic-concurrency token (mirroring <c>TemplateAuthoringService</c>).
/// </summary>
public class DocumentInstanceService : IDocumentInstanceService
{
    /// <summary>Max serialized size of the value-document (cross-cutting G-x.2). ~1 MB of JSON is far beyond any real form.</summary>
    public const int MaxValuesJsonBytes = 1_000_000;

    private const string PermissionMessage = "You do not have permission to access this document.";
    private const string InstanceNotFoundMessage = "Document not found.";
    private const string NotDraftEditMessage = "This document can no longer be edited.";
    private const string NotDraftDeleteMessage = "Only a draft document can be deleted.";
    private const string ConcurrencyMessage = "This document was changed by someone else. Please reload and try again.";
    private const string TooLargeMessage = "This document is too large to save. Please reduce its content.";

    private static readonly JsonSerializerOptions ConfigJsonOptions = TemplateFieldConfigValidator.JsonOptions;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly ITemplateResolutionService _resolution;
    private readonly ITemplateAuthoringService _authoring;
    private readonly IAuditLogger _audit;
    private readonly ILogger<DocumentInstanceService> _logger;

    public DocumentInstanceService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        ITemplateResolutionService resolution,
        ITemplateAuthoringService authoring,
        IAuditLogger audit,
        ILogger<DocumentInstanceService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _resolution = resolution;
        _authoring = authoring;
        _audit = audit;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Create

    public async Task<ServiceResult<DocumentInstanceDetailModel>> CreateAsync(
        int schoolStudentId, int documentTypeId, int actingUserId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, schoolStudentId, AccessRole.Collaborator, ct))
            return Fail(PermissionMessage);

        // Read the student's state (authz already confirmed the student exists + is in scope).
        var stateCode = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => s.StateCode)
            .FirstOrDefaultAsync(ct);

        // Resolve + pin a Published template version. A blocked resolution propagates its friendly message.
        var resolution = await _resolution.ResolveAsync(stateCode, documentTypeId, ct);
        if (!resolution.Success)
            return Fail(resolution.Message!);

        var now = DateTime.UtcNow;
        var instance = new DocumentInstance
        {
            SchoolStudentId = schoolStudentId,
            DocumentTypeId = documentTypeId,
            DocumentTemplateVersionId = resolution.Data!.DocumentTemplateVersionId,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = "{}",
            RowVersion = Guid.NewGuid().ToByteArray(),
            LastEditedByUserId = actingUserId,
            LastEditedAt = now,
            CreatedById = actingUserId,
            UpdatedById = actingUserId
        };

        await _context.DocumentInstances.AddAsync(instance, ct);
        await _context.SaveChangesAsync(ct);

        // FERPA audit on instance create (cross-cutting G-e.4).
        _audit.Record(AuditAction.Edit, actingUserId, "DocumentInstance", instance.Id);
        _logger.LogInformation(
            "User {UserId} created document instance {InstanceId} for student {StudentId} pinning template version {VersionId} (docType {DocumentTypeId}).",
            actingUserId, instance.Id, schoolStudentId, instance.DocumentTemplateVersionId, documentTypeId);

        return await BuildDetailResultAsync(instance.Id, ct);
    }

    // ---------------------------------------------------------------- Read

    public async Task<ServiceResult<DocumentInstanceDetailModel>> GetAsync(
        int instanceId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadHeaderAsync(instanceId, ct);
        if (header == null)
            return Fail(InstanceNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return Fail(PermissionMessage);

        var result = await BuildDetailResultAsync(instanceId, ct);
        if (result.Success)
            _audit.Record(AuditAction.View, actingUserId, "DocumentInstance", instanceId);
        return result;
    }

    public async Task<ServiceResult<List<DocumentInstanceSummaryModel>>> ListForStudentAsync(
        int schoolStudentId, int actingUserId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<List<DocumentInstanceSummaryModel>>.FailureResult(PermissionMessage);

        var rows = await _context.DocumentInstances
            .AsNoTracking()
            .Where(i => i.SchoolStudentId == schoolStudentId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new DocumentInstanceSummaryModel
            {
                Id = i.Id,
                DocumentTypeId = i.DocumentTypeId,
                DocumentTypeKey = i.DocumentType.Key,
                DocumentTypeDisplayName = i.DocumentType.DisplayName,
                Status = i.Status,
                DocumentTemplateVersionId = i.DocumentTemplateVersionId,
                TemplateVersionNumber = i.DocumentTemplateVersion.VersionNumber,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                LastEditedAt = i.LastEditedAt
            })
            .ToListAsync(ct);

        return ServiceResult<List<DocumentInstanceSummaryModel>>.SuccessResult(rows);
    }

    // ---------------------------------------------------------------- Save values

    public async Task<ServiceResult<DocumentInstanceValuesModel>> SaveValuesAsync(
        int instanceId, IReadOnlyDictionary<string, JsonElement> valuesPatch, byte[]? rowVersion,
        int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadHeaderAsync(instanceId, ct);
        if (header == null)
            return FailValues(InstanceNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return FailValues(PermissionMessage);

        // Edits are blocked once the instance leaves Draft (Finalizing/Finalized).
        if (header.Status != DocumentInstanceStatus.Draft)
            return FailValues(NotDraftEditMessage);

        var instance = await _context.DocumentInstances.FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance == null)
            return FailValues(InstanceNotFoundMessage);

        var concurrency = CheckConcurrency(instance.RowVersion, rowVersion);
        if (concurrency != null)
            return FailValues(concurrency);

        // Load the pinned version's fields (denormalized version FK) for schema validation.
        var fieldsByKey = await LoadFieldsByKeyAsync(instance.DocumentTemplateVersionId, ct);

        var merged = ParseValues(instance.ValuesJson);
        var applyError = ApplyPatch(merged, valuesPatch, fieldsByKey);
        if (applyError != null)
            return FailValues(applyError);

        var serialized = merged.ToJsonString();
        if (Encoding.UTF8.GetByteCount(serialized) > MaxValuesJsonBytes)
            return FailValues(TooLargeMessage);

        var now = DateTime.UtcNow;
        instance.ValuesJson = serialized;
        instance.RowVersion = Guid.NewGuid().ToByteArray();
        instance.LastEditedByUserId = actingUserId;
        instance.LastEditedAt = now;
        instance.UpdatedById = actingUserId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FailValues(ConcurrencyMessage);
        }

        _audit.Record(AuditAction.Edit, actingUserId, "DocumentInstance", instanceId);

        // Return only the normalized values + rotated token; the immutable pinned tree stays client-side.
        return ServiceResult<DocumentInstanceValuesModel>.SuccessResult(new DocumentInstanceValuesModel
        {
            ValuesJson = instance.ValuesJson,
            RowVersion = instance.RowVersion
        });
    }

    // ---------------------------------------------------------------- Delete

    public async Task<ServiceResult> DeleteAsync(int instanceId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult.FailureResult(InstanceNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(PermissionMessage);

        if (header.Status != DocumentInstanceStatus.Draft)
            return ServiceResult.FailureResult(NotDraftDeleteMessage);

        var instance = await _context.DocumentInstances.FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance == null)
            return ServiceResult.FailureResult(InstanceNotFoundMessage);

        _context.DocumentInstances.Remove(instance);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion is in the DELETE WHERE clause; a concurrent edit surfaces the same friendly
            // concurrency message rather than a 500 (consistent with SaveValuesAsync).
            return ServiceResult.FailureResult(ConcurrencyMessage);
        }

        _logger.LogInformation("User {UserId} deleted document instance {InstanceId}.", actingUserId, instanceId);
        return ServiceResult.SuccessResult();
    }

    // ---------------------------------------------------------------- Value merge + validation

    /// <summary>Parses the stored value-document into a mutable object; a blank/invalid store starts fresh.</summary>
    private static JsonObject ParseValues(string? valuesJson)
    {
        if (string.IsNullOrWhiteSpace(valuesJson))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(valuesJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    /// <summary>
    /// Merges the patch into <paramref name="target"/> in place. Unknown field keys are silently
    /// stripped; a value whose type does not conform to its field's <see cref="FieldType"/> returns a
    /// friendly error (the whole save is rejected — partial drafts are allowed, wrong types are not).
    /// RichText is sanitized before storing. A JSON null clears a field.
    /// </summary>
    private static string? ApplyPatch(
        JsonObject target, IReadOnlyDictionary<string, JsonElement> patch, IReadOnlyDictionary<Guid, TemplateField> fieldsByKey)
    {
        foreach (var (rawKey, value) in patch)
        {
            // Unknown / non-guid keys are stripped (not persisted).
            if (!Guid.TryParse(rawKey, out var fieldKey) || !fieldsByKey.TryGetValue(fieldKey, out var field))
                continue;

            var (node, error) = CoerceFieldValue(field, value);
            if (error != null)
                return error;

            target[rawKey] = node; // null clears; otherwise the coerced node
        }

        return null;
    }

    /// <summary>Coerces + validates a top-level field value. Returns (node, null) on success or (null, error) on a type mismatch.</summary>
    private static (JsonNode? Node, string? Error) CoerceFieldValue(TemplateField field, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return (null, null); // clear

        switch (field.FieldType)
        {
            case FieldType.RichText:
                if (value.ValueKind != JsonValueKind.String)
                    return (null, TypeError(field.Label, "formatted text"));
                return (JsonValue.Create(RichTextSanitizer.Sanitize(value.GetString())), null);

            case FieldType.Table:
                return CoerceTable(field, value);

            default:
                var (scalar, error) = CoerceScalar(field.FieldType, value, field.Label);
                return (scalar, error);
        }
    }

    /// <summary>Coerces a scalar (non-Table, non-RichText) value per type. Used for top-level fields and table cells.</summary>
    private static (JsonNode? Node, string? Error) CoerceScalar(FieldType type, JsonElement value, string label)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return (null, null);

        switch (type)
        {
            case FieldType.Checkbox:
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return (JsonValue.Create(value.GetBoolean()), null);
                return (null, TypeError(label, "a checkbox (true/false)"));

            case FieldType.Date:
                if (value.ValueKind != JsonValueKind.String)
                    return (null, TypeError(label, "a date"));
                var dateStr = value.GetString();
                if (string.IsNullOrWhiteSpace(dateStr))
                    return (null, null); // blank clears
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return (null, TypeError(label, "a valid date"));
                return (JsonValue.Create(dateStr), null);

            case FieldType.Text:
            case FieldType.Select:
                if (value.ValueKind != JsonValueKind.String)
                    return (null, TypeError(label, "text"));
                return (JsonValue.Create(value.GetString()), null);

            default:
                // RichText/Table are not valid scalar/column types (config validation forbids them in tables).
                return (null, TypeError(label, "a supported value"));
        }
    }

    /// <summary>Coerces a Table value: an array of row objects keyed by columnKey. Unknown columns are stripped; each cell is type-checked by its column type.</summary>
    private static (JsonNode? Node, string? Error) CoerceTable(TemplateField field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return (null, TypeError(field.Label, "a table (list of rows)"));

        var columns = ParseTableColumns(field.ConfigJson);

        var rows = new JsonArray();
        foreach (var rowElement in value.EnumerateArray())
        {
            if (rowElement.ValueKind != JsonValueKind.Object)
                return (null, $"'{field.Label}' has an invalid table row.");

            var row = new JsonObject();
            foreach (var cell in rowElement.EnumerateObject())
            {
                // Strip unknown / non-guid column keys.
                if (!Guid.TryParse(cell.Name, out var columnKey) || !columns.TryGetValue(columnKey, out var columnType))
                    continue;

                var (node, error) = CoerceScalar(columnType, cell.Value, $"{field.Label} column");
                if (error != null)
                    return (null, error);

                row[cell.Name] = node;
            }

            // Skip rows that reduced to nothing (all columns unknown/stripped) so the value-document
            // does not accumulate junk empty-object rows.
            if (row.Count > 0)
                rows.Add(row);
        }

        return (rows, null);
    }

    /// <summary>Parses a Table field's ConfigJson into a columnKey → column FieldType map (empty on any parse issue).</summary>
    private static Dictionary<Guid, FieldType> ParseTableColumns(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return new Dictionary<Guid, FieldType>();
        try
        {
            var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, ConfigJsonOptions);
            if (cfg?.Columns == null)
                return new Dictionary<Guid, FieldType>();

            var map = new Dictionary<Guid, FieldType>();
            foreach (var col in cfg.Columns)
                map[col.ColumnKey] = col.Type;
            return map;
        }
        catch (JsonException)
        {
            return new Dictionary<Guid, FieldType>();
        }
    }

    private static string TypeError(string label, string expected) => $"'{label}' must be {expected}.";

    // ---------------------------------------------------------------- Concurrency

    /// <summary>Manual optimistic-concurrency check against the client token (mirrors TemplateAuthoringService). Null = proceed.</summary>
    private static string? CheckConcurrency(byte[]? currentToken, byte[]? clientToken)
    {
        if (currentToken == null || currentToken.Length == 0)
            return null; // never-rotated row accepts any/no token
        if (clientToken == null || clientToken.Length == 0)
            return null; // no token supplied — EF's WHERE clause still guards a truly concurrent write
        return currentToken.AsSpan().SequenceEqual(clientToken) ? null : ConcurrencyMessage;
    }

    // ---------------------------------------------------------------- Loading + mapping

    private sealed record InstanceHeader(int SchoolStudentId, DocumentInstanceStatus Status);

    private async Task<InstanceHeader?> LoadHeaderAsync(int instanceId, CancellationToken ct) =>
        await _context.DocumentInstances
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => new InstanceHeader(i.SchoolStudentId, i.Status))
            .FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyDictionary<Guid, TemplateField>> LoadFieldsByKeyAsync(int versionId, CancellationToken ct)
    {
        var fields = await _context.TemplateFields
            .AsNoTracking()
            .Where(f => f.DocumentTemplateVersionId == versionId)
            .ToListAsync(ct);

        // FieldKey is unique within a version (enforced in Phase 2), so ToDictionary is safe.
        return fields.ToDictionary(f => f.FieldKey);
    }

    private async Task<ServiceResult<DocumentInstanceDetailModel>> BuildDetailResultAsync(int instanceId, CancellationToken ct)
    {
        var instance = await _context.DocumentInstances
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => new
            {
                i.Id,
                i.SchoolStudentId,
                i.DocumentTypeId,
                DocumentTypeKey = i.DocumentType.Key,
                DocumentTypeDisplayName = i.DocumentType.DisplayName,
                i.DocumentTemplateVersionId,
                i.Status,
                i.ValuesJson,
                i.RowVersion,
                i.CreatedAt,
                i.LastEditedAt,
                i.LastEditedByUserId
            })
            .FirstOrDefaultAsync(ct);

        if (instance == null)
            return Fail(InstanceNotFoundMessage);

        // Reuse the Phase 2 tree builder for the pinned version's section/field schema.
        var tree = await _authoring.GetVersionAsync(instance.DocumentTemplateVersionId, ct);
        if (!tree.Success)
            return Fail(tree.Message ?? "The pinned template version could not be loaded.");

        return ServiceResult<DocumentInstanceDetailModel>.SuccessResult(new DocumentInstanceDetailModel
        {
            Id = instance.Id,
            SchoolStudentId = instance.SchoolStudentId,
            DocumentTypeId = instance.DocumentTypeId,
            DocumentTypeKey = instance.DocumentTypeKey,
            DocumentTypeDisplayName = instance.DocumentTypeDisplayName,
            DocumentTemplateVersionId = instance.DocumentTemplateVersionId,
            Status = instance.Status,
            ValuesJson = instance.ValuesJson,
            RowVersion = instance.RowVersion,
            CreatedAt = instance.CreatedAt,
            LastEditedAt = instance.LastEditedAt,
            LastEditedByUserId = instance.LastEditedByUserId,
            TemplateVersion = tree.Data!
        });
    }

    private static ServiceResult<DocumentInstanceDetailModel> Fail(string message)
        => ServiceResult<DocumentInstanceDetailModel>.FailureResult(message);

    private static ServiceResult<DocumentInstanceValuesModel> FailValues(string message)
        => ServiceResult<DocumentInstanceValuesModel>.FailureResult(message);
}
