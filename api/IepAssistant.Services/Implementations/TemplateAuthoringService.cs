using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Phase 2 authoring for the State Document Template Engine. Controller-side authorization restricts
/// callers to platform <c>Admin</c>; this service focuses on Draft-only mutation, per-FieldType config
/// validation, optimistic concurrency (rotating <see cref="DocumentTemplateVersion.RowVersion"/>),
/// publish gating, and forking a new Draft from the latest Published version.
///
/// <para>Immutability of Published versions is enforced primarily here (every mutation refuses a
/// non-Draft version) and defended by <c>ImmutableVersionInterceptor</c>. Because that interceptor
/// does not catch <c>ExecuteUpdate</c>/<c>ExecuteDelete</c>, this service never uses bulk ops on
/// version content.</para>
/// </summary>
public class TemplateAuthoringService : ITemplateAuthoringService
{
    private const string VersionNotFoundMessage = "Template version not found.";
    private const string SectionNotFoundMessage = "Template section not found.";
    private const string FieldNotFoundMessage = "Template field not found.";
    private const string TemplateNotFoundMessage = "Template not found.";
    private const string NotDraftMessage = "Only a Draft version can be edited. Create a new draft from the published version first.";
    private const string ConcurrencyMessage = "This template was changed by someone else. Please reload and try again.";

    private readonly ApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    private readonly ILogger<TemplateAuthoringService> _logger;

