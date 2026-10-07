using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
///
/// <para>Multilingual plan (2026-10-06) phase 5: every failure <c>DocumentInstanceController</c> maps to
/// a status carries an explicit <see cref="ServiceErrorKind"/>, propagated from <see cref="_resolution"/>
/// (422 Unprocessable) and <see cref="_authoring"/> (whatever kind it set) rather than re-derived from
/// their message text. A field's configured <c>Label</c> interpolated into a type-mismatch message is
/// district-authored content and is never translated.</para>
/// </summary>
public class DocumentInstanceService : IDocumentInstanceService
{
    /// <summary>Max serialized size of the value-document (cross-cutting G-x.2). ~1 MB of JSON is far beyond any real form.</summary>
    public const int MaxValuesJsonBytes = 1_000_000;

    /// <summary>Max objectives/benchmarks kept per goal row (plan 2026-10-02-002); extras beyond this are dropped, preserving order.</summary>
    private const int MaxObjectives = 20;
    private const int MaxObjectiveDescriptionLength = 2000;
    private const int MaxObjectiveCriteriaLength = 2000;
    private const int MaxObjectiveTargetDateLength = 50;
    private const string OwnerNotTeamMemberWarningCode = "ownerNotTeamMember";

    private static readonly JsonSerializerOptions ConfigJsonOptions = TemplateFieldConfigValidator.JsonOptions;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly ITemplateResolutionService _resolution;
    private readonly ITemplateAuthoringService _authoring;
    private readonly IAuditLogger _audit;
    private readonly ILogger<DocumentInstanceService> _logger;
    private readonly IStudentEvidenceService? _evidence;
    private readonly IDocumentPrefillService? _prefill;
    private readonly IStringLocalizer<Messages> _localizer;

