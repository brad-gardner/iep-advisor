using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Resolves the Published template version to pin for a <c>(state, documentType)</c> (see
/// <see cref="ITemplateResolutionService"/>). Only <see cref="TemplateVersionStatus.Published"/>
/// versions are resolvable — a template that has only a Draft is treated as "no version", so
/// resolution falls through to the default (or blocks) rather than pinning an unpublished version.
/// </summary>
public class TemplateResolutionService : ITemplateResolutionService
{
    /// <summary>Friendly, user-facing block message (English reference copy — kept in sync with
    /// <c>Templates.NoTemplateAvailable</c> in Messages.resx, which is what <see cref="ResolveAsync"/>
    /// actually returns). Multilingual plan (2026-10-06) phase 5: the FailureResult call site now carries
    /// <see cref="ServiceErrorKind.Unprocessable"/>, so <c>DocumentInstanceController</c>'s 422 never
    /// depends on matching this (possibly Spanish) text.</summary>
    public const string NoTemplateMessage =
        "No document template is available for this document type yet. Ask an administrator to publish one.";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<TemplateResolutionService> _logger;
    private readonly IStringLocalizer<Messages> _localizer;

    public TemplateResolutionService(ApplicationDbContext context, ILogger<TemplateResolutionService> logger, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<ServiceResult<TemplateResolutionModel>> ResolveAsync(
        string? stateCode, int documentTypeId, CancellationToken ct = default)
    {
        var normalizedState = NormalizeState(stateCode);

        // 1. State-specific Published version, if a normalized state was supplied.
        if (normalizedState != null)
        {
            var stateMatch = await ResolvePublishedAsync(normalizedState, documentTypeId, usedDefault: false, ct);
            if (stateMatch != null)
                return ServiceResult<TemplateResolutionModel>.SuccessResult(stateMatch);
        }

        // 2. Fall back to the default (state-less) Published version.
        var defaultMatch = await ResolvePublishedAsync(null, documentTypeId, usedDefault: true, ct);
        if (defaultMatch != null)
            return ServiceResult<TemplateResolutionModel>.SuccessResult(defaultMatch);

        // 3. Nothing resolvable — block with a friendly message (do NOT pin a wrong type or a Draft).
        _logger.LogInformation(
            "Template resolution blocked: no Published template for documentType {DocumentTypeId} (state {State}).",
            documentTypeId, normalizedState ?? "default");
        return ServiceResult<TemplateResolutionModel>.FailureResult(ServiceErrorKind.Unprocessable, _localizer["Templates.NoTemplateAvailable"]);
    }

    /// <summary>
    /// Finds the highest-numbered Published version of the template for (<paramref name="state"/>,
    /// <paramref name="documentTypeId"/>), or null if the template does not exist or has no Published
    /// version. <paramref name="state"/> null targets the default template.
    /// </summary>
    private async Task<TemplateResolutionModel?> ResolvePublishedAsync(
        string? state, int documentTypeId, bool usedDefault, CancellationToken ct)
    {
        // (StateCode, DocumentTypeId) is unique, so at most one template matches. Branch on null vs. a
        // concrete state so the SQL is an explicit IS NULL rather than relying on parameter null-semantics.
        var templateId = state == null
            ? await _context.DocumentTemplates.AsNoTracking()
                .Where(t => t.DocumentTypeId == documentTypeId && t.StateCode == null)
                .Select(t => (int?)t.Id).FirstOrDefaultAsync(ct)
            : await _context.DocumentTemplates.AsNoTracking()
                .Where(t => t.DocumentTypeId == documentTypeId && t.StateCode == state)
                .Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);

        if (templateId == null)
            return null;

        var version = await _context.DocumentTemplateVersions.AsNoTracking()
            .Where(v => v.DocumentTemplateId == templateId.Value && v.Status == TemplateVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.Id, v.VersionNumber })
            .FirstOrDefaultAsync(ct);

        if (version == null)
            return null;

        return new TemplateResolutionModel
        {
            DocumentTemplateId = templateId.Value,
            DocumentTemplateVersionId = version.Id,
            VersionNumber = version.VersionNumber,
            StateCode = state,
            UsedDefault = usedDefault
        };
    }

    /// <summary>
    /// Normalizes a state code to 2-letter uppercase. null/blank OR a malformed value returns null,
    /// which routes resolution to the default template (a stored malformed state must not throw here —
    /// it simply has no state-specific match). Matches DocumentTemplateService's normalization for the
    /// well-formed case so codes line up with how templates are stored.
    /// </summary>
    private static string? NormalizeState(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return null;

        var trimmed = stateCode.Trim();
        if (trimmed.Length != 2 || !trimmed.All(char.IsLetter))
            return null;

        return trimmed.ToUpperInvariant();
    }
}
