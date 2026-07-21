using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Finalize a Draft <see cref="DocumentInstance"/> into an immutable <see cref="AuthoredDocumentVersion"/>
/// snapshot plus version reads (State Document Template Engine, Phase 4). The dynamic-template equivalent
/// of <see cref="IepVersionService"/>.
///
/// <para><b>Concurrency strategy (serializable tx):</b> finalize runs inside a
/// <see cref="IsolationLevel.Serializable"/> transaction. Within it we flip the instance to
/// <see cref="DocumentInstanceStatus.Finalizing"/> (which the DocumentInstanceService edit-freeze honors),
/// validate + snapshot the values, and insert the version. The unique index on
/// <c>(SchoolStudentId, DocumentTypeId, VersionNumber)</c> is the DB backstop — a concurrent finalize that
/// slips past serialization fails with a unique violation, which we translate to a friendly retry. The
/// instance returns to <see cref="DocumentInstanceStatus.Draft"/> afterward so it stays re-finalizable
/// (mirroring the IepDraft flow).</para>
/// </summary>
public class AuthoredDocumentVersionService : IAuthoredDocumentVersionService
{
    private const string PermissionMessage = "You do not have permission to access this document.";
    private const string VersionPermissionMessage = "You do not have permission to access this document version.";
    private const string InstanceNotFoundMessage = "Document not found.";
    private const string VersionNotFoundMessage = "Document version not found.";
    private const string AlreadyFinalizingMessage = "This document is already being finalized.";
    private const string NotDraftMessage = "This document cannot be finalized in its current state.";
    private const string RaceMessage = "Another version of this document was finalized at the same time. Please try again.";
    private const string ValidationSummaryMessage = "This document has missing or invalid required fields and cannot be finalized.";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly ITemplateAuthoringService _authoring;
    private readonly IBlobStorageService _blob;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AuthoredDocumentVersionService> _logger;

    public AuthoredDocumentVersionService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IAccessService accessService,
        ITemplateAuthoringService authoring,
        IBlobStorageService blob,
        IAuditLogger audit,
        ILogger<AuthoredDocumentVersionService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _authoring = authoring;
        _blob = blob;
        _audit = audit;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Finalize

    public async Task<ServiceResult<AuthoredDocumentVersionSummaryModel>> FinalizeAsync(
        int instanceId, int actingUserId, CancellationToken ct = default)
    {
        // 1. Collaborator+ access on the instance's student.
        var header = await LoadInstanceHeaderAsync(instanceId, ct);
        if (header == null)
            return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(InstanceNotFoundMessage);

        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(PermissionMessage);

        AuthoredDocumentVersionSummaryModel summary;

        // 2. Serializable transaction — atomic validate + snapshot capture.
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // 3. Re-read the instance inside the transaction.
            var instance = await _context.DocumentInstances.FirstOrDefaultAsync(i => i.Id == instanceId, ct);
            if (instance == null)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(InstanceNotFoundMessage);
            }