    public DocumentInstanceService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        ITemplateResolutionService resolution,
        ITemplateAuthoringService authoring,
        IAuditLogger audit,
        ILogger<DocumentInstanceService> logger,
        IStringLocalizer<Messages> localizer,
        IStudentEvidenceService? evidence = null,
        IDocumentPrefillService? prefill = null)
    {
        _context = context;
        _orgAccess = orgAccess;
        _resolution = resolution;
        _authoring = authoring;
        _audit = audit;
        _logger = logger;
        _localizer = localizer;
        _evidence = evidence;
        _prefill = prefill;
    }

    // ---------------------------------------------------------------- Create

    public async Task<ServiceResult<DocumentInstanceDetailModel>> CreateAsync(
        int schoolStudentId, int documentTypeId, int actingUserId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, schoolStudentId, AccessRole.Collaborator, ct))
            return Fail(ServiceErrorKind.Forbidden, _localizer["Documents.Permission"]);

        // Resolve the student's state (authz already confirmed the student exists + is in scope):
        // an explicit student state wins, else the school's, else the district's. Students are almost
        // never given a state directly — they inherit the building's — so without this chain every
        // state-specific template would silently fall through to the default.
        var stateCode = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.Id == schoolStudentId)
            .Select(s => s.StateCode ?? s.School.StateCode ?? s.School.District.StateCode)
            .FirstOrDefaultAsync(ct);

        // Resolve + pin a Published template version. A blocked resolution propagates its friendly
        // message AND its ErrorKind (422 Unprocessable) — never re-derived from the message text.
        var resolution = await _resolution.ResolveAsync(stateCode, documentTypeId, ct);
        if (!resolution.Success)
            return Fail(resolution.ErrorKind, resolution.Message!);

        // "Never blank": prefill from the student's evidence. Any failure degrades to an empty draft
        // (logged) — prefill must never block creating a document.
        var initialValues = "{}";
        if (_evidence != null && _prefill != null)
        {
            try
            {
                var typeKey = await _context.DocumentTypes.AsNoTracking()
                    .Where(t => t.Id == documentTypeId).Select(t => t.Key).FirstOrDefaultAsync(ct) ?? string.Empty;
                var bundle = await _evidence.BuildForStaffAsync(actingUserId, schoolStudentId, ct);
                if (bundle.Success && bundle.Data != null)
                {
                    var values = await _prefill.BuildInitialValuesAsync(resolution.Data!.DocumentTemplateVersionId, typeKey, bundle.Data, ct);
                    // Route through the same coercion as a save so rows get ids and metadata is validated —
                    // including the owner-on-team check, so a prefilled owner who has left the student's
                    // team since the source version was finalized is dropped here, not carried in blind.
                    var fields = await LoadFieldsByKeyAsync(resolution.Data.DocumentTemplateVersionId, ct);
                    var activeTeamUserIds = await LoadActiveTeamUserIdsAsync(schoolStudentId, ct);
                    var patch = values.ToDictionary(kv => kv.Key, kv => JsonSerializer.SerializeToElement(kv.Value));
                    var target = new JsonObject();
                    var prefillWarnings = new List<DocumentSaveWarningModel>();
                    var error = ApplyPatch(target, patch, fields, activeTeamUserIds, prefillWarnings, _localizer);
                    if (error == null)
                        initialValues = target.ToJsonString();
                    else
                        _logger.LogWarning("Prefill for student {StudentId} produced invalid values ({Error}); creating an empty draft.", schoolStudentId, error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Prefill failed for student {StudentId}; creating an empty draft.", schoolStudentId);
                initialValues = "{}";
            }
        }

        var now = DateTime.UtcNow;
        var instance = new DocumentInstance
        {
            SchoolStudentId = schoolStudentId,
            DocumentTypeId = documentTypeId,
            DocumentTemplateVersionId = resolution.Data!.DocumentTemplateVersionId,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = initialValues,
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
            return Fail(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return Fail(ServiceErrorKind.Forbidden, _localizer["Documents.Permission"]);

        var result = await BuildDetailResultAsync(instanceId, ct);
        if (result.Success)
            _audit.Record(AuditAction.View, actingUserId, "DocumentInstance", instanceId);
        return result;
    }

    public async Task<ServiceResult<List<DocumentInstanceSummaryModel>>> ListForStudentAsync(
        int schoolStudentId, int actingUserId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<List<DocumentInstanceSummaryModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Documents.Permission"]);

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
            return FailValues(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return FailValues(ServiceErrorKind.Forbidden, _localizer["Documents.Permission"]);

        // Edits are blocked once the instance leaves Draft (Finalizing/Finalized).
        if (header.Status != DocumentInstanceStatus.Draft)
            return FailValues(ServiceErrorKind.Validation, _localizer["Documents.NoLongerEditable"]);

        var instance = await _context.DocumentInstances.FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance == null)
            return FailValues(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        var concurrency = CheckConcurrency(instance.RowVersion, rowVersion, _localizer);
        if (concurrency != null)
            return FailValues(ServiceErrorKind.Conflict, concurrency);

        // Load the pinned version's fields (denormalized version FK) for schema validation.
        var fieldsByKey = await LoadFieldsByKeyAsync(instance.DocumentTemplateVersionId, ct);

        // Active team membership is only needed to validate a `_ownerUserId` on a Table field whose
        // semantic is owner-eligible (goals/services/accommodations/transition); most autosave ticks
        // touch narrative fields and never need this, so the query is skipped unless the patch actually
        // reaches an owner-eligible table (one query per save, only when relevant).
        var activeTeamUserIds = PatchTouchesOwnerEligibleTable(valuesPatch, fieldsByKey)
            ? await LoadActiveTeamUserIdsAsync(header.SchoolStudentId, ct)
            : EmptyUserIdSet;

        var merged = ParseValues(instance.ValuesJson);
        var warnings = new List<DocumentSaveWarningModel>();
        var applyError = ApplyPatch(merged, valuesPatch, fieldsByKey, activeTeamUserIds, warnings, _localizer);
        if (applyError != null)
            return FailValues(ServiceErrorKind.Validation, applyError);

        var serialized = merged.ToJsonString();
        if (Encoding.UTF8.GetByteCount(serialized) > MaxValuesJsonBytes)
            return FailValues(ServiceErrorKind.Validation, _localizer["Documents.TooLargeToSave"]);

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
            return FailValues(ServiceErrorKind.Conflict, _localizer["Documents.ConcurrencyConflict"]);
        }

        _audit.Record(AuditAction.Edit, actingUserId, "DocumentInstance", instanceId);

        // Return only the normalized values + rotated token; the immutable pinned tree stays client-side.
        return ServiceResult<DocumentInstanceValuesModel>.SuccessResult(new DocumentInstanceValuesModel
        {
            ValuesJson = instance.ValuesJson,
            RowVersion = instance.RowVersion,
            Warnings = warnings
        });
    }

    // ---------------------------------------------------------------- Delete

    public async Task<ServiceResult> DeleteAsync(int instanceId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(ServiceErrorKind.Forbidden, _localizer["Documents.Permission"]);

        if (header.Status != DocumentInstanceStatus.Draft)
            return ServiceResult.FailureResult(ServiceErrorKind.Validation, _localizer["Documents.OnlyDraftCanBeDeleted"]);

        var instance = await _context.DocumentInstances.FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance == null)
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        _context.DocumentInstances.Remove(instance);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion is in the DELETE WHERE clause; a concurrent edit surfaces the same friendly
            // concurrency message rather than a 500 (consistent with SaveValuesAsync).
            return ServiceResult.FailureResult(ServiceErrorKind.Conflict, _localizer["Documents.ConcurrencyConflict"]);
        }
        catch (DbUpdateException)
        {
            // Pilot-gates plan, phase 1: SharedDraftRevision -> DocumentInstance is Restrict, not
            // Cascade (SQL Server disallows an immutability trigger on a table with any cascading FK
            // touching it). A Draft-status instance CAN have been shared with the family before
            // someone tries to delete it (sharing is allowed at Draft/Finalizing) — that now surfaces
            // as a friendly refusal instead of silently destroying the family's shared history.
            return ServiceResult.FailureResult(ServiceErrorKind.Validation, _localizer["Documents.SharedDraftBlocksDelete"]);
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
        JsonObject target, IReadOnlyDictionary<string, JsonElement> patch, IReadOnlyDictionary<Guid, TemplateField> fieldsByKey,
        IReadOnlySet<int> activeTeamUserIds, List<DocumentSaveWarningModel> warnings, IStringLocalizer<Messages> localizer)
    {
        foreach (var (rawKey, value) in patch)
        {
            // Unknown / non-guid keys are stripped (not persisted).
            if (!Guid.TryParse(rawKey, out var fieldKey) || !fieldsByKey.TryGetValue(fieldKey, out var field))
                continue;

            var (node, error) = CoerceFieldValue(field, value, activeTeamUserIds, warnings, localizer);
            if (error != null)
                return error;

            target[rawKey] = node; // null clears; otherwise the coerced node
        }

        return null;
    }

    /// <summary>Coerces + validates a top-level field value. Returns (node, null) on success or (null, error) on a type mismatch.</summary>
    private static (JsonNode? Node, string? Error) CoerceFieldValue(
        TemplateField field, JsonElement value, IReadOnlySet<int> activeTeamUserIds, List<DocumentSaveWarningModel> warnings, IStringLocalizer<Messages> localizer)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return (null, null); // clear

        switch (field.FieldType)
        {
            case FieldType.RichText:
                if (value.ValueKind != JsonValueKind.String)
                    return (null, TypeError(localizer, "Documents.FieldMustBeFormattedText", field.Label));
                return (JsonValue.Create(RichTextSanitizer.Sanitize(value.GetString())), null);

            case FieldType.Table:
                return CoerceTable(field, value, activeTeamUserIds, warnings, localizer);

            default:
                var (scalar, error) = CoerceScalar(field.FieldType, value, field.Label, localizer);
                return (scalar, error);
        }
    }

    /// <summary>Coerces a scalar (non-Table, non-RichText) value per type. Used for top-level fields and table cells.</summary>
    private static (JsonNode? Node, string? Error) CoerceScalar(FieldType type, JsonElement value, string label, IStringLocalizer<Messages> localizer)
    {
        if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            return (null, null);

        switch (type)
        {
            case FieldType.Checkbox:
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    return (JsonValue.Create(value.GetBoolean()), null);
                return (null, TypeError(localizer, "Documents.FieldMustBeCheckbox", label));

            case FieldType.Date:
                if (value.ValueKind != JsonValueKind.String)
                    return (null, TypeError(localizer, "Documents.FieldMustBeDate", label));
                var dateStr = value.GetString();
                if (string.IsNullOrWhiteSpace(dateStr))
                    return (null, null); // blank clears
                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return (null, TypeError(localizer, "Documents.FieldMustBeValidDate", label));
                return (JsonValue.Create(dateStr), null);

            case FieldType.Text:
            case FieldType.Select:
                if (value.ValueKind != JsonValueKind.String)
                    return (null, TypeError(localizer, "Documents.FieldMustBeText", label));
                return (JsonValue.Create(value.GetString()), null);

            default:
                // RichText/Table are not valid scalar/column types (config validation forbids them in tables).
                return (null, TypeError(localizer, "Documents.FieldMustBeSupportedValue", label));
        }
    }

    /// <summary>
    /// Coerces a Table value: an array of row objects keyed by columnKey. Unknown columns are stripped;
    /// each cell is type-checked by its column type. Two reserved keys get semantic-aware handling beyond
    /// plain pass-through (plan 2026-10-02-002):
    /// <list type="bullet">
    /// <item><c>_ownerUserId</c> is kept only on a row of a <see cref="FieldSemantics.OwnerEligible"/>
    /// table AND only when the value is an active <paramref name="activeTeamUserIds"/> member; any other
    /// table's <c>_ownerUserId</c> is dropped silently (same as any other stray reserved key), while an
    /// owner-eligible table's non-member/invalid value is dropped WITH a warning (the row picked a real
    /// person who is no longer/never was on the team — worth surfacing next to the picker).</item>
    /// <item><c>_objectives</c> is normalized (see <see cref="CoerceObjectives"/>) only on a
    /// <see cref="FieldSemantics.Goals"/> table; elsewhere it is dropped silently.</item>
    /// </list>
    /// </summary>
    private static (JsonNode? Node, string? Error) CoerceTable(
        TemplateField field, JsonElement value, IReadOnlySet<int> activeTeamUserIds, List<DocumentSaveWarningModel> warnings, IStringLocalizer<Messages> localizer)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return (null, TypeError(localizer, "Documents.FieldMustBeTable", field.Label));

        var columns = ParseTableColumns(field.ConfigJson);
        var semantic = TemplateSemanticsReader.ReadField(field.FieldType, field.ConfigJson).Semantic;
        var ownerEligible = semantic != null && FieldSemantics.OwnerEligible.Contains(semantic);
        var objectivesEligible = semantic == FieldSemantics.Goals;

        var rows = new JsonArray();
        var seenRowIds = new HashSet<Guid>();
        foreach (var rowElement in value.EnumerateArray())
        {
            if (rowElement.ValueKind != JsonValueKind.Object)
                return (null, localizer["Documents.InvalidTableRow", field.Label]);

            var row = new JsonObject();
            Guid? rowId = null;
            JsonNode? carriedFrom = null;
            bool? confirmed = null;
            int? ownerUserId = null;
            var ownerRejected = false;
            JsonNode? objectives = null;
            foreach (var cell in rowElement.EnumerateObject())
            {
                // Row identity is carried inside the row object, not as a column. Keep a valid GUID;
                // anything else is replaced below so every persisted row has exactly one stable id.
                if (cell.Name == RowMetaKeys.RowId)
                {
                    if (cell.Value.ValueKind == JsonValueKind.String && Guid.TryParse(cell.Value.GetString(), out var parsed) && parsed != Guid.Empty)
                        rowId = parsed;
                    continue;
                }
                // Provenance metadata for carried-forward rows: kept only in its well-formed shape.
                if (cell.Name == RowMetaKeys.CarriedFrom)
                {
                    carriedFrom = CoerceCarriedFrom(cell.Value);
                    continue;
                }
                if (cell.Name == RowMetaKeys.Confirmed)
                {
                    if (cell.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        confirmed = cell.Value.GetBoolean();
                    continue;
                }
                if (cell.Name == RowMetaKeys.OwnerUserId)
                {
                    if (ownerEligible && cell.Value.ValueKind == JsonValueKind.Number && cell.Value.TryGetInt32(out var uid) && activeTeamUserIds.Contains(uid))
                        ownerUserId = uid;
                    else if (ownerEligible && cell.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                        ownerRejected = true; // structurally eligible table, but not an active team member
                    // else: wrong-semantic table (or an explicit clear) — silently dropped, like any stray key.
                    continue;
                }
                if (cell.Name == RowMetaKeys.Objectives)
                {
                    if (objectivesEligible)
                        objectives = CoerceObjectives(cell.Value);
                    continue;
                }

                // Strip unknown / non-guid column keys.
                if (!Guid.TryParse(cell.Name, out var columnKey) || !columns.TryGetValue(columnKey, out var columnType))
                    continue;

                var (node, error) = CoerceScalar(columnType, cell.Value, $"{field.Label} column", localizer);
                if (error != null)
                    return (null, error);

                row[cell.Name] = node;
            }

            // Skip rows that reduced to nothing (all columns unknown/stripped) so the value-document
            // does not accumulate junk empty-object rows. A row with only an id is still "nothing" —
            // but an owner or objectives list is real content, so it alone keeps the row.
            if (row.Count == 0 && ownerUserId == null && objectives == null)
                continue;

            if (seenRowIds.Contains(rowId ?? Guid.Empty))
                rowId = null; // duplicate ids (e.g. a client-side copy) get a fresh identity
            var finalId = rowId ?? Guid.NewGuid();
            seenRowIds.Add(finalId);
            row[RowMetaKeys.RowId] = JsonValue.Create(finalId.ToString());
            if (carriedFrom != null) row[RowMetaKeys.CarriedFrom] = carriedFrom;
            if (confirmed != null) row[RowMetaKeys.Confirmed] = JsonValue.Create(confirmed.Value);
            if (ownerUserId != null) row[RowMetaKeys.OwnerUserId] = JsonValue.Create(ownerUserId.Value);
            if (objectives != null) row[RowMetaKeys.Objectives] = objectives;
            if (ownerRejected)
            {
                warnings.Add(new DocumentSaveWarningModel
                {
                    FieldKey = field.FieldKey.ToString(),
                    RowId = finalId.ToString(),
                    Code = OwnerNotTeamMemberWarningCode,
                    Message = localizer["Documents.OwnerNotActiveTeamMember", field.Label]
                });
            }
            rows.Add(row);
        }

        return (rows, null);
    }

    /// <summary>
    /// Normalizes a goal row's <c>_objectives</c> cell: an array (capped at <see cref="MaxObjectives"/>,
    /// extras dropped — order preserved) of <c>{ _rowId, description, criteria, targetDate }</c>. Each
    /// objective's own <c>_rowId</c> follows the exact same server-assign/dedupe rule as a table row's (so
    /// a client that omits it, or sends a duplicate, still gets back a stable per-objective identity).
    /// Unknown keys are dropped; an objective with no description/criteria/targetDate left is dropped
    /// (mirrors the row "reduced to nothing" rule above). A malformed (non-array) cell, OR an array that
    /// reduces to zero surviving objectives (e.g. the client clears every objective, sending <c>[]</c> or
    /// an array of now-empty entries), yields <c>null</c> — never an empty array — so
    /// <see cref="CoerceTable"/>'s "row reduced to nothing" check correctly drops an otherwise-empty row
    /// instead of keeping it alive on a vacuous <c>_objectives: []</c>. Reserved-key coercion is lenient,
    /// like <see cref="CoerceCarriedFrom"/>.
    /// </summary>
    private static JsonNode? CoerceObjectives(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            return null;

        var result = new JsonArray();
        var seenIds = new HashSet<Guid>();
        foreach (var item in value.EnumerateArray())
        {
            if (result.Count >= MaxObjectives)
                break;
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            Guid? objectiveId = null;
            string? description = null;
            string? criteria = null;
            string? targetDate = null;
            foreach (var prop in item.EnumerateObject())
            {
                switch (prop.Name)
                {
                    case RowMetaKeys.RowId:
                        if (prop.Value.ValueKind == JsonValueKind.String && Guid.TryParse(prop.Value.GetString(), out var parsed) && parsed != Guid.Empty)
                            objectiveId = parsed;
                        break;
                    case "description":
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            description = Truncate(prop.Value.GetString(), MaxObjectiveDescriptionLength)?.Trim();
                        break;
                    case "criteria":
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            criteria = Truncate(prop.Value.GetString(), MaxObjectiveCriteriaLength)?.Trim();
                        break;
                    case "targetDate":
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            targetDate = Truncate(prop.Value.GetString(), MaxObjectiveTargetDateLength)?.Trim();
                        break;
                    // Unknown keys dropped.
                }
            }

            if (string.IsNullOrEmpty(description) && string.IsNullOrEmpty(criteria) && string.IsNullOrEmpty(targetDate))
                continue;

            if (seenIds.Contains(objectiveId ?? Guid.Empty))
                objectiveId = null;
            var finalId = objectiveId ?? Guid.NewGuid();
            seenIds.Add(finalId);

            var objective = new JsonObject { [RowMetaKeys.RowId] = finalId.ToString() };
            if (description != null) objective["description"] = description;
            if (criteria != null) objective["criteria"] = criteria;
            if (targetDate != null) objective["targetDate"] = targetDate;
            result.Add(objective);
        }

        return result.Count == 0 ? null : result;
    }

    /// <summary>Accepts <c>{ versionId: int, rowId: guid, label?: string, date?: string }</c>; anything else is dropped.</summary>
    private static JsonNode? CoerceCarriedFrom(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return null;
        if (!value.TryGetProperty("versionId", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var versionId)) return null;
        if (!value.TryGetProperty("rowId", out var r) || r.ValueKind != JsonValueKind.String || !Guid.TryParse(r.GetString(), out var sourceRowId)) return null;
        var node = new JsonObject { ["versionId"] = versionId, ["rowId"] = sourceRowId.ToString() };
        if (value.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String) node["label"] = Truncate(l.GetString(), 120);
        if (value.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String) node["date"] = Truncate(d.GetString(), 40);
        return node;
    }

    private static string? Truncate(string? s, int max) => s == null ? null : (s.Length <= max ? s : s[..max]);

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

    private static string TypeError(IStringLocalizer<Messages> localizer, string resourceKey, string label) => localizer[resourceKey, label];

    // ---------------------------------------------------------------- Owner-on-team validation

    private static readonly IReadOnlySet<int> EmptyUserIdSet = new HashSet<int>();

    /// <summary>One query, loaded once per save: every user id currently an ACTIVE <c>StudentTeamMember</c>
    /// of this student — the allow-list <see cref="CoerceTable"/> validates a row's <c>_ownerUserId</c>
    /// against.</summary>
    private async Task<IReadOnlySet<int>> LoadActiveTeamUserIdsAsync(int schoolStudentId, CancellationToken ct)
    {
        var ids = await _context.StudentTeamMembers
            .AsNoTracking()
            .Where(m => m.SchoolStudentId == schoolStudentId && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    /// <summary>
    /// True when the patch carries a NUMERIC <c>_ownerUserId</c> on at least one row of a Table field
    /// whose semantic is <see cref="FieldSemantics.OwnerEligible"/> — the only shape <see cref="CoerceTable"/>
    /// can actually keep as an owner, so this gates the one team-membership query a save might need. A
    /// Table field's full row array is resent on every save of that field (not a per-row delta), so most
    /// autosave ticks on an owner-eligible table still touch zero rows with a numeric owner value (the
    /// table was edited for an unrelated reason) — this skips the query for those, not just for saves that
    /// never touch the table at all. A non-numeric/absent <c>_ownerUserId</c> can never be kept regardless
    /// of team membership (see <see cref="CoerceTable"/>'s own handling), so narrowing on "numeric value
    /// present" never changes which owners are accepted or rejected — only whether the query runs.
    /// </summary>
    private static bool PatchTouchesOwnerEligibleTable(
        IReadOnlyDictionary<string, JsonElement> patch, IReadOnlyDictionary<Guid, TemplateField> fieldsByKey)
    {
        foreach (var (rawKey, value) in patch)
        {
            if (!Guid.TryParse(rawKey, out var fieldKey) || !fieldsByKey.TryGetValue(fieldKey, out var field) || field.FieldType != FieldType.Table)
                continue;
            if (value.ValueKind != JsonValueKind.Array)
                continue;
            var semantic = TemplateSemanticsReader.ReadField(field.FieldType, field.ConfigJson).Semantic;
            if (semantic == null || !FieldSemantics.OwnerEligible.Contains(semantic))
                continue;

            foreach (var rowElement in value.EnumerateArray())
            {
                if (rowElement.ValueKind == JsonValueKind.Object
                    && rowElement.TryGetProperty(RowMetaKeys.OwnerUserId, out var ownerEl)
                    && ownerEl.ValueKind == JsonValueKind.Number)
                    return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- Concurrency

    /// <summary>Manual optimistic-concurrency check against the client token (mirrors TemplateAuthoringService). Null = proceed.</summary>
    private static string? CheckConcurrency(byte[]? currentToken, byte[]? clientToken, IStringLocalizer<Messages> localizer)
    {
        if (currentToken == null || currentToken.Length == 0)
            return null; // never-rotated row accepts any/no token
        if (clientToken == null || clientToken.Length == 0)
            return null; // no token supplied — EF's WHERE clause still guards a truly concurrent write
        return currentToken.AsSpan().SequenceEqual(clientToken) ? null : localizer["Documents.ConcurrencyConflict"];
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
                i.LastEditedByUserId,
                i.AmendsVersionId,
                AmendsVersionNumber = i.AmendsVersionId == null
                    ? (int?)null
                    : _context.AuthoredDocumentVersions.Where(v => v.Id == i.AmendsVersionId).Select(v => (int?)v.VersionNumber).FirstOrDefault(),
                i.AmendmentReason,
                i.EffectiveDate
            })
            .FirstOrDefaultAsync(ct);

        if (instance == null)
            return Fail(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        // Reuse the Phase 2 tree builder for the pinned version's section/field schema. Its ErrorKind
        // (set by TemplateAuthoringService) propagates as-is — never re-derived from the message text.
        var tree = await _authoring.GetVersionAsync(instance.DocumentTemplateVersionId, ct);
        if (!tree.Success)
            return Fail(tree.ErrorKind, tree.Message ?? _localizer["Documents.PinnedVersionUnavailable"]);

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
            AmendsVersionId = instance.AmendsVersionId,
            AmendsVersionNumber = instance.AmendsVersionNumber,
            AmendmentReason = instance.AmendmentReason,
            EffectiveDate = instance.EffectiveDate,
            TemplateVersion = tree.Data!
        });
    }

    private static ServiceResult<DocumentInstanceDetailModel> Fail(ServiceErrorKind kind, string message)
        => ServiceResult<DocumentInstanceDetailModel>.FailureResult(kind, message);

    private static ServiceResult<DocumentInstanceValuesModel> FailValues(ServiceErrorKind kind, string message)
        => ServiceResult<DocumentInstanceValuesModel>.FailureResult(kind, message);
}
