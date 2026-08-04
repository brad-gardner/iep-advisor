using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Admin-authored State Document Template Engine (Phase 1). Controller-side authorization restricts
/// all callers to platform <c>Admin</c>; this service assumes that gate and focuses on validation,
/// state-code normalization, uniqueness, and the create/list operations. Auditing follows the
/// IAuditableEntity convention (CreatedById stamped from the acting admin).
/// </summary>
public class DocumentTemplateService : IDocumentTemplateService
{
    private readonly ApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    private readonly ILogger<DocumentTemplateService> _logger;

    public DocumentTemplateService(ApplicationDbContext context, IAuditLogger audit, ILogger<DocumentTemplateService> logger)
    {
        _context = context;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ServiceResult<DocumentTemplateModel>> CreateTemplateAsync(
        int userId, string? stateCode, int documentTypeId, string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<DocumentTemplateModel>.FailureResult("Template name is required.");

        var (normalizedState, stateError) = NormalizeStateCode(stateCode);
        if (stateError != null)
            return ServiceResult<DocumentTemplateModel>.FailureResult(stateError);

        // The document type must exist AND be active before a template can target it.
        var documentType = await _context.DocumentTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(dt => dt.Id == documentTypeId, ct);
        if (documentType == null)
            return ServiceResult<DocumentTemplateModel>.FailureResult("The selected document type does not exist.");
        if (!documentType.IsActive)
            return ServiceResult<DocumentTemplateModel>.FailureResult("The selected document type is not active.");

        // Friendly uniqueness pre-check (the unique index is the DB backstop). Compared against the
        // normalized state so "oh" and "OH" collide as intended.
        var duplicate = await _context.DocumentTemplates
            .AsNoTracking()
            .AnyAsync(t => t.StateCode == normalizedState && t.DocumentTypeId == documentTypeId, ct);
        if (duplicate)
        {
            var scope = normalizedState ?? "the default";
            return ServiceResult<DocumentTemplateModel>.FailureResult(
                $"A template for {documentType.DisplayName} in {scope} already exists.");
        }

        var now = DateTime.UtcNow;
        var template = new DocumentTemplate
        {
            StateCode = normalizedState,
            DocumentTypeId = documentTypeId,
            Name = name.Trim(),
            CreatedById = userId,
            UpdatedById = userId,
            // Every new template starts with an empty Draft working copy (VersionNumber = 1).
            Versions =
            {
                new DocumentTemplateVersion
                {
                    VersionNumber = 1,
                    Status = TemplateVersionStatus.Draft,
                    CreatedById = userId,
                    UpdatedById = userId
                }
            }
        };

        await _context.DocumentTemplates.AddAsync(template, ct);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Backstop for the (StateCode, DocumentTypeId) unique index: two concurrent creates can
            // both pass the AnyAsync pre-check, so translate the index violation into the same
            // friendly error rather than letting it surface as a 500.
            var scope = normalizedState ?? "the default";
            return ServiceResult<DocumentTemplateModel>.FailureResult(
                $"A template for {documentType.DisplayName} in {scope} already exists.");
        }

        // Audit template creation (its empty Draft v1) in the tamper-evident authoring trail (G-e.4).
        _audit.Record(AuditAction.Edit, userId, "DocumentTemplate", template.Id);
        _logger.LogInformation(
            "Admin {UserId} created document template {TemplateId} ({DocumentTypeKey}, state {StateCode}).",
            userId, template.Id, documentType.Key, normalizedState ?? "default");

        return ServiceResult<DocumentTemplateModel>.SuccessResult(MapTemplate(template, documentType));
    }

    public async Task<ServiceResult<List<DocumentTemplateModel>>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await _context.DocumentTemplates
            .AsNoTracking()
            .Include(t => t.DocumentType)
            .Include(t => t.Versions)
            .OrderBy(t => t.DocumentTypeId)
            .ThenBy(t => t.StateCode)
            .ToListAsync(ct);

        var models = templates.Select(t => MapTemplate(t, t.DocumentType)).ToList();
        return ServiceResult<List<DocumentTemplateModel>>.SuccessResult(models);
    }

    public async Task<ServiceResult<List<DocumentTypeModel>>> ListDocumentTypesAsync(CancellationToken ct = default)
    {
        var types = await _context.DocumentTypes
            .AsNoTracking()
            .Where(dt => dt.IsActive)
            .OrderBy(dt => dt.Id)
            .Select(dt => new DocumentTypeModel
            {
                Id = dt.Id,
                Key = dt.Key,
                DisplayName = dt.DisplayName,
                IsActive = dt.IsActive
            })
            .ToListAsync(ct);

        return ServiceResult<List<DocumentTypeModel>>.SuccessResult(types);
    }

    // ---------------------------------------------------------------- Helpers

    /// <summary>
    /// Normalizes a state code to 2-letter uppercase; null/blank becomes null (the default template).
    /// Returns a friendly error message when a non-blank value is not a 2-letter code.
    /// </summary>
    private static (string? Normalized, string? Error) NormalizeStateCode(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return (null, null);

        var trimmed = stateCode.Trim();
        if (trimmed.Length != 2 || !trimmed.All(char.IsLetter))
            return (null, "State code must be a 2-letter code (e.g. OH), or left blank for the default template.");

        return (trimmed.ToUpperInvariant(), null);
    }

    private static DocumentTemplateModel MapTemplate(DocumentTemplate t, DocumentType documentType)
    {
        var latest = t.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentTemplateVersionSummaryModel
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                Status = v.Status,
                PublishedAt = v.PublishedAt
            })
            .FirstOrDefault();

        return new DocumentTemplateModel
        {
            Id = t.Id,
            StateCode = t.StateCode,
            DocumentTypeId = t.DocumentTypeId,
            DocumentTypeKey = documentType.Key,
            DocumentTypeDisplayName = documentType.DisplayName,
            Name = t.Name,
            CreatedAt = t.CreatedAt,
            LatestVersion = latest
        };
    }
}
