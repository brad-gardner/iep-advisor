using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Cached, plain-language explanation of a whole shared revision (see <see cref="IDraftExplanationService"/>,
/// plan 6, decision 3). Exactly one Claude call per revision — the result is cached in
/// <see cref="SharedDraftExplanation"/> and never regenerated. Runs on the frozen revision's own values,
/// never the live instance and never the heavy AnalysisRun pipeline. Usage is billed to the district, not
/// the parent's subscription, so it never trips <c>SubscriptionService.CanPerformAnalysisAsync</c>.
/// </summary>
public class DraftExplanationService : IDraftExplanationService
{
    private const string NotFoundMessage = "Shared draft revision not found.";
    private const string PermissionMessage = "You do not have permission to access this revision.";
    private const string UnavailableMessage = "Explanations are temporarily unavailable.";
    private const int MaxTokens = 2048;
    private const int DraftCharBudget = 12_000;

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _accessService;
    private readonly IClaudeClient _claude;
    private readonly ILogger<DraftExplanationService> _logger;

    public DraftExplanationService(ApplicationDbContext context, IAccessService accessService, IClaudeClient claude, ILogger<DraftExplanationService> logger)
    {
        _context = context;
        _accessService = accessService;
        _claude = claude;
        _logger = logger;
    }

    public async Task<ServiceResult<DraftExplanationModel>> GetOrGenerateAsync(int parentUserId, int revisionId, CancellationToken ct = default)
    {
        var header = await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.Id == revisionId)
            .Select(r => new
            {
                r.DocumentTemplateVersionId,
                r.ValuesJson,
                SchoolStudentId = r.DocumentInstance.SchoolStudentId,
                DistrictId = r.DocumentInstance.SchoolStudent.DistrictId
            })
            .FirstOrDefaultAsync(ct);
        if (header == null)
            return ServiceResult<DraftExplanationModel>.FailureResult(NotFoundMessage);

        var childId = await ParentAccessResolver.ResolveChildIdAsync(_context, _accessService, parentUserId, header.SchoolStudentId, AccessRole.Viewer, ct);
        if (childId == null)
            return ServiceResult<DraftExplanationModel>.FailureResult(PermissionMessage);

        var cached = await _context.SharedDraftExplanations.AsNoTracking()
            .FirstOrDefaultAsync(e => e.SharedDraftRevisionId == revisionId, ct);
        if (cached != null)
            return ServiceResult<DraftExplanationModel>.SuccessResult(Deserialize(revisionId, cached));

        var sections = await TemplateSectionLoader.LoadAsync(_context, header.DocumentTemplateVersionId, ct);
        var rendered = DraftPromptBuilder.RenderDraft(sections, ValueDocumentJson.Parse(header.ValuesJson), DraftCharBudget);

        string? reply;
        try
        {
            reply = await _claude.CompleteAsync(new ClaudeCompletionRequest
            {
                SystemPrompt = DraftPrompts.Explanation,
                UserText = rendered.Text + "\n\n" + DraftPrompts.ExplanationInstruction,
                MaxTokens = MaxTokens
            }, ct);
        }
        catch (ClaudeApiException ex)
        {
            _logger.LogError(ex, "Draft explanation for revision {RevisionId} failed with {Kind}", revisionId, ex.Kind);
            return ServiceResult<DraftExplanationModel>.FailureResult(UnavailableMessage);
        }

        if (string.IsNullOrWhiteSpace(reply))
        {
            _logger.LogWarning("Draft explanation: Claude returned no content for revision {RevisionId}.", revisionId);
            return ServiceResult<DraftExplanationModel>.FailureResult(UnavailableMessage);
        }

        var parsed = ParseExplanation(reply, rendered);
        if (parsed == null)
        {
            _logger.LogWarning("Draft explanation: could not parse a usable reply for revision {RevisionId}.", revisionId);
            return ServiceResult<DraftExplanationModel>.FailureResult(UnavailableMessage);
        }

        var now = DateTime.UtcNow;
        parsed.RevisionId = revisionId;
        parsed.GeneratedAt = now;

        var entity = new SharedDraftExplanation
        {
            SharedDraftRevisionId = revisionId,
            ExplanationJson = JsonSerializer.Serialize(parsed),
            GeneratedAt = now,
            CreatedById = parentUserId,
            UpdatedById = parentUserId
        };

        try
        {
            await _context.SharedDraftExplanations.AddAsync(entity, ct);
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race to cache this revision's explanation (unique index) — read back the winner.
            _context.ChangeTracker.Clear();
            var winner = await _context.SharedDraftExplanations.AsNoTracking()
                .FirstOrDefaultAsync(e => e.SharedDraftRevisionId == revisionId, ct);
            if (winner != null)
                return ServiceResult<DraftExplanationModel>.SuccessResult(Deserialize(revisionId, winner));
            throw;
        }

        // District-billed usage — never the parent's own subscription/paywall.
        await _context.UsageRecords.AddAsync(new UsageRecord
        {
            UserId = parentUserId,
            ChildProfileId = childId.Value,
            DistrictId = header.DistrictId,
            OperationType = "draft_explanation",
            CreatedAt = now
        }, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<DraftExplanationModel>.SuccessResult(parsed);
    }

    private static DraftExplanationModel Deserialize(int revisionId, SharedDraftExplanation entity)
    {
        var model = JsonSerializer.Deserialize<DraftExplanationModel>(entity.ExplanationJson) ?? new DraftExplanationModel();
        model.RevisionId = revisionId;
        model.GeneratedAt = entity.GeneratedAt;
        model.Disclaimer = string.IsNullOrWhiteSpace(model.Disclaimer) ? DraftPrompts.Disclaimer : model.Disclaimer;
        return model;
    }

    /// <summary>Parses `{ sections: [...], items: [...] }`; unknown item ids (not among the rendered draft's lines) are dropped. Returns null when nothing usable was produced.</summary>
    private static DraftExplanationModel? ParseExplanation(string raw, RenderedDraft rendered)
    {
        using var doc = TolerantJsonParser.TryParseObject(raw);
        if (doc == null)
            return null;
        var root = doc.RootElement;

        var sections = new List<ExplanationSectionModel>();
        if (root.TryGetProperty("sections", out var sectionsEl) && sectionsEl.ValueKind == JsonValueKind.Array)
        {
            var seq = 0;
            foreach (var s in sectionsEl.EnumerateArray())
            {
                var title = s.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()?.Trim() : null;
                var explanation = s.TryGetProperty("explanation", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(explanation))
                    continue;
                sections.Add(new ExplanationSectionModel { SectionId = $"s{++seq}", Title = title!, Explanation = explanation! });
            }
        }

        var items = new List<ExplanationItemModel>();
        if (root.TryGetProperty("items", out var itemsEl) && itemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in itemsEl.EnumerateArray())
            {
                var id = i.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String ? idEl.GetString() : null;
                var explanation = i.TryGetProperty("explanation", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()?.Trim() : null;
                if (id == null || string.IsNullOrWhiteSpace(explanation))
                    continue;
                var line = rendered.Resolve(id);
                if (line == null)
                    continue; // unknown id — dropped, never surfaced
                items.Add(new ExplanationItemModel { FieldKey = line.FieldKey, RowId = line.RowId, Label = line.Label, Explanation = explanation! });
            }
        }

        if (sections.Count == 0 && items.Count == 0)
            return null;

        return new DraftExplanationModel { Sections = sections, Items = items, Disclaimer = DraftPrompts.Disclaimer };
    }
}