    public TemplateAuthoringService(ApplicationDbContext context, IAuditLogger audit, ILogger<TemplateAuthoringService> logger)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
    }

    // ---------------------------------------------------------------- Reads

    public async Task<ServiceResult<TemplateVersionDetailModel>> GetVersionAsync(int versionId, CancellationToken ct = default)
    {
        var detail = await BuildDetailAsync(versionId, ct);
        return detail == null
            ? ServiceResult<TemplateVersionDetailModel>.FailureResult(VersionNotFoundMessage)
            : ServiceResult<TemplateVersionDetailModel>.SuccessResult(detail);
    }

    // ---------------------------------------------------------------- Sections

    public async Task<ServiceResult<TemplateVersionDetailModel>> AddSectionAsync(
        int userId, int versionId, string title, byte[]? rowVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Fail("Section title is required.");

        var version = await LoadDraftVersionAsync(versionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(versionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        var maxOrder = await _context.TemplateSections
            .Where(s => s.DocumentTemplateVersionId == versionId)
            .Select(s => (int?)s.DisplayOrder).MaxAsync(ct);

        _context.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = versionId,
            SectionKey = Guid.NewGuid(),
            Title = title.Trim(),
            DisplayOrder = (maxOrder ?? -1) + 1,
            CreatedById = userId,
            UpdatedById = userId
        });

        return await CommitAsync(version, userId, versionId, ct);
    }

    public async Task<ServiceResult<TemplateVersionDetailModel>> UpdateSectionAsync(
        int userId, int sectionId, string title, byte[]? rowVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Fail("Section title is required.");

        var section = await _context.TemplateSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null) return Fail(SectionNotFoundMessage);

        var version = await LoadDraftVersionAsync(section.DocumentTemplateVersionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(section.DocumentTemplateVersionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        section.Title = title.Trim();
        section.UpdatedById = userId;

        return await CommitAsync(version, userId, version.Id, ct);
    }

    public async Task<ServiceResult<TemplateVersionDetailModel>> DeleteSectionAsync(
        int userId, int sectionId, byte[]? rowVersion, CancellationToken ct = default)
    {
        // Load with fields so the cascade delete is explicit under every provider.
        var section = await _context.TemplateSections
            .Include(s => s.Fields)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null) return Fail(SectionNotFoundMessage);

        var version = await LoadDraftVersionAsync(section.DocumentTemplateVersionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(section.DocumentTemplateVersionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        _context.TemplateSections.Remove(section);

        return await CommitAsync(version, userId, version.Id, ct);
    }

    public async Task<ServiceResult<TemplateVersionDetailModel>> ReorderSectionsAsync(
        int userId, int versionId, IReadOnlyList<int> orderedSectionIds, byte[]? rowVersion, CancellationToken ct = default)
    {
        var version = await LoadDraftVersionAsync(versionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(versionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        var sections = await _context.TemplateSections
            .Where(s => s.DocumentTemplateVersionId == versionId).ToListAsync(ct);

        var orderError = ValidateReorderSet(orderedSectionIds, sections.Select(s => s.Id), "section");
        if (orderError != null) return Fail(orderError);

        var byId = sections.ToDictionary(s => s.Id);
        for (var i = 0; i < orderedSectionIds.Count; i++)
        {
            var section = byId[orderedSectionIds[i]];
            section.DisplayOrder = i;
            section.UpdatedById = userId;
        }

        return await CommitAsync(version, userId, versionId, ct);
    }

    // ---------------------------------------------------------------- Fields

    public async Task<ServiceResult<TemplateVersionDetailModel>> AddFieldAsync(
        int userId, int sectionId, FieldType fieldType, string label, bool required, string? configJson,
        byte[]? rowVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Fail("Field label is required.");

        var configError = TemplateFieldConfigValidator.Validate(fieldType, configJson);
        if (configError != null) return Fail(configError);

        var section = await _context.TemplateSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null) return Fail(SectionNotFoundMessage);

        var version = await LoadDraftVersionAsync(section.DocumentTemplateVersionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(section.DocumentTemplateVersionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        var maxOrder = await _context.TemplateFields
            .Where(f => f.TemplateSectionId == sectionId)
            .Select(f => (int?)f.DisplayOrder).MaxAsync(ct);

        _context.TemplateFields.Add(new TemplateField
        {
            TemplateSectionId = sectionId,
            DocumentTemplateVersionId = version.Id,
            FieldKey = Guid.NewGuid(),
            FieldType = fieldType,
            Label = label.Trim(),
            Required = required,
            ConfigJson = NormalizeConfig(configJson),
            DisplayOrder = (maxOrder ?? -1) + 1,
            CreatedById = userId,
            UpdatedById = userId
        });

        return await CommitAsync(version, userId, version.Id, ct);
    }

    public async Task<ServiceResult<TemplateVersionDetailModel>> UpdateFieldAsync(
        int userId, int fieldId, FieldType fieldType, string label, bool required, string? configJson,
        byte[]? rowVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Fail("Field label is required.");

        var configError = TemplateFieldConfigValidator.Validate(fieldType, configJson);
        if (configError != null) return Fail(configError);

        var field = await _context.TemplateFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field == null) return Fail(FieldNotFoundMessage);

        var version = await LoadDraftVersionAsync(field.DocumentTemplateVersionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(field.DocumentTemplateVersionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        // FieldKey is stable and never changes on update (values stay mapped).
        field.FieldType = fieldType;
        field.Label = label.Trim();
        field.Required = required;
        field.ConfigJson = NormalizeConfig(configJson);
        field.UpdatedById = userId;

        return await CommitAsync(version, userId, version.Id, ct);
    }

    public async Task<ServiceResult<TemplateVersionDetailModel>> DeleteFieldAsync(
        int userId, int fieldId, byte[]? rowVersion, CancellationToken ct = default)
    {
        var field = await _context.TemplateFields.FirstOrDefaultAsync(f => f.Id == fieldId, ct);
        if (field == null) return Fail(FieldNotFoundMessage);

        var version = await LoadDraftVersionAsync(field.DocumentTemplateVersionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(field.DocumentTemplateVersionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        _context.TemplateFields.Remove(field);

        return await CommitAsync(version, userId, version.Id, ct);
    }

    public async Task<ServiceResult<TemplateVersionDetailModel>> ReorderFieldsAsync(
        int userId, int sectionId, IReadOnlyList<int> orderedFieldIds, byte[]? rowVersion, CancellationToken ct = default)
    {
        var section = await _context.TemplateSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null) return Fail(SectionNotFoundMessage);

        var version = await LoadDraftVersionAsync(section.DocumentTemplateVersionId, ct);
        if (version == null) return Fail(await VersionErrorAsync(section.DocumentTemplateVersionId, ct));

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        var fields = await _context.TemplateFields
            .Where(f => f.TemplateSectionId == sectionId).ToListAsync(ct);

        var orderError = ValidateReorderSet(orderedFieldIds, fields.Select(f => f.Id), "field");
        if (orderError != null) return Fail(orderError);

        var byId = fields.ToDictionary(f => f.Id);
        for (var i = 0; i < orderedFieldIds.Count; i++)
        {
            var field = byId[orderedFieldIds[i]];
            field.DisplayOrder = i;
            field.UpdatedById = userId;
        }

        return await CommitAsync(version, userId, version.Id, ct);
    }

    // ---------------------------------------------------------------- Publish

    public async Task<ServiceResult<TemplateVersionDetailModel>> PublishAsync(
        int userId, int templateId, byte[]? rowVersion, CancellationToken ct = default)
    {
        var template = await _context.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template == null) return Fail(TemplateNotFoundMessage);

        var version = await _context.DocumentTemplateVersions
            .FirstOrDefaultAsync(v => v.DocumentTemplateId == templateId && v.Status == TemplateVersionStatus.Draft, ct);
        if (version == null) return Fail("This template has no Draft version to publish.");

        var guard = CheckConcurrency(version, rowVersion);
        if (guard != null) return Fail(guard);

        // Structural + config validation — gather ALL problems as field-level errors.
        var errors = await ValidatePublishableAsync(version.Id, ct);
        if (errors.Count > 0)
            return ServiceResult<TemplateVersionDetailModel>.FailureResult(errors);

        version.Status = TemplateVersionStatus.Published;
        version.PublishedAt = DateTime.UtcNow;
        version.UpdatedById = userId;
        RotateToken(version);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(ConcurrencyMessage);
        }

        // FERPA/governance audit: publish is what makes a template version the pinned schema for all
        // future documents of this (state, type) — record it in the tamper-evident trail (G-e.4).
        _audit.Record(AuditAction.Publish, userId, "DocumentTemplateVersion", version.Id);
        _logger.LogInformation(
            "Admin {UserId} published template {TemplateId} version {VersionId} (v{VersionNumber}).",
            userId, templateId, version.Id, version.VersionNumber);

        return await SuccessDetailAsync(version.Id, ct);
    }

    // ---------------------------------------------------------------- Fork

    public async Task<ServiceResult<TemplateVersionDetailModel>> CreateDraftFromPublishedAsync(
        int userId, int templateId, CancellationToken ct = default)
    {
        var template = await _context.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template == null) return Fail(TemplateNotFoundMessage);

        // Only one Draft per template at a time.
        var hasDraft = await _context.DocumentTemplateVersions
            .AnyAsync(v => v.DocumentTemplateId == templateId && v.Status == TemplateVersionStatus.Draft, ct);
        if (hasDraft)
            return Fail("This template already has a Draft version. Publish or discard it before creating another.");

        // Fork the latest Published version.
        var source = await _context.DocumentTemplateVersions
            .AsNoTracking()
            .Where(v => v.DocumentTemplateId == templateId && v.Status == TemplateVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (source == null)
            return Fail("This template has no published version to base a new draft on.");

        var sourceSections = await _context.TemplateSections
            .AsNoTracking()
            .Where(s => s.DocumentTemplateVersionId == source.Id)
            .Include(s => s.Fields)
            .ToListAsync(ct);

        var maxVersion = await _context.DocumentTemplateVersions
            .Where(v => v.DocumentTemplateId == templateId)
            .Select(v => (int?)v.VersionNumber).MaxAsync(ct);

        var draft = new DocumentTemplateVersion
        {
            DocumentTemplateId = templateId,
            VersionNumber = (maxVersion ?? 0) + 1,
            Status = TemplateVersionStatus.Draft,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedById = userId,
            UpdatedById = userId,
            // Deep-copy sections/fields, carrying SectionKey/FieldKey verbatim so instance values remain mappable.
            Sections = sourceSections.Select(s => new TemplateSection
            {
                SectionKey = s.SectionKey,
                Title = s.Title,
                DisplayOrder = s.DisplayOrder,
                CreatedById = userId,
                UpdatedById = userId,
                Fields = s.Fields.Select(f => new TemplateField
                {
                    FieldKey = f.FieldKey,
                    FieldType = f.FieldType,
                    Label = f.Label,
                    Required = f.Required,
                    ConfigJson = f.ConfigJson,
                    DisplayOrder = f.DisplayOrder,
                    CreatedById = userId,
                    UpdatedById = userId
                }).ToList()
            }).ToList()
        };

        // Wire the denormalized version FK on every copied field via the navigation (FK resolved on save).
        foreach (var section in draft.Sections)
            foreach (var field in section.Fields)
                field.Version = draft;

        _context.DocumentTemplateVersions.Add(draft);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Backstop for the (DocumentTemplateId, VersionNumber) unique index: two concurrent forks
            // can both pass the AnyAsync/Max pre-checks, so the loser's duplicate version number is
            // translated into the same friendly single-draft error rather than surfacing as a 500.
            return Fail("This template already has a Draft version. Publish or discard it before creating another.");
        }

        // Audit the fork (new Draft created from a Published version) alongside the app log (G-e.4).
        _audit.Record(AuditAction.Edit, userId, "DocumentTemplateVersion", draft.Id);
        _logger.LogInformation(
            "Admin {UserId} forked template {TemplateId} into new Draft version {VersionId} (v{VersionNumber}) from published v{SourceVersion}.",
            userId, templateId, draft.Id, draft.VersionNumber, source.VersionNumber);

        return await SuccessDetailAsync(draft.Id, ct);
    }

    // ---------------------------------------------------------------- Publish validation

    /// <summary>
    /// Returns all publish blockers for a version: at least one section, each section at least one field,
    /// and every field's config valid. Empty list = publishable. Errors are field-level friendly strings.
    /// </summary>
    private async Task<List<string>> ValidatePublishableAsync(int versionId, CancellationToken ct)
    {
        var errors = new List<string>();

        var sections = await _context.TemplateSections
            .AsNoTracking()
            .Where(s => s.DocumentTemplateVersionId == versionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        if (sections.Count == 0)
        {
            errors.Add("Add at least one section before publishing.");
            return errors;
        }

        foreach (var section in sections)
        {
            if (section.Fields.Count == 0)
            {
                errors.Add($"Section '{section.Title}' must have at least one field.");
                continue;
            }

            foreach (var field in section.Fields)
            {
                var configError = TemplateFieldConfigValidator.Validate(field.FieldType, field.ConfigJson);
                if (configError != null)
                    errors.Add($"Field '{field.Label}': {configError}");
            }
        }

        return errors;
    }

    // ---------------------------------------------------------------- Concurrency + commit helpers

    /// <summary>Loads a version tracked, or null if it does not exist OR is not a Draft.</summary>
    private async Task<DocumentTemplateVersion?> LoadDraftVersionAsync(int versionId, CancellationToken ct)
    {
        var version = await _context.DocumentTemplateVersions.FirstOrDefaultAsync(v => v.Id == versionId, ct);
        return version is { Status: TemplateVersionStatus.Draft } ? version : null;
    }

    /// <summary>Distinguishes "not found" from "not a Draft" for a friendly message after LoadDraftVersionAsync returns null.</summary>
    private async Task<string> VersionErrorAsync(int versionId, CancellationToken ct)
    {
        var exists = await _context.DocumentTemplateVersions.AnyAsync(v => v.Id == versionId, ct);
        return exists ? NotDraftMessage : VersionNotFoundMessage;
    }

    /// <summary>Manual optimistic-concurrency check against the client-supplied token. Null = proceed.</summary>
    private static string? CheckConcurrency(DocumentTemplateVersion version, byte[]? clientRowVersion)
    {
        // First edit of a never-rotated draft (token still null) accepts any/no client token.
        if (version.RowVersion == null || version.RowVersion.Length == 0)
            return null;

        // If the caller supplied no token we cannot detect staleness — allow (EF's WHERE clause still
        // guards against a truly concurrent DB write during save).
        if (clientRowVersion == null || clientRowVersion.Length == 0)
            return null;

        return version.RowVersion.AsSpan().SequenceEqual(clientRowVersion) ? null : ConcurrencyMessage;
    }

    private static void RotateToken(DocumentTemplateVersion version)
        => version.RowVersion = Guid.NewGuid().ToByteArray();

    /// <summary>Rotates the token, stamps the editor, saves (translating a concurrency fault), and returns the fresh tree.</summary>
    private async Task<ServiceResult<TemplateVersionDetailModel>> CommitAsync(
        DocumentTemplateVersion version, int userId, int versionId, CancellationToken ct)
    {
        // Touch the version so it is Modified (advances the concurrency token + bumps UpdatedAt).
        RotateToken(version);
        version.UpdatedById = userId;

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Fail(ConcurrencyMessage);
        }

        return await SuccessDetailAsync(versionId, ct);
    }

    private async Task<ServiceResult<TemplateVersionDetailModel>> SuccessDetailAsync(int versionId, CancellationToken ct)
    {
        var detail = await BuildDetailAsync(versionId, ct);
        return detail == null
            ? ServiceResult<TemplateVersionDetailModel>.FailureResult(VersionNotFoundMessage)
            : ServiceResult<TemplateVersionDetailModel>.SuccessResult(detail);
    }

    private static ServiceResult<TemplateVersionDetailModel> Fail(string message)
        => ServiceResult<TemplateVersionDetailModel>.FailureResult(message);

    private static string? NormalizeConfig(string? configJson)
        => string.IsNullOrWhiteSpace(configJson) ? null : configJson.Trim();

    /// <summary>Ensures the reorder id set is exactly the current set (no missing/extra/duplicate ids).</summary>
    private static string? ValidateReorderSet(IReadOnlyList<int> orderedIds, IEnumerable<int> currentIds, string noun)
    {
        var current = currentIds.ToHashSet();
        if (orderedIds.Count != current.Count || orderedIds.Distinct().Count() != orderedIds.Count
            || !orderedIds.All(current.Contains))
            return $"The {noun} order must list every {noun} exactly once.";
        return null;
    }

    // ---------------------------------------------------------------- Tree mapping

    private async Task<TemplateVersionDetailModel?> BuildDetailAsync(int versionId, CancellationToken ct)
    {
        var version = await _context.DocumentTemplateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);
        if (version == null) return null;

        var sections = await _context.TemplateSections
            .AsNoTracking()
            .Where(s => s.DocumentTemplateVersionId == versionId)
            .Include(s => s.Fields)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id)
            .ToListAsync(ct);

        return new TemplateVersionDetailModel
        {
            Id = version.Id,
            DocumentTemplateId = version.DocumentTemplateId,
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            PublishedAt = version.PublishedAt,
            RowVersion = version.RowVersion,
            Sections = sections.Select(s => new TemplateSectionModel
            {
                Id = s.Id,
                SectionKey = s.SectionKey,
                Title = s.Title,
                DisplayOrder = s.DisplayOrder,
                Fields = s.Fields
                    .OrderBy(f => f.DisplayOrder).ThenBy(f => f.Id)
                    .Select(f => new TemplateFieldModel
                    {
                        Id = f.Id,
                        FieldKey = f.FieldKey,
                        FieldType = f.FieldType,
                        Label = f.Label,
                        Required = f.Required,
                        ConfigJson = f.ConfigJson,
                        DisplayOrder = f.DisplayOrder
                    }).ToList()
            }).ToList()
        };
    }
}