            if (instance.Status == DocumentInstanceStatus.Finalizing)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(AlreadyFinalizingMessage);
            }

            if (instance.Status != DocumentInstanceStatus.Draft)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(NotDraftMessage);
            }

            // 4. Load the pinned template version tree and VALIDATE the value-document against it.
            var tree = await _authoring.GetVersionAsync(instance.DocumentTemplateVersionId, ct);
            if (!tree.Success)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(
                    tree.Message ?? "The pinned template version could not be loaded.");
            }

            var errors = ValidateAgainstSchema(tree.Data!, instance.ValuesJson);
            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(ct);
                return new ServiceResult<AuthoredDocumentVersionSummaryModel>
                {
                    Success = false,
                    Message = ValidationSummaryMessage,
                    Errors = errors
                };
            }

            // 5. Freeze the instance (blocks concurrent edits via the DocumentInstanceService edit-freeze).
            instance.Status = DocumentInstanceStatus.Finalizing;
            await _context.SaveChangesAsync(ct);

            // 6. VersionNumber = max for this (student, docType) + 1.
            var maxVersion = await _context.AuthoredDocumentVersions
                .Where(v => v.SchoolStudentId == instance.SchoolStudentId && v.DocumentTypeId == instance.DocumentTypeId)
                .Select(v => (int?)v.VersionNumber)
                .MaxAsync(ct);
            var versionNumber = (maxVersion ?? 0) + 1;

            var now = DateTime.UtcNow;

            // 7. Create the immutable version, snapshotting ValuesJson + the pinned template version id.
            var version = new AuthoredDocumentVersion
            {
                SchoolStudentId = instance.SchoolStudentId,
                DocumentTypeId = instance.DocumentTypeId,
                DocumentTemplateVersionId = instance.DocumentTemplateVersionId,
                VersionNumber = versionNumber,
                ValuesJson = instance.ValuesJson,
                FinalizedByUserId = actingUserId,
                FinalizedAt = now,
                CreatedById = actingUserId,
                UpdatedById = actingUserId,
                // The render worker flips this Pending -> Rendered/Error.
                Pdf = new AuthoredDocumentPdf
                {
                    RenderStatus = PdfRenderStatus.Pending,
                    CreatedById = actingUserId,
                    UpdatedById = actingUserId
                }
            };

            try
            {
                await _context.AuthoredDocumentVersions.AddAsync(version, ct);
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);

                // The expected contention outcome is the unique (student, docType, VersionNumber) index
                // rejecting the loser of a concurrent finalize. Confirm precisely — our computed number now
                // exists because someone else took it — so a persistent fault (FK/check/etc.) is never
                // masked behind a "try again" the caller can't recover from.
                var lostTheNumberRace = await _context.AuthoredDocumentVersions
                    .AsNoTracking()
                    .AnyAsync(v => v.SchoolStudentId == instance.SchoolStudentId
                                   && v.DocumentTypeId == instance.DocumentTypeId
                                   && v.VersionNumber == versionNumber, ct);

                if (lostTheNumberRace)
                {
                    _logger.LogWarning(ex,
                        "Concurrent finalize race on instance {InstanceId} (version number {VersionNumber} taken); caller asked to retry.",
                        instanceId, versionNumber);
                    return ServiceResult<AuthoredDocumentVersionSummaryModel>.FailureResult(RaceMessage);
                }

                _logger.LogError(ex, "Finalize failed persisting AuthoredDocumentVersion for instance {InstanceId}.", instanceId);
                throw;
            }

            // 8. Instance returns to Draft so it stays editable; re-finalize creates the next version.
            instance.Status = DocumentInstanceStatus.Draft;
            await _context.SaveChangesAsync(ct);

            // 9. Commit.
            await transaction.CommitAsync(ct);

            summary = new AuthoredDocumentVersionSummaryModel
            {
                Id = version.Id,
                SchoolStudentId = version.SchoolStudentId,
                DocumentTypeId = version.DocumentTypeId,
                VersionNumber = version.VersionNumber,
                FinalizedByUserId = actingUserId,
                FinalizedAt = version.FinalizedAt,
                PdfRenderStatus = PdfRenderStatus.Pending
            };
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }

        // FERPA audit: record the finalize against the newly-created version, after commit.
        _audit.Record(AuditAction.Finalize, actingUserId, "AuthoredDocumentVersion", summary.Id);
        _logger.LogInformation(
            "User {UserId} finalized document instance {InstanceId} into AuthoredDocumentVersion {VersionId} (v{VersionNumber}).",
            actingUserId, instanceId, summary.Id, summary.VersionNumber);

        // 10. The PDF render is enqueued by the controller AFTER this commit (failure-isolated, outside
        //     the transaction). The AuthoredDocumentPdf row is created Pending above.
        return ServiceResult<AuthoredDocumentVersionSummaryModel>.SuccessResult(summary);
    }

    // ---------------------------------------------------------------- Reads

    public async Task<ServiceResult<List<AuthoredDocumentVersionSummaryModel>>> ListVersionsForStudentAsync(
        int studentId, int actingUserId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.FailureResult(PermissionMessage);

        var rows = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.SchoolStudentId == studentId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<List<AuthoredDocumentVersionSummaryModel>>> ListForChildAsync(
        int childId, int actingUserId, CancellationToken ct = default)
    {
        // Parent must have AccessService access to the child...
        if (!await _accessService.HasMinimumRoleAsync(childId, actingUserId, AccessRole.Viewer, ct))
            return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.FailureResult(PermissionMessage);

        // ...and the version's SchoolStudent must be linked via an active accepted ChildLink.
        var linkedStudentIds = _context.ChildLinks
            .Where(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId);

        var rows = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => linkedStudentIds.Contains(v.SchoolStudentId))
            .OrderByDescending(v => v.VersionNumber)
            .Select(SummaryProjection)
            .ToListAsync(ct);

        return ServiceResult<List<AuthoredDocumentVersionSummaryModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<AuthoredDocumentVersionDetailModel>> GetVersionAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(VersionNotFoundMessage);

        if (!await CanReadStudentAsync(actingUserId, header.SchoolStudentId, ct))
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(VersionPermissionMessage);

        var version = await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new
            {
                v.Id,
                v.SchoolStudentId,
                v.DocumentTypeId,
                DocumentTypeKey = v.DocumentType.Key,
                DocumentTypeDisplayName = v.DocumentType.DisplayName,
                v.DocumentTemplateVersionId,
                v.VersionNumber,
                v.FinalizedByUserId,
                v.FinalizedAt,
                v.ValuesJson,
                PdfRenderStatus = v.Pdf != null ? (PdfRenderStatus?)v.Pdf.RenderStatus : null,
                PdfBlobUri = v.Pdf != null ? v.Pdf.BlobUri : null,
                PdfRenderedAt = v.Pdf != null ? v.Pdf.RenderedAt : null
            })
            .FirstOrDefaultAsync(ct);

        if (version == null)
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(VersionNotFoundMessage);

        // Reuse the Phase 2 tree builder for the pinned version's section/field schema.
        var tree = await _authoring.GetVersionAsync(version.DocumentTemplateVersionId, ct);
        if (!tree.Success)
            return ServiceResult<AuthoredDocumentVersionDetailModel>.FailureResult(
                tree.Message ?? "The pinned template version could not be loaded.");

        _audit.Record(AuditAction.View, actingUserId, "AuthoredDocumentVersion", versionId);

        return ServiceResult<AuthoredDocumentVersionDetailModel>.SuccessResult(new AuthoredDocumentVersionDetailModel
        {
            Id = version.Id,
            SchoolStudentId = version.SchoolStudentId,
            DocumentTypeId = version.DocumentTypeId,
            DocumentTypeKey = version.DocumentTypeKey,
            DocumentTypeDisplayName = version.DocumentTypeDisplayName,
            DocumentTemplateVersionId = version.DocumentTemplateVersionId,
            VersionNumber = version.VersionNumber,
            FinalizedByUserId = version.FinalizedByUserId,
            FinalizedAt = version.FinalizedAt,
            ValuesJson = version.ValuesJson,
            PdfRenderStatus = version.PdfRenderStatus,
            PdfBlobUri = version.PdfBlobUri,
            PdfRenderedAt = version.PdfRenderedAt,
            TemplateVersion = tree.Data!
        });
    }

    // ---------------------------------------------------------------- PDF status + retry

    public async Task<ServiceResult<AuthoredDocumentPdfStatusModel>> GetPdfStatusAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<AuthoredDocumentPdfStatusModel>.FailureResult(VersionNotFoundMessage);

        if (!await CanReadStudentAsync(actingUserId, header.SchoolStudentId, ct))
            return ServiceResult<AuthoredDocumentPdfStatusModel>.FailureResult(VersionPermissionMessage);

        var pdf = await _context.AuthoredDocumentPdfs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.AuthoredDocumentVersionId == versionId, ct);

        var model = new AuthoredDocumentPdfStatusModel
        {
            VersionId = versionId,
            RenderStatus = pdf?.RenderStatus ?? PdfRenderStatus.Pending,
            RenderedAt = pdf?.RenderedAt,
            ErrorMessage = pdf?.ErrorMessage
        };

        if (pdf?.RenderStatus == PdfRenderStatus.Rendered)
        {
            // Build a short-lived download URL from the deterministic blob path (SAS when supported).
            var blobPath = IAuthoredDocumentPdfService.BlobPathFor(versionId, header.VersionNumber);
            model.Url = await _blob.GetDownloadUrlAsync(blobPath);

            // FERPA audit: a Rendered download URL is actually being handed out — log the export.
            _audit.Record(AuditAction.Export, actingUserId, "AuthoredDocumentVersion", versionId);
        }

        return ServiceResult<AuthoredDocumentPdfStatusModel>.SuccessResult(model);
    }

    public async Task<ServiceResult<int>> RequestPdfRetryAsync(
        int versionId, int actingUserId, CancellationToken ct = default)
    {
        var header = await LoadVersionHeaderAsync(versionId, ct);
        if (header == null)
            return ServiceResult<int>.FailureResult(VersionNotFoundMessage);

        // Retry is an authoring action — Collaborator+ educator on the student's school.
        if (!await _orgAccess.CanActOnStudentAsync(actingUserId, header.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<int>.FailureResult(PermissionMessage);

        var pdf = await _context.AuthoredDocumentPdfs.FirstOrDefaultAsync(p => p.AuthoredDocumentVersionId == versionId, ct);
        if (pdf == null)
            return ServiceResult<int>.FailureResult("This version has no PDF record to retry.");

        if (pdf.RenderStatus == PdfRenderStatus.Rendered)
            return ServiceResult<int>.FailureResult("This version's PDF is already rendered.");

        // Error or Pending -> set Pending so the UI shows "generating" until the worker re-renders.
        pdf.RenderStatus = PdfRenderStatus.Pending;
        pdf.ErrorMessage = null;
        pdf.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<int>.SuccessResult(versionId);
    }

    // ---------------------------------------------------------------- Schema validation (finalize)

    /// <summary>
    /// Validates the frozen value-document against the pinned template schema, returning a COMPLETE list
    /// of friendly errors (empty when valid). Each message identifies section + field label (+ row index
    /// for table cells). Walks sections/fields in display order for stable, deterministic error ordering.
    /// </summary>
    public static List<string> ValidateAgainstSchema(TemplateVersionDetailModel tree, string? valuesJson)
    {
        var errors = new List<string>();

        JsonObject values;
        try
        {
            values = (string.IsNullOrWhiteSpace(valuesJson)
                ? new JsonObject()
                : JsonNode.Parse(valuesJson) as JsonObject) ?? new JsonObject();
        }
        catch (JsonException)
        {
            values = new JsonObject();
        }

        foreach (var section in tree.Sections.OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id))
        {
            foreach (var field in section.Fields.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id))
            {
                values.TryGetPropertyValue(field.FieldKey.ToString(), out var node);
                ValidateField(section.Title, field, node, errors);
            }
        }

        return errors;
    }

    private static void ValidateField(string sectionTitle, TemplateFieldModel field, JsonNode? node, List<string> errors)
    {
        switch (field.FieldType)
        {
            case FieldType.Table:
                ValidateTable(sectionTitle, field, node, errors);
                break;

            case FieldType.Select:
            {
                var value = AsString(node);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (field.Required) errors.Add(RequiredError(sectionTitle, field.Label));
                }
                else if (!ParseSelectValues(field.ConfigJson).Contains(value))
                {
                    errors.Add(FieldError(sectionTitle, field.Label, $"\"{value}\" is not a valid option."));
                }
                break;
            }

            case FieldType.Date:
            {
                var value = AsString(node);
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (field.Required) errors.Add(RequiredError(sectionTitle, field.Label));
                }
                else if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                {
                    errors.Add(FieldError(sectionTitle, field.Label, "Enter a valid date."));
                }
                break;
            }

            case FieldType.Checkbox:
                // A required checkbox must be checked (the "I certify …" pattern). An unchecked/absent
                // required checkbox is a validation error; optional checkboxes are always satisfied.
                if (field.Required && AsBool(node) != true)
                    errors.Add(FieldError(sectionTitle, field.Label, "This must be checked."));
                break;

            default: // Text, RichText
                if (field.Required && string.IsNullOrWhiteSpace(AsString(node)))
                    errors.Add(RequiredError(sectionTitle, field.Label));
                break;
        }
    }

    private static void ValidateTable(string sectionTitle, TemplateFieldModel field, JsonNode? node, List<string> errors)
    {
        var (columns, minRows, maxRows) = ParseTableConfig(field.ConfigJson);
        var rows = node as JsonArray;
        var rowCount = rows?.Count ?? 0;

        if (minRows is int min && rowCount < min)
            errors.Add(FieldError(sectionTitle, field.Label, $"At least {min} row(s) are required."));
        else if (field.Required && (minRows is null or 0) && rowCount == 0)
            errors.Add(RequiredError(sectionTitle, field.Label));

        if (maxRows is int max && rowCount > max)
            errors.Add(FieldError(sectionTitle, field.Label, $"No more than {max} row(s) are allowed."));

        if (rows == null)
            return;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] as JsonObject;
            var rowNum = i + 1; // 1-based for humans

            foreach (var col in columns)
            {
                JsonNode? cell = null;
                row?.TryGetPropertyValue(col.ColumnKey.ToString(), out cell);

                if (IsCellEmpty(col.Type, cell))
                {
                    if (col.Required)
                        errors.Add(TableCellError(sectionTitle, field.Label, rowNum, col.Label, "This field is required."));
                    continue;
                }

                switch (col.Type)
                {
                    case FieldType.Date:
                        if (!DateTime.TryParse(AsString(cell), CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                            errors.Add(TableCellError(sectionTitle, field.Label, rowNum, col.Label, "Enter a valid date."));
                        break;

                    case FieldType.Select:
                        var v = AsString(cell);
                        if (v != null && !ParseSelectValues(col.ConfigJson).Contains(v))
                            errors.Add(TableCellError(sectionTitle, field.Label, rowNum, col.Label, $"\"{v}\" is not a valid option."));
                        break;
                }
            }
        }
    }

    private static bool IsCellEmpty(FieldType columnType, JsonNode? cell)
    {
        if (cell == null)
            return true;
        // A Checkbox column always carries a bool value, so it is never "empty".
        if (columnType == FieldType.Checkbox)
            return AsBool(cell) == null;
        return string.IsNullOrWhiteSpace(AsString(cell));
    }

    private static string? AsString(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool? AsBool(JsonNode? node)
        => node is JsonValue v && v.TryGetValue<bool>(out var b) ? b : null;

    private static HashSet<string> ParseSelectValues(string? configJson)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(configJson))
            return set;
        try
        {
            var cfg = JsonSerializer.Deserialize<SelectFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            if (cfg?.Options != null)
                foreach (var option in cfg.Options)
                    set.Add(option.Value);
        }
        catch (JsonException)
        {
            // A malformed config yields an empty option set; membership checks then fail loudly.
        }
        return set;
    }

    private static (List<TableColumn> Columns, int? MinRows, int? MaxRows) ParseTableConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return (new List<TableColumn>(), null, null);
        try
        {
            var cfg = JsonSerializer.Deserialize<TableFieldConfig>(configJson, TemplateFieldConfigValidator.JsonOptions);
            return (cfg?.Columns ?? new List<TableColumn>(), cfg?.MinRows, cfg?.MaxRows);
        }
        catch (JsonException)
        {
            return (new List<TableColumn>(), null, null);
        }
    }

    private static string RequiredError(string sectionTitle, string fieldLabel)
        => FieldError(sectionTitle, fieldLabel, "This field is required.");

    private static string FieldError(string sectionTitle, string fieldLabel, string message)
        => $"Section \"{sectionTitle}\" → \"{fieldLabel}\": {message}";

    private static string TableCellError(string sectionTitle, string fieldLabel, int rowNum, string columnLabel, string message)
        => $"Section \"{sectionTitle}\" → \"{fieldLabel}\" (row {rowNum}) → \"{columnLabel}\": {message}";

    // ---------------------------------------------------------------- Access helpers

    /// <summary>Educator with org access (Viewer+) OR a linked parent — read authorization for a version's student.</summary>
    private async Task<bool> CanReadStudentAsync(int userId, int studentId, CancellationToken ct)
    {
        if (await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return true;
        return await ParentCanViewStudentAsync(userId, studentId, ct);
    }

    /// <summary>
    /// True when the caller is a parent linked to this SchoolStudent: an active accepted ChildLink to a
    /// ChildProfile the caller has AccessService (Viewer+) access to (mirrors IepVersionService).
    /// </summary>
    private async Task<bool> ParentCanViewStudentAsync(int userId, int studentId, CancellationToken ct)
    {
        var linkedChildIds = await _context.ChildLinks
            .AsNoTracking()
            .Where(l => l.SchoolStudentId == studentId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
            .Select(l => l.ChildProfileId!.Value)
            .ToListAsync(ct);

        foreach (var childId in linkedChildIds)
        {
            if (await _accessService.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
                return true;
        }
        return false;
    }

    private sealed record InstanceHeader(int SchoolStudentId, int DocumentTypeId, DocumentInstanceStatus Status);

    private async Task<InstanceHeader?> LoadInstanceHeaderAsync(int instanceId, CancellationToken ct) =>
        await _context.DocumentInstances
            .AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => new InstanceHeader(i.SchoolStudentId, i.DocumentTypeId, i.Status))
            .FirstOrDefaultAsync(ct);

    private sealed record VersionHeader(int SchoolStudentId, int VersionNumber);

    private async Task<VersionHeader?> LoadVersionHeaderAsync(int versionId, CancellationToken ct) =>
        await _context.AuthoredDocumentVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new VersionHeader(v.SchoolStudentId, v.VersionNumber))
            .FirstOrDefaultAsync(ct);

    // ---------------------------------------------------------------- Mappers

    // EF-translatable projection (PdfRenderStatus + DocumentType lookup join via nav properties).
    private static readonly System.Linq.Expressions.Expression<Func<AuthoredDocumentVersion, AuthoredDocumentVersionSummaryModel>> SummaryProjection =
        v => new AuthoredDocumentVersionSummaryModel
        {
            Id = v.Id,
            SchoolStudentId = v.SchoolStudentId,
            DocumentTypeId = v.DocumentTypeId,
            DocumentTypeKey = v.DocumentType.Key,
            DocumentTypeDisplayName = v.DocumentType.DisplayName,
            VersionNumber = v.VersionNumber,
            FinalizedByUserId = v.FinalizedByUserId,
            FinalizedAt = v.FinalizedAt,
            PdfRenderStatus = v.Pdf != null ? v.Pdf.RenderStatus : (PdfRenderStatus?)null
        };
}
