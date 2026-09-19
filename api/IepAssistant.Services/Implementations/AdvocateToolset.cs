using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// The Virtual Advocate's read-only tools, closed over one (child, parent) pair for one turn. This class is
/// the whole boundary between the model and the family's record: every tool re-scopes its query to
/// <c>_childId</c> (a model-supplied id that belongs to any other child — the same parent's or a
/// stranger's — is "Not found." with no existence hint), wraps every free-text field with
/// <see cref="PromptText.Data"/>, records each returned <c>sourceRef</c> in <see cref="ReturnedRefs"/> (the
/// citation allow-list), and caps its output — per call (<see cref="PerToolCharCap"/>) and per turn
/// (<see cref="PerTurnCharBudget"/>). Nothing staff-only has a code path in.
/// </summary>
/// <remarks>
/// Adding a tool = one definition + one handler registered in the constructor. Unknown tools and inputs that
/// fail their schema throw <see cref="ToolExecutionException"/> (the model sees <c>is_error</c> and can
/// recover); any other failure inside a handler is logged and rethrown as a generic
/// <see cref="ToolExecutionException"/> so one bad lookup cannot kill the turn.
/// </remarks>
public sealed class AdvocateToolset : IToolExecutor
{
    public const int PerToolCharCap = 6_000;
    public const int PerTurnCharBudget = 30_000;
    public const int MaxKnowledgeBaseEntries = 8;
    public const int MaxSummaryChars = 600;
    public const int MaxJournalEntries = 30;
    public const int MaxJournalChars = 600;
    public const int MaxContributionChars = 600;
    public const int MaxListItems = 40;
    public const int MaxSectionChars = 4_500;
    public const int MaxAnalysisStringChars = 400;
    public const int MaxObservationsPerGoal = 12;
    public const int SharedDraftCharBudget = 5_000;
    public const string NotFoundMessage = "Not found.";
    private const int MaxStringInputLength = 300;
    private const int MaxLabelChars = 60;

    private delegate Task<ToolPayload> ToolHandler(JsonElement input, CancellationToken ct);

    private sealed record ToolEntry(ClaudeToolDefinition Definition, ToolHandler Handler);

    /// <summary>A handler's result: the payload plus the arrays to drop items from (longest first) if it is over the per-tool cap.</summary>
    private sealed class ToolPayload
    {
        public ToolPayload(JsonObject payload, params JsonArray[] trimmable)
        {
            Payload = payload;
            Trimmable = trimmable;
        }

        public JsonObject Payload { get; }
        public JsonArray[] Trimmable { get; }
    }

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;
    private readonly IKnowledgeBaseService _knowledgeBase;
    private readonly IIepComparisonService _comparison;
    private readonly ILogger _logger;
    private readonly int _childId;
    private readonly int _userId;
    private readonly string? _stateCode;
    private readonly Dictionary<string, ToolEntry> _tools;
    private int _remainingChars = PerTurnCharBudget;

    public AdvocateToolset(
        ApplicationDbContext context,
        IAccessService access,
        IKnowledgeBaseService knowledgeBase,
        IIepComparisonService comparison,
        int childId,
        int userId,
        string? stateCode,
        ILogger? logger = null)
    {
        _context = context;
        _access = access;
        _knowledgeBase = knowledgeBase;
        _comparison = comparison;
        _childId = childId;
        _userId = userId;
        _stateCode = stateCode;
        _logger = logger ?? NullLogger.Instance;

        // Registration order is the order the model sees the tools in — keep it stable.
        var registry = new List<ToolEntry>
        {
            new(SearchKnowledgeBaseDefinition, SearchKnowledgeBaseAsync),
            new(GetChildSummaryDefinition, GetChildSummaryAsync),
            new(ListDocumentsDefinition, ListDocumentsAsync),
            new(GetDocumentAnalysisDefinition, GetDocumentAnalysisAsync),
            new(GetDocumentSectionDefinition, GetDocumentSectionAsync),
            new(GetGoalsAndProgressDefinition, GetGoalsAndProgressAsync),
            new(CompareIepVersionsDefinition, CompareIepVersionsAsync),
            new(ListJournalDefinition, ListJournalAsync),
            new(ListContributionsDefinition, ListContributionsAsync),
            new(ListAdvocacyGoalsDefinition, ListAdvocacyGoalsAsync),
            new(GetMeetingPrepDefinition, GetMeetingPrepAsync),
            new(ListMeetingsAndDeadlinesDefinition, ListMeetingsAndDeadlinesAsync),
            new(GetSharedDraftDefinition, GetSharedDraftAsync)
        };
        _tools = registry.ToDictionary(t => t.Definition.Name, StringComparer.Ordinal);
        Definitions = registry.Select(t => t.Definition).ToList();
    }

    /// <summary>Tool definitions in registration order — stable so the cached tool block does not churn.</summary>
    public IReadOnlyList<ClaudeToolDefinition> Definitions { get; }

    /// <summary>Every <c>sourceRef</c> (<c>kind:id</c>) a tool returned this turn — the only refs the answer may cite.</summary>
    public HashSet<string> ReturnedRefs { get; } = new(StringComparer.Ordinal);

    /// <summary>Human labels for returned refs (KB titles, "IEP 2026-03-12", goal domains…), for citation chips.</summary>
    public Dictionary<string, string> Labels { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// For returned refs that have no page of their own, the record to open them inside (goal, iep_section,
    /// iep_analysis → the iep; etr_section, etr_analysis → the etr; progress_report → its iep;
    /// progress_report_analysis → the progress report). Kinds that are their own page have no entry.
    /// </summary>
    public Dictionary<string, AdvocateCitationParent> Parents { get; } = new(StringComparer.Ordinal);

    public async Task<string> ExecuteAsync(string toolName, JsonElement input, CancellationToken cancellationToken)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
            throw new ToolExecutionException($"Unknown tool '{PromptText.Data(PromptText.Truncate(toolName, 60))}'.");

        ToolInputValidator.Validate(input, tool.Definition.InputSchema, MaxStringInputLength);

        if (_remainingChars <= 0)
            return new JsonObject { ["truncated"] = true, ["reason"] = "budget" }.ToJsonString();

        ToolPayload result;
        try
        {
            result = await tool.Handler(input, cancellationToken);
        }
        catch (ToolExecutionException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Advocate tool {Tool} failed for child {ChildId}", toolName, _childId);
            throw new ToolExecutionException("This lookup failed.");
        }

        var json = FitToCap(result);
        _remainingChars -= json.Length;
        return json;
    }

    // ------------------------------------------------------------------ search_knowledge_base

    private static readonly ClaudeToolDefinition SearchKnowledgeBaseDefinition = new(
        "search_knowledge_base",
        "Search the plain-language knowledge base of special-education rights, processes, IEP/ETR provisions and " +
        "glossary terms. Returns federal (IDEA) entries plus entries for the child's state. Use it before " +
        "explaining a right, a timeline or a process. Each entry has a sourceRef to cite.",
        ToolSchema.Object(
            ("query", "string", "Keywords or a short phrase, e.g. \"prior written notice\" or \"evaluation timeline\".", true),
            ("category", "string", "Optional category filter: rights, process, provisions or glossary.", false)));

    private async Task<ToolPayload> SearchKnowledgeBaseAsync(JsonElement input, CancellationToken ct)
    {
        var query = input.GetProperty("query").GetString()!.Trim();
        if (query.Length == 0)
            throw new ToolExecutionException("query must not be empty.");
        var category = OptionalString(input, "category");

        var results = await _knowledgeBase.SearchAsync(query, category, _stateCode, ct);
        // Defence in depth: the service leaves state unfiltered when no state is given; the advocate only
        // ever sees federal entries plus the child's own state. The state's own entries come first (stable
        // within each group) so the more specific rule is never the one the cap drops.
        var entries = results
            .Where(e => e.State == null || (_stateCode != null && string.Equals(e.State, _stateCode, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.State == null ? 1 : 0)
            .Take(MaxKnowledgeBaseEntries)
            .ToList();

        var array = new JsonArray();
        foreach (var e in entries)
        {
            var sourceRef = Register("kb", e.Id, e.Title);
            array.Add(new JsonObject
            {
                ["sourceRef"] = sourceRef,
                ["title"] = PromptText.Data(e.Title),
                ["category"] = PromptText.Data(e.Category),
                ["legalReference"] = Str(e.LegalReference, MaxSummaryChars),
                ["state"] = e.State == null ? "federal" : PromptText.Data(e.State),
                ["summary"] = PromptText.Data(PromptText.Truncate(e.Content, MaxSummaryChars))
            });
        }

        var payload = new JsonObject
        {
            ["state"] = StateValue(),
            ["entries"] = array
        };
        return new ToolPayload(payload, array);
    }

    // ------------------------------------------------------------------ get_child_summary

    private static readonly ClaudeToolDefinition GetChildSummaryDefinition = new(
        "get_child_summary",
        "Read the child's profile: first name, grade, disability category, school district, state, how many " +
        "IEPs, ETRs, progress reports and journal entries are on file, and the next scheduled meeting date.",
        ToolSchema.Object());

    private async Task<ToolPayload> GetChildSummaryAsync(JsonElement input, CancellationToken ct)
    {
        var child = await _context.ChildProfiles.AsNoTracking()
            .Where(c => c.Id == _childId)
            .Select(c => new { c.Id, c.FirstName, c.GradeLevel, c.DisabilityCategory, c.SchoolDistrict })
            .FirstOrDefaultAsync(ct);
        if (child == null)
            throw new ToolExecutionException(NotFoundMessage);

        var now = DateTime.UtcNow;
        var ieps = await _context.IepDocuments.CountAsync(d => d.ChildProfileId == _childId, ct);
        var etrs = await _context.EtrDocuments.CountAsync(d => d.ChildProfileId == _childId, ct);
        var progressReports = await _context.ProgressReports.CountAsync(r => r.ChildProfileId == _childId, ct);
        var journalEntries = await _context.JournalEntries.CountAsync(j => j.ChildProfileId == _childId, ct);

        var linkedStudentIds = LinkedStudentIds();
        var nextMeeting = await _context.Meetings.AsNoTracking()
            .Where(m => linkedStudentIds.Contains(m.SchoolStudentId) && m.Status == MeetingStatus.Scheduled && m.StartsAtUtc >= now)
            .OrderBy(m => m.StartsAtUtc)
            .Select(m => (DateTime?)m.StartsAtUtc)
            .FirstOrDefaultAsync(ct);

        var sourceRef = Register("child", child.Id, child.FirstName);
        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["firstName"] = PromptText.Data(child.FirstName),
            ["gradeLevel"] = Str(child.GradeLevel, MaxLabelChars),
            ["disabilityCategory"] = Str(child.DisabilityCategory, MaxLabelChars),
            ["schoolDistrict"] = Str(child.SchoolDistrict, MaxLabelChars),
            ["state"] = StateValue(),
            ["counts"] = new JsonObject
            {
                ["ieps"] = ieps,
                ["etrs"] = etrs,
                ["progressReports"] = progressReports,
                ["journalEntries"] = journalEntries
            },
            ["nextMeetingDate"] = Date(nextMeeting)
        };
        return new ToolPayload(payload);
    }

    // ------------------------------------------------------------------ list_documents

    private static readonly ClaudeToolDefinition ListDocumentsDefinition = new(
        "list_documents",
        "List everything on file for this child, newest first: uploaded IEPs, ETRs (evaluations) and progress " +
        "reports with their dates and whether an analysis exists; finalized documents the school authored; and " +
        "draft revisions the school shared with the family. Use it first to find the right document id before " +
        "reading a document, its analysis or its goals.",
        ToolSchema.Object());

    private async Task<ToolPayload> ListDocumentsAsync(JsonElement input, CancellationToken ct)
    {
        var ieps = await _context.IepDocuments.AsNoTracking()
            .Where(d => d.ChildProfileId == _childId && d.IsActive)
            .OrderByDescending(d => d.IepDate ?? d.UploadDate).ThenByDescending(d => d.Id)
            .Take(MaxListItems)
            .Select(d => new
            {
                d.Id, d.IepDate, d.UploadDate, d.Status, d.MeetingType,
                AnalysisStatus = _context.IepAnalyses.Where(a => a.IepDocumentId == d.Id).OrderByDescending(a => a.Id).Select(a => a.Status).FirstOrDefault()
            })
            .ToListAsync(ct);
        var etrs = await _context.EtrDocuments.AsNoTracking()
            .Where(d => d.ChildProfileId == _childId && d.IsActive)
            .OrderByDescending(d => d.EvaluationDate ?? d.UploadDate).ThenByDescending(d => d.Id)
            .Take(MaxListItems)
            .Select(d => new
            {
                d.Id, d.EvaluationDate, d.UploadDate, d.Status, d.EvaluationType, d.DocumentState,
                AnalysisStatus = _context.EtrAnalyses.Where(a => a.EtrDocumentId == d.Id).OrderByDescending(a => a.Id).Select(a => a.Status).FirstOrDefault()
            })
            .ToListAsync(ct);
        var reports = await _context.ProgressReports.AsNoTracking()
            .Where(r => r.ChildProfileId == _childId && r.IsActive)
            .OrderByDescending(r => r.ReportingPeriodEnd ?? r.UploadDate).ThenByDescending(r => r.Id)
            .Take(MaxListItems)
            .Select(r => new
            {
                r.Id, r.IepDocumentId, r.ReportingPeriodStart, r.ReportingPeriodEnd, r.UploadDate, r.Status,
                AnalysisStatus = _context.ProgressReportAnalyses.Where(a => a.ProgressReportId == r.Id).OrderByDescending(a => a.Id).Select(a => a.Status).FirstOrDefault()
            })
            .ToListAsync(ct);

        var linkedStudentIds = LinkedStudentIds();
        // Same rule as AuthoredDocumentVersionService.ListForChildAsync: a version is parent-visible when its
        // student is linked to this child through an active, accepted ChildLink. Every row is finalized.
        var versions = await _context.AuthoredDocumentVersions.AsNoTracking()
            .Where(v => linkedStudentIds.Contains(v.SchoolStudentId))
            .OrderByDescending(v => v.FinalizedAt).ThenByDescending(v => v.Id)
            .Take(MaxListItems)
            .Select(v => new { v.Id, v.VersionNumber, v.FinalizedAt, v.EffectiveDate, DocumentType = v.DocumentType.DisplayName, v.SignatureStatus })
            .ToListAsync(ct);
        // Same rule as DraftSharingService.ListForParentAsync.
        var drafts = await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => linkedStudentIds.Contains(r.DocumentInstance.SchoolStudentId))
            .OrderByDescending(r => r.SharedAt).ThenByDescending(r => r.Id)
            .Take(MaxListItems)
            .Select(r => new { r.Id, r.RevisionNumber, r.SharedAt, r.Status, DocumentType = r.DocumentInstance.DocumentType.DisplayName })
            .ToListAsync(ct);

        var iepArray = new JsonArray();
        foreach (var d in ieps)
        {
            iepArray.Add(new JsonObject
            {
                ["sourceRef"] = Register("iep", d.Id, IepLabel(d.IepDate, d.UploadDate)),
                ["iepDate"] = Date(d.IepDate),
                ["uploadDate"] = Date(d.UploadDate),
                ["status"] = Str(d.Status, MaxLabelChars),
                ["analysisStatus"] = d.AnalysisStatus == null ? "none" : Str(d.AnalysisStatus, MaxLabelChars),
                ["meetingType"] = Str(d.MeetingType, MaxLabelChars)
            });
        }
        var etrArray = new JsonArray();
        foreach (var d in etrs)
        {
            etrArray.Add(new JsonObject
            {
                ["sourceRef"] = Register("etr", d.Id, EtrLabel(d.EvaluationDate, d.UploadDate)),
                ["evaluationDate"] = Date(d.EvaluationDate),
                ["uploadDate"] = Date(d.UploadDate),
                ["status"] = Str(d.Status, MaxLabelChars),
                ["analysisStatus"] = d.AnalysisStatus == null ? "none" : Str(d.AnalysisStatus, MaxLabelChars),
                ["evaluationType"] = Str(d.EvaluationType, MaxLabelChars),
                ["documentState"] = Str(d.DocumentState, MaxLabelChars)
            });
        }
        var reportArray = new JsonArray();
        foreach (var r in reports)
        {
            reportArray.Add(new JsonObject
            {
                ["sourceRef"] = Register("progress_report", r.Id, ProgressReportLabel(r.ReportingPeriodEnd, r.UploadDate), IepRef(r.IepDocumentId)),
                ["iepId"] = r.IepDocumentId,
                ["periodStart"] = Date(r.ReportingPeriodStart),
                ["periodEnd"] = Date(r.ReportingPeriodEnd),
                ["uploadDate"] = Date(r.UploadDate),
                ["status"] = Str(r.Status, MaxLabelChars),
                ["analysisStatus"] = r.AnalysisStatus == null ? "none" : Str(r.AnalysisStatus, MaxLabelChars)
            });
        }
        var versionArray = new JsonArray();
        foreach (var v in versions)
        {
            versionArray.Add(new JsonObject
            {
                ["sourceRef"] = Register("authored_version", v.Id, $"{v.DocumentType} v{v.VersionNumber} ({v.FinalizedAt:yyyy-MM-dd})"),
                ["documentType"] = Str(v.DocumentType, MaxLabelChars),
                ["versionNumber"] = v.VersionNumber,
                ["finalizedAt"] = Date(v.FinalizedAt),
                ["effectiveDate"] = Date(v.EffectiveDate),
                ["signatureStatus"] = v.SignatureStatus.ToString()
            });
        }
        var draftArray = new JsonArray();
        foreach (var r in drafts)
        {
            draftArray.Add(new JsonObject
            {
                ["sourceRef"] = Register("shared_draft", r.Id, $"Shared {r.DocumentType} draft, revision {r.RevisionNumber}"),
                ["documentType"] = Str(r.DocumentType, MaxLabelChars),
                ["revisionNumber"] = r.RevisionNumber,
                ["sharedAt"] = Date(r.SharedAt),
                ["status"] = r.Status.ToString()
            });
        }

        var payload = new JsonObject
        {
            ["ieps"] = iepArray,
            ["etrs"] = etrArray,
            ["progressReports"] = reportArray,
            ["authoredVersions"] = versionArray,
            ["sharedDrafts"] = draftArray
        };
        return new ToolPayload(payload, iepArray, etrArray, reportArray, versionArray, draftArray);
    }

    // ------------------------------------------------------------------ get_document_analysis

    private static readonly ClaudeToolDefinition GetDocumentAnalysisDefinition = new(
        "get_document_analysis",
        "Read the stored analysis of one uploaded IEP, ETR or progress report: plain-language summary, red " +
        "flags, goal-by-goal SMART ratings (IEP), assessment completeness and eligibility review (ETR), or " +
        "goal progress findings (progress report), plus how the parent's own priorities line up. Get the " +
        "documentId from list_documents.",
        ToolSchema.Object(
            ("documentType", "string", "One of: iep, etr, progress_report.", true),
            ("documentId", "integer", "The document's id from list_documents.", true)));

    private const string NoAnalysisHint = "The parent can run an analysis from the document page.";

    private async Task<ToolPayload> GetDocumentAnalysisAsync(JsonElement input, CancellationToken ct)
    {
        var documentType = RequireEnum(input, "documentType", "iep", "etr", "progress_report");
        var documentId = input.GetProperty("documentId").GetInt32();

        return documentType switch
        {
            "iep" => await GetIepAnalysisAsync(documentId, ct),
            "etr" => await GetEtrAnalysisAsync(documentId, ct),
            _ => await GetProgressReportAnalysisAsync(documentId, ct)
        };
    }

    private async Task<ToolPayload> GetIepAnalysisAsync(int iepId, CancellationToken ct)
    {
        var iep = await RequireIepAsync(iepId, ct);
        var documentRef = Register("iep", iep.Id, IepLabel(iep.IepDate, iep.UploadDate));

        var analysis = await _context.IepAnalyses.AsNoTracking()
            .Where(a => a.IepDocumentId == iep.Id)
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Status, a.OverallSummary, a.OverallRedFlags, a.GoalAnalyses, a.SectionAnalyses, a.AdvocacyGapAnalysis })
            .FirstOrDefaultAsync(ct);
        if (analysis == null || !IsCompleted(analysis.Status))
            return NoAnalysis(documentRef, analysis?.Status);

        var sourceRef = Register("iep_analysis", analysis.Id, $"Analysis of {Labels[documentRef]}", documentRef);

        // goalId values inside the stored JSON are only trusted when they are goals of THIS IEP.
        var goalIds = (await _context.Goals.AsNoTracking()
            .Where(g => g.IepSection.IepDocumentId == iep.Id)
            .Select(g => new { g.Id, g.Domain, g.GoalText })
            .ToListAsync(ct)).ToDictionary(g => g.Id, g => g);
        var goalAnalyses = ParseArray(analysis.GoalAnalyses);
        foreach (var node in goalAnalyses.OfType<JsonObject>())
        {
            if (node["goalId"] is JsonValue idValue && idValue.TryGetValue<int>(out var goalId) && goalIds.TryGetValue(goalId, out var goal))
                node["sourceRef"] = Register("goal", goal.Id, GoalLabel(goal.Domain, goal.GoalText), documentRef);
        }
        var sectionAnalyses = ParseArray(analysis.SectionAnalyses);
        var redFlags = ParseArray(analysis.OverallRedFlags);
        var run = await LatestRunSectionsAsync(AnalysisSourceType.IepDocument, iep.Id, ct);

        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["documentRef"] = documentRef,
            ["status"] = "completed",
            ["overallSummary"] = Str(analysis.OverallSummary, 1_500),
            ["overallRedFlags"] = redFlags,
            ["goalAnalyses"] = goalAnalyses,
            ["sectionAnalyses"] = sectionAnalyses,
            ["advocacyGapAnalysis"] = ParseOrText(analysis.AdvocacyGapAnalysis),
            ["analysisRun"] = run.Payload
        };
        return new ToolPayload(payload, goalAnalyses, sectionAnalyses, redFlags, run.Sections);
    }

    private async Task<ToolPayload> GetEtrAnalysisAsync(int etrId, CancellationToken ct)
    {
        var etr = await RequireEtrAsync(etrId, ct);
        var documentRef = Register("etr", etr.Id, EtrLabel(etr.EvaluationDate, etr.UploadDate));

        var analysis = await _context.EtrAnalyses.AsNoTracking()
            .Where(a => a.EtrDocumentId == etr.Id)
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Status, a.OverallSummary, a.AssessmentCompleteness, a.EligibilityReview, a.OverallRedFlags, a.AdvocacyGapAnalysis })
            .FirstOrDefaultAsync(ct);
        if (analysis == null || !IsCompleted(analysis.Status))
            return NoAnalysis(documentRef, analysis?.Status);

        var sourceRef = Register("etr_analysis", analysis.Id, $"Analysis of {Labels[documentRef]}", documentRef);
        var redFlags = ParseArray(analysis.OverallRedFlags);
        var run = await LatestRunSectionsAsync(AnalysisSourceType.EtrDocument, etr.Id, ct);

        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["documentRef"] = documentRef,
            ["status"] = "completed",
            ["overallSummary"] = Str(analysis.OverallSummary, 1_500),
            ["assessmentCompleteness"] = ParseOrText(analysis.AssessmentCompleteness),
            ["eligibilityReview"] = ParseOrText(analysis.EligibilityReview),
            ["overallRedFlags"] = redFlags,
            ["advocacyGapAnalysis"] = ParseOrText(analysis.AdvocacyGapAnalysis),
            ["analysisRun"] = run.Payload
        };
        return new ToolPayload(payload, redFlags, run.Sections);
    }

    private async Task<ToolPayload> GetProgressReportAnalysisAsync(int reportId, CancellationToken ct)
    {
        var report = await _context.ProgressReports.AsNoTracking()
            .Where(r => r.Id == reportId && r.ChildProfileId == _childId && r.IsActive)
            .Select(r => new { r.Id, r.IepDocumentId, r.ReportingPeriodStart, r.ReportingPeriodEnd, r.UploadDate })
            .FirstOrDefaultAsync(ct);
        if (report == null)
            throw new ToolExecutionException(NotFoundMessage);
        var documentRef = Register("progress_report", report.Id, ProgressReportLabel(report.ReportingPeriodEnd, report.UploadDate), IepRef(report.IepDocumentId));

        var analysis = await _context.ProgressReportAnalyses.AsNoTracking()
            .Where(a => a.ProgressReportId == report.Id)
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.Id, a.Status, a.Summary, a.GoalProgressFindings, a.RedFlags, a.AdvocacyGapAnalysis })
            .FirstOrDefaultAsync(ct);
        if (analysis == null || !IsCompleted(analysis.Status))
            return NoAnalysis(documentRef, analysis?.Status);

        var sourceRef = Register("progress_report_analysis", analysis.Id, $"Analysis of {Labels[documentRef]}", documentRef);
        var findings = ParseArray(analysis.GoalProgressFindings);
        var redFlags = ParseArray(analysis.RedFlags);
        var run = await LatestRunSectionsAsync(AnalysisSourceType.ProgressReport, report.Id, ct);

        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["documentRef"] = documentRef,
            ["iepId"] = report.IepDocumentId,
            ["periodStart"] = Date(report.ReportingPeriodStart),
            ["periodEnd"] = Date(report.ReportingPeriodEnd),
            ["status"] = "completed",
            ["summary"] = Str(analysis.Summary, 1_500),
            ["goalProgressFindings"] = findings,
            ["redFlags"] = redFlags,
            ["advocacyGapAnalysis"] = ParseOrText(analysis.AdvocacyGapAnalysis),
            ["analysisRun"] = run.Payload
        };
        return new ToolPayload(payload, findings, redFlags, run.Sections);
    }

    private static ToolPayload NoAnalysis(string documentRef, string? status) => new(new JsonObject
    {
        ["documentRef"] = documentRef,
        ["status"] = status == null ? "none" : PromptText.Data(PromptText.Truncate(status, MaxLabelChars)),
        ["hint"] = NoAnalysisHint
    });

    private sealed record RunSections(JsonObject? Payload, JsonArray Sections);

    /// <summary>The newest completed multi-document analysis run for this child that used the given document as a source: its summary plus the sections about that document.</summary>
    private async Task<RunSections> LatestRunSectionsAsync(AnalysisSourceType sourceType, int sourceId, CancellationToken ct)
    {
        var sections = new JsonArray();
        var source = await _context.AnalysisRunSources.AsNoTracking()
            .Where(s => s.SourceType == sourceType && s.SourceId == sourceId
                        && s.AnalysisRun.ChildProfileId == _childId && s.AnalysisRun.Status == AnalysisRunStatus.Completed)
            .OrderByDescending(s => s.AnalysisRunId)
            .Select(s => new { s.Id, s.AnalysisRunId, s.AnalysisRun.OverallSummary, s.AnalysisRun.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (source == null)
            return new RunSections(null, sections);

        var rows = await _context.AnalysisRunSections.AsNoTracking()
            .Where(s => s.AnalysisRunId == source.AnalysisRunId && s.AnalysisRunSourceId == source.Id)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id)
            .Select(s => new { s.SectionKind, s.Analysis })
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            sections.Add(new JsonObject
            {
                ["sectionKind"] = Str(row.SectionKind, MaxLabelChars),
                ["analysis"] = ParseOrText(row.Analysis)
            });
        }

        var sourceRef = Register("analysis_run", source.AnalysisRunId, $"Analysis run {source.CreatedAt:yyyy-MM-dd}");
        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["ranOn"] = Date(source.CreatedAt),
            ["overallSummary"] = Str(source.OverallSummary, 1_000),
            ["sections"] = sections
        };
        return new RunSections(payload, sections);
    }

    // ------------------------------------------------------------------ get_document_section

    private static readonly ClaudeToolDefinition GetDocumentSectionDefinition = new(
        "get_document_section",
        "Read the actual text of an uploaded IEP or ETR. Without sectionType it lists the document's sections " +
        "(present_levels, annual_goals, services, accommodations, placement, transition, …) and their size; with " +
        "sectionType it returns that section's text. Use it to quote what the document actually says.",
        ToolSchema.Object(
            ("documentType", "string", "One of: iep, etr.", true),
            ("documentId", "integer", "The document's id from list_documents.", true),
            ("sectionType", "string", "Optional section type exactly as listed, e.g. \"present_levels\".", false)));

    private async Task<ToolPayload> GetDocumentSectionAsync(JsonElement input, CancellationToken ct)
    {
        var documentType = RequireEnum(input, "documentType", "iep", "etr");
        var documentId = input.GetProperty("documentId").GetInt32();
        var sectionType = OptionalString(input, "sectionType");

        string documentRef;
        string refKind;
        List<SectionRow> sections;
        if (documentType == "iep")
        {
            var iep = await RequireIepAsync(documentId, ct);
            documentRef = Register("iep", iep.Id, IepLabel(iep.IepDate, iep.UploadDate));
            refKind = "iep_section";
            sections = await _context.IepSections.AsNoTracking()
                .Where(s => s.IepDocumentId == iep.Id)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id)
                .Select(s => new SectionRow(s.Id, s.SectionType, s.DisplayOrder, s.RawText, s.ParsedContent))
                .ToListAsync(ct);
        }
        else
        {
            var etr = await RequireEtrAsync(documentId, ct);
            documentRef = Register("etr", etr.Id, EtrLabel(etr.EvaluationDate, etr.UploadDate));
            refKind = "etr_section";
            sections = await _context.EtrSections.AsNoTracking()
                .Where(s => s.EtrDocumentId == etr.Id)
                .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Id)
                .Select(s => new SectionRow(s.Id, s.SectionType, s.DisplayOrder, s.RawText, s.ParsedContent))
                .ToListAsync(ct);
        }

        if (sectionType == null)
        {
            var list = new JsonArray();
            foreach (var s in sections)
            {
                list.Add(new JsonObject
                {
                    ["sourceRef"] = Register(refKind, s.Id, SectionLabel(s.SectionType, Labels[documentRef]), documentRef),
                    ["sectionType"] = Str(s.SectionType, MaxLabelChars),
                    ["displayOrder"] = s.DisplayOrder,
                    ["chars"] = (s.RawText ?? s.ParsedContent ?? string.Empty).Length
                });
            }
            return new ToolPayload(new JsonObject { ["documentRef"] = documentRef, ["sections"] = list }, list);
        }

        var section = sections.FirstOrDefault(s => string.Equals(s.SectionType.Trim(), sectionType, StringComparison.OrdinalIgnoreCase));
        if (section == null)
        {
            var available = new JsonArray();
            foreach (var s in sections) available.Add(Str(s.SectionType, MaxLabelChars));
            return new ToolPayload(new JsonObject
            {
                ["documentRef"] = documentRef,
                ["status"] = "no_such_section",
                ["availableSections"] = available
            }, available);
        }

        var text = section.RawText ?? section.ParsedContent;
        var payload = new JsonObject
        {
            ["sourceRef"] = Register(refKind, section.Id, SectionLabel(section.SectionType, Labels[documentRef]), documentRef),
            ["documentRef"] = documentRef,
            ["sectionType"] = Str(section.SectionType, MaxLabelChars),
            ["source"] = section.RawText != null ? "raw_text" : "parsed_content",
            ["truncated"] = text != null && text.Trim().Length > MaxSectionChars,
            ["text"] = text == null ? null : PromptText.Data(PromptText.Truncate(text, MaxSectionChars))
        };
        return new ToolPayload(payload);
    }

    private sealed record SectionRow(int Id, string SectionType, int DisplayOrder, string? RawText, string? ParsedContent);

    // ------------------------------------------------------------------ get_goals_and_progress

    private static readonly ClaudeToolDefinition GetGoalsAndProgressDefinition = new(
        "get_goals_and_progress",
        "Read the annual goals of one uploaded IEP (default: the child's current IEP) — goal text, baseline, " +
        "target criteria, how it is measured, timeframe — plus the school's goal records and progress " +
        "observations when the school tracks them. Use it for any question about goals, baselines, " +
        "measurability or progress.",
        ToolSchema.Object(
            ("iepDocumentId", "integer", "Optional IEP id from list_documents; omit for the current IEP.", false)));

    private async Task<ToolPayload> GetGoalsAndProgressAsync(JsonElement input, CancellationToken ct)
    {
        IepHeader? iep;
        if (input.TryGetProperty("iepDocumentId", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
        {
            iep = await RequireIepAsync(idProp.GetInt32(), ct);
        }
        else
        {
            var currentId = await _context.ChildProfiles.AsNoTracking()
                .Where(c => c.Id == _childId)
                .Select(c => c.CurrentIepDocumentId)
                .FirstOrDefaultAsync(ct);
            iep = currentId == null ? null : await FindIepAsync(currentId.Value, ct);
            iep ??= await _context.IepDocuments.AsNoTracking()
                .Where(d => d.ChildProfileId == _childId && d.IsActive)
                .OrderByDescending(d => d.IepDate ?? d.UploadDate).ThenByDescending(d => d.Id)
                .Select(d => new IepHeader(d.Id, d.IepDate, d.UploadDate))
                .FirstOrDefaultAsync(ct);
        }

        var goalArray = new JsonArray();
        string? documentRef = null;
        if (iep != null)
        {
            documentRef = Register("iep", iep.Id, IepLabel(iep.IepDate, iep.UploadDate));
            var goals = await _context.Goals.AsNoTracking()
                .Where(g => g.IepSection.IepDocumentId == iep.Id)
                .OrderBy(g => g.IepSection.DisplayOrder).ThenBy(g => g.Id)
                .Select(g => new { g.Id, g.Domain, g.GoalText, g.Baseline, g.TargetCriteria, g.MeasurementMethod, g.Timeframe })
                .ToListAsync(ct);
            foreach (var g in goals)
            {
                goalArray.Add(new JsonObject
                {
                    ["sourceRef"] = Register("goal", g.Id, GoalLabel(g.Domain, g.GoalText), documentRef),
                    ["domain"] = Str(g.Domain, MaxLabelChars),
                    ["goalText"] = Str(g.GoalText, 800),
                    ["baseline"] = Str(g.Baseline, 400),
                    ["targetCriteria"] = Str(g.TargetCriteria, 400),
                    ["measurementMethod"] = Str(g.MeasurementMethod, 300),
                    ["timeframe"] = Str(g.Timeframe, 200)
                });
            }
        }

        // The school's own goal records (projected from finalized authored documents) carry the progress
        // observations. They live on the linked SchoolStudent, not on the uploaded IEP — same visibility rule
        // as GoalRecordService.GetForChildAsync.
        var linkedStudentIds = LinkedStudentIds();
        var records = await _context.GoalRecords.AsNoTracking()
            .Where(r => linkedStudentIds.Contains(r.SchoolStudentId)
                        && (r.Status == GoalRecordStatus.Active || r.Status == GoalRecordStatus.Met || r.Status == GoalRecordStatus.NotMet))
            .OrderBy(r => r.Domain).ThenBy(r => r.Id)
            .Take(MaxListItems)
            .Select(r => new
            {
                r.Id, r.Domain, r.GoalText, r.Baseline, r.TargetCriteria, r.MeasurementMethod, r.Timeframe, r.Status, r.StatusReason,
                Observations = r.Observations.OrderByDescending(o => o.ObservedAt).ThenByDescending(o => o.Id).Take(MaxObservationsPerGoal)
                    .Select(o => new { o.ObservedAt, o.Value, o.Unit, o.Note }).ToList()
            })
            .ToListAsync(ct);
        var recordArray = new JsonArray();
        foreach (var r in records)
        {
            var progress = new JsonArray();
            foreach (var o in r.Observations.OrderBy(o => o.ObservedAt))
            {
                progress.Add(new JsonObject
                {
                    ["observedAt"] = Date(o.ObservedAt),
                    ["value"] = o.Value,
                    ["unit"] = Str(o.Unit, MaxLabelChars),
                    ["note"] = Str(o.Note, 300)
                });
            }
            recordArray.Add(new JsonObject
            {
                ["sourceRef"] = Register("goal_record", r.Id, GoalLabel(r.Domain, r.GoalText)),
                ["domain"] = Str(r.Domain, MaxLabelChars),
                ["goalText"] = Str(r.GoalText, 800),
                ["baseline"] = Str(r.Baseline, 400),
                ["targetCriteria"] = Str(r.TargetCriteria, 400),
                ["measurementMethod"] = Str(r.MeasurementMethod, 300),
                ["timeframe"] = Str(r.Timeframe, 200),
                ["status"] = r.Status.ToString(),
                ["statusReason"] = Str(r.StatusReason, 300),
                ["progress"] = progress
            });
        }

        var payload = new JsonObject
        {
            ["iepRef"] = documentRef,
            ["status"] = iep == null ? "no_iep" : goalArray.Count == 0 ? "no_goals" : "ok",
            ["goals"] = goalArray,
            ["goalRecords"] = recordArray
        };
        return new ToolPayload(payload, goalArray, recordArray);
    }

    // ------------------------------------------------------------------ compare_iep_versions

    private static readonly ClaudeToolDefinition CompareIepVersionsDefinition = new(
        "compare_iep_versions",
        "Compare two of the child's uploaded IEPs: goals added, removed or changed, sections added or removed, " +
        "and which red flags were resolved, persist or are new. Use it when the parent asks what changed " +
        "between years or versions. Get both ids from list_documents.",
        ToolSchema.Object(
            ("iepIdA", "integer", "One IEP id.", true),
            ("iepIdB", "integer", "The other IEP id.", true)));

    private async Task<ToolPayload> CompareIepVersionsAsync(JsonElement input, CancellationToken ct)
    {
        var a = input.GetProperty("iepIdA").GetInt32();
        var b = input.GetProperty("iepIdB").GetInt32();
        if (a == b)
            throw new ToolExecutionException("iepIdA and iepIdB must be different IEPs.");

        var iepA = await RequireIepAsync(a, ct);
        var iepB = await RequireIepAsync(b, ct);

        var result = await _comparison.CompareAsync(a, b, _userId, ct);
        if (result == null)
            throw new ToolExecutionException(NotFoundMessage);

        var older = result.OlderIepId == iepA.Id ? iepA : iepB;
        var newer = result.NewerIepId == iepB.Id ? iepB : iepA;
        var olderRef = Register("iep", older.Id, IepLabel(older.IepDate, older.UploadDate));
        var newerRef = Register("iep", newer.Id, IepLabel(newer.IepDate, newer.UploadDate));
        var sourceRef = $"comparison:{result.OlderIepId}-{result.NewerIepId}";
        ReturnedRefs.Add(sourceRef);
        Labels[sourceRef] = $"{Labels[olderRef]} vs {Labels[newerRef]}";

        var added = new JsonArray();
        foreach (var g in result.GoalChanges.Added)
            added.Add(new JsonObject { ["domain"] = Str(g.Domain, MaxLabelChars), ["goalText"] = Str(g.GoalText, 400) });
        var removed = new JsonArray();
        foreach (var g in result.GoalChanges.Removed)
            removed.Add(new JsonObject { ["domain"] = Str(g.Domain, MaxLabelChars), ["goalText"] = Str(g.GoalText, 400) });
        var modified = new JsonArray();
        foreach (var g in result.GoalChanges.Modified)
        {
            var changes = new JsonArray();
            foreach (var c in g.Changes)
                changes.Add(new JsonObject { ["field"] = Str(c.Field, MaxLabelChars), ["older"] = Str(c.Older, 300), ["newer"] = Str(c.Newer, 300) });
            modified.Add(new JsonObject
            {
                ["domain"] = Str(g.Domain, MaxLabelChars),
                ["olderGoalText"] = Str(g.OlderGoalText, 400),
                ["newerGoalText"] = Str(g.NewerGoalText, 400),
                ["changes"] = changes
            });
        }

        var payload = new JsonObject
        {
            ["sourceRef"] = sourceRef,
            ["olderIepRef"] = olderRef,
            ["newerIepRef"] = newerRef,
            ["olderDate"] = Date(result.OlderDate),
            ["newerDate"] = Date(result.NewerDate),
            ["summary"] = new JsonObject
            {
                ["goalsAdded"] = result.Summary.GoalsAdded,
                ["goalsRemoved"] = result.Summary.GoalsRemoved,
                ["goalsModified"] = result.Summary.GoalsModified,
                ["goalsUnchanged"] = result.Summary.GoalsUnchanged,
                ["sectionsAdded"] = result.Summary.SectionsAdded,
                ["sectionsRemoved"] = result.Summary.SectionsRemoved,
                ["redFlagsResolved"] = result.Summary.RedFlagsResolved,
                ["redFlagsPersisting"] = result.Summary.RedFlagsPersisting,
                ["newRedFlags"] = result.Summary.NewRedFlags
            },
            ["goalChanges"] = new JsonObject { ["added"] = added, ["removed"] = removed, ["modified"] = modified },
            ["sectionChanges"] = new JsonObject
            {
                ["added"] = StringArray(result.SectionChanges.Added),
                ["removed"] = StringArray(result.SectionChanges.Removed),
                ["inBoth"] = StringArray(result.SectionChanges.InBoth)
            },
            ["redFlags"] = new JsonObject
            {
                ["resolved"] = RedFlagArray(result.RedFlagResolution.Resolved),
                ["persisting"] = RedFlagArray(result.RedFlagResolution.Persisting),
                ["new"] = RedFlagArray(result.RedFlagResolution.NewFlags)
            }
        };
        return new ToolPayload(payload, modified, added, removed);
    }

    private static JsonArray RedFlagArray(IEnumerable<RedFlagChange> flags)
    {
        var array = new JsonArray();
        foreach (var f in flags)
            array.Add(new JsonObject { ["title"] = Str(f.Title, 200), ["description"] = Str(f.Description, 300) });
        return array;
    }

    // ------------------------------------------------------------------ list_journal

    private static readonly ClaudeToolDefinition ListJournalDefinition = new(
        "list_journal",
        "Read the parent's dated journal about this child (incidents, communications with the school, medical " +
        "notes, progress they noticed), newest first, at most 30 entries. Use it when the parent refers to " +
        "something that happened, or asks what they should raise from recent weeks.",
        ToolSchema.Object(
            ("sinceDays", "integer", "Optional: only entries from the last N days (1–365).", false),
            ("tag", "string", "Optional tag filter: incident, communication, medical, progress or other.", false)));

    private async Task<ToolPayload> ListJournalAsync(JsonElement input, CancellationToken ct)
    {
        var query = _context.JournalEntries.AsNoTracking().Where(j => j.ChildProfileId == _childId);

        if (input.TryGetProperty("sinceDays", out var sinceProp) && sinceProp.ValueKind == JsonValueKind.Number)
        {
            var sinceDays = sinceProp.GetInt32();
            if (sinceDays is < 1 or > 365)
                throw new ToolExecutionException("sinceDays must be between 1 and 365.");
            var since = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-sinceDays);
            query = query.Where(j => j.OccurredOn >= since);
        }

        var tagText = OptionalString(input, "tag");
        JournalTag? tag = null;
        if (tagText != null)
        {
            if (!Enum.TryParse<JournalTag>(tagText, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ToolExecutionException("tag must be one of: incident, communication, medical, progress, other.");
            tag = parsed;
            query = query.Where(j => j.Tag == parsed);
        }

        var entries = await query
            .OrderByDescending(j => j.OccurredOn).ThenByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
            .Take(MaxJournalEntries)
            .Select(j => new { j.Id, j.OccurredOn, j.Tag, j.ContentMarkdown, j.LinkedIepDocumentId, j.LinkedEtrDocumentId, j.LinkedMeetingId })
            .ToListAsync(ct);

        var array = new JsonArray();
        foreach (var j in entries)
        {
            array.Add(new JsonObject
            {
                ["sourceRef"] = Register("journal", j.Id, $"Journal entry {j.OccurredOn:yyyy-MM-dd}"),
                ["occurredOn"] = j.OccurredOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["tag"] = TagName(j.Tag),
                ["text"] = PromptText.Data(PromptText.Truncate(j.ContentMarkdown, MaxJournalChars)),
                ["linkedIepId"] = j.LinkedIepDocumentId,
                ["linkedEtrId"] = j.LinkedEtrDocumentId,
                ["linkedMeetingId"] = j.LinkedMeetingId
            });
        }

        var payload = new JsonObject
        {
            ["tag"] = tag == null ? null : TagName(tag.Value),
            ["entries"] = array
        };
        return new ToolPayload(payload, array);
    }

    private static string TagName(JournalTag tag) => tag.ToString().ToLowerInvariant();

    // ------------------------------------------------------------------ list_contributions

    private static readonly ClaudeToolDefinition ListContributionsDefinition = new(
        "list_contributions",
        "Read what the parent has written about their child for the school: strengths, concerns, what works at " +
        "home, priorities. Use it to ground advice in what the parent already knows and has said.",
        ToolSchema.Object());

    private async Task<ToolPayload> ListContributionsAsync(JsonElement input, CancellationToken ct)
    {
        var rows = await _context.ParentContributions.AsNoTracking()
            .Where(c => c.ChildProfileId == _childId)
            .OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id)
            .Take(MaxListItems)
            .Select(c => new { c.Id, c.Kind, c.Text, c.IsShared, c.UpdatedAt })
            .ToListAsync(ct);

        var array = new JsonArray();
        foreach (var c in rows)
        {
            var kind = c.Kind.ToString();
            array.Add(new JsonObject
            {
                ["sourceRef"] = Register("contribution", c.Id, $"{kind}: {PromptText.Truncate(PromptText.OneLine(c.Text), 40)}"),
                ["kind"] = kind,
                ["text"] = PromptText.Data(PromptText.Truncate(c.Text, MaxContributionChars)),
                ["isShared"] = c.IsShared,
                ["updatedAt"] = Date(c.UpdatedAt)
            });
        }
        return new ToolPayload(new JsonObject { ["contributions"] = array }, array);
    }

    // ------------------------------------------------------------------ list_advocacy_goals

    private static readonly ClaudeToolDefinition ListAdvocacyGoalsDefinition = new(
        "list_advocacy_goals",
        "Read the parent's own advocacy goals for this child — what THEY want the plan to achieve (academic, " +
        "behavioral, services, placement). Use it to check whether the IEP addresses the parent's priorities.",
        ToolSchema.Object());

    private async Task<ToolPayload> ListAdvocacyGoalsAsync(JsonElement input, CancellationToken ct)
    {
        var rows = await _context.ParentAdvocacyGoals.AsNoTracking()
            .Where(g => g.ChildProfileId == _childId && g.IsActive)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Id)
            .Take(MaxListItems)
            .Select(g => new { g.Id, g.Category, g.GoalText })
            .ToListAsync(ct);

        var array = new JsonArray();
        foreach (var g in rows)
        {
            array.Add(new JsonObject
            {
                ["sourceRef"] = Register("advocacy_goal", g.Id, PromptText.Truncate(PromptText.OneLine(g.GoalText), MaxLabelChars)),
                ["category"] = Str(g.Category, MaxLabelChars),
                ["goalText"] = PromptText.Data(PromptText.Truncate(g.GoalText, 500))
            });
        }
        return new ToolPayload(new JsonObject { ["advocacyGoals"] = array }, array);
    }

    // ------------------------------------------------------------------ get_meeting_prep

    private static readonly ClaudeToolDefinition GetMeetingPrepDefinition = new(
        "get_meeting_prep",
        "Read the parent's latest meeting-prep checklist: questions to ask, documents to bring, red flags to " +
        "raise, rights to reference, goal gaps and notes — plus parentQuestions, the questions the parent has " +
        "already written down to ask themselves. Use it when the parent is preparing for a meeting or asks " +
        "what to bring up, and check parentQuestions before suggesting a prep_question.",
        ToolSchema.Object());

    private async Task<ToolPayload> GetMeetingPrepAsync(JsonElement input, CancellationToken ct)
    {
        var prep = await _context.MeetingPrepChecklists.AsNoTracking()
            .Where(p => p.ChildProfileId == _childId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id, p.Status, p.MeetingDate, p.IepDocumentId, p.EtrDocumentId, p.CreatedAt,
                p.QuestionsToAsk, p.DocumentsToBring, p.RedFlagsToRaise, p.RightsToReference, p.GoalGaps, p.GeneralTips, p.PreparationNotes
            })
            .FirstOrDefaultAsync(ct);

        // The parent's own questions live beside the AI checklist and are returned even when there is no
        // checklist — they are what the parent already plans to ask, so the model can avoid re-suggesting them.
        var parentRows = await _context.ParentPrepQuestions.AsNoTracking()
            .Where(q => q.ChildProfileId == _childId)
            .OrderBy(q => q.DisplayOrder).ThenBy(q => q.Id)
            .Take(MaxListItems)
            .Select(q => new { q.Id, q.Text, q.IsChecked })
            .ToListAsync(ct);
        var parentQuestions = new JsonArray();
        foreach (var q in parentRows)
        {
            parentQuestions.Add(new JsonObject
            {
                ["sourceRef"] = Register("prep_question", q.Id, PromptText.Truncate(PromptText.OneLine(q.Text), MaxLabelChars)),
                ["text"] = Str(q.Text, ParentPrepQuestionService.MaxTextLength),
                ["isChecked"] = q.IsChecked
            });
        }

        if (prep == null)
        {
            return new ToolPayload(new JsonObject
            {
                ["status"] = "none",
                ["hint"] = "The parent can generate a meeting-prep checklist from the Meeting Prep tab.",
                ["parentQuestions"] = parentQuestions
            }, parentQuestions);
        }

        var questions = ParseArray(prep.QuestionsToAsk);
        var documents = ParseArray(prep.DocumentsToBring);
        var redFlags = ParseArray(prep.RedFlagsToRaise);
        var rights = ParseArray(prep.RightsToReference);
        var gaps = ParseArray(prep.GoalGaps);
        var tips = ParseArray(prep.GeneralTips);
        var notes = ParseArray(prep.PreparationNotes);

        var payload = new JsonObject
        {
            ["sourceRef"] = Register("meeting_prep", prep.Id, $"Meeting prep {prep.CreatedAt:yyyy-MM-dd}"),
            ["status"] = Str(prep.Status, MaxLabelChars),
            ["meetingDate"] = Date(prep.MeetingDate),
            ["iepId"] = prep.IepDocumentId,
            ["etrId"] = prep.EtrDocumentId,
            ["questionsToAsk"] = questions,
            ["documentsToBring"] = documents,
            ["redFlagsToRaise"] = redFlags,
            ["rightsToReference"] = rights,
            ["goalGaps"] = gaps,
            ["generalTips"] = tips,
            ["preparationNotes"] = notes,
            ["parentQuestions"] = parentQuestions
        };
        return new ToolPayload(payload, questions, documents, redFlags, rights, gaps, tips, notes, parentQuestions);
    }

    // ------------------------------------------------------------------ list_meetings_and_deadlines

    private static readonly ClaudeToolDefinition ListMeetingsAndDeadlinesDefinition = new(
        "list_meetings_and_deadlines",
        "List the IEP-team meetings the school has scheduled or held for this child (type, status, date, " +
        "location), upcoming first. Use it for any question about when the next meeting is or what has " +
        "happened recently. Procedural deadlines are only available through the knowledge base and the " +
        "document dates.",
        ToolSchema.Object());

    private async Task<ToolPayload> ListMeetingsAndDeadlinesAsync(JsonElement input, CancellationToken ct)
    {
        // Same rule as MeetingService.ListForChildAsync: meetings of any SchoolStudent linked to this child via
        // an active, accepted ChildLink. Staff notes are never included.
        var linkedStudentIds = LinkedStudentIds();
        var now = DateTime.UtcNow;
        var meetings = await _context.Meetings.AsNoTracking()
            .Where(m => linkedStudentIds.Contains(m.SchoolStudentId))
            .OrderByDescending(m => m.StartsAtUtc).ThenByDescending(m => m.Id)
            .Take(MaxListItems)
            .Select(m => new { m.Id, m.Type, m.Title, m.Status, m.StartsAtUtc, m.DurationMinutes, m.Location, m.TimeZoneId })
            .ToListAsync(ct);

        var array = new JsonArray();
        var ordered = meetings.Where(m => m.StartsAtUtc >= now).OrderBy(m => m.StartsAtUtc)
            .Concat(meetings.Where(m => m.StartsAtUtc < now).OrderByDescending(m => m.StartsAtUtc));
        foreach (var m in ordered)
        {
            array.Add(new JsonObject
            {
                ["sourceRef"] = Register("meeting", m.Id, $"{m.Type.ToDisplay()} meeting {m.StartsAtUtc:yyyy-MM-dd}"),
                ["type"] = m.Type.ToString(),
                ["title"] = Str(m.Title, 120),
                ["status"] = m.Status.ToString(),
                ["scheduledOn"] = Date(m.StartsAtUtc),
                ["startsAtUtc"] = m.StartsAtUtc.ToString("yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture),
                ["timeZone"] = Str(m.TimeZoneId, MaxLabelChars),
                ["durationMinutes"] = m.DurationMinutes,
                ["location"] = Str(m.Location, 120),
                ["isUpcoming"] = m.StartsAtUtc >= now
            });
        }

        var payload = new JsonObject
        {
            ["today"] = Date(now),
            ["meetings"] = array,
            ["deadlines"] = new JsonArray(),
            ["deadlinesNote"] = "The school's procedural deadline tracker is not shared with families. Work out timelines from the document dates and the knowledge base rules."
        };
        return new ToolPayload(payload, array);
    }

    // ------------------------------------------------------------------ get_shared_draft

    private static readonly ClaudeToolDefinition GetSharedDraftDefinition = new(
        "get_shared_draft",
        "Read a draft document the school shared with the family (an IEP or ETR in progress), field by field. " +
        "Use it when the parent asks about a draft they were sent. Get the revisionId from list_documents.",
        ToolSchema.Object(
            ("revisionId", "integer", "The shared draft revision id from list_documents.", true)));

    private async Task<ToolPayload> GetSharedDraftAsync(JsonElement input, CancellationToken ct)
    {
        var revisionId = input.GetProperty("revisionId").GetInt32();
        var header = await _context.SharedDraftRevisions.AsNoTracking()
            .Where(r => r.Id == revisionId)
            .Select(r => new
            {
                r.Id, r.RevisionNumber, r.Status, r.SharedAt, r.WithdrawnAt, r.Message, r.ValuesJson, r.DocumentTemplateVersionId,
                r.DocumentInstance.SchoolStudentId,
                DocumentType = r.DocumentInstance.DocumentType.DisplayName
            })
            .FirstOrDefaultAsync(ct);
        if (header == null)
            throw new ToolExecutionException(NotFoundMessage);

        // The parent endpoint's rule (DraftSharingService.GetForParentAsync) AND the revision must resolve to
        // THIS child — a revision the caller may read through another of their children is still "not found"
        // here, because this toolset only ever speaks about one child.
        var resolvedChildId = await ParentAccessResolver.ResolveChildIdAsync(_context, _access, _userId, header.SchoolStudentId, AccessRole.Viewer, ct);
        if (resolvedChildId != _childId)
            throw new ToolExecutionException(NotFoundMessage);

        var sections = await TemplateSectionLoader.LoadAsync(_context, header.DocumentTemplateVersionId, ct);
        var rendered = DraftPromptBuilder.RenderDraft(sections, ValueDocumentJson.Parse(header.ValuesJson), SharedDraftCharBudget);

        var payload = new JsonObject
        {
            ["sourceRef"] = Register("shared_draft", header.Id, $"Shared {header.DocumentType} draft, revision {header.RevisionNumber}"),
            ["documentType"] = Str(header.DocumentType, MaxLabelChars),
            ["revisionNumber"] = header.RevisionNumber,
            ["status"] = header.Status.ToString(),
            ["withdrawn"] = header.Status == SharedDraftStatus.Withdrawn,
            ["sharedAt"] = Date(header.SharedAt),
            ["withdrawnAt"] = Date(header.WithdrawnAt),
            ["messageFromSchool"] = Str(header.Message, 400),
            // Already rendered as a <draft> data block with every value entity-escaped (DraftPromptBuilder).
            ["draft"] = rendered.Text
        };
        return new ToolPayload(payload);
    }

    // ------------------------------------------------------------------ ownership checks

    private sealed record IepHeader(int Id, DateTime? IepDate, DateTime UploadDate);
    private sealed record EtrHeader(int Id, DateTime? EvaluationDate, DateTime UploadDate);

    private Task<IepHeader?> FindIepAsync(int id, CancellationToken ct) =>
        _context.IepDocuments.AsNoTracking()
            .Where(d => d.Id == id && d.ChildProfileId == _childId && d.IsActive)
            .Select(d => new IepHeader(d.Id, d.IepDate, d.UploadDate))
            .FirstOrDefaultAsync(ct);

    private async Task<IepHeader> RequireIepAsync(int id, CancellationToken ct) =>
        await FindIepAsync(id, ct) ?? throw new ToolExecutionException(NotFoundMessage);

    private async Task<EtrHeader> RequireEtrAsync(int id, CancellationToken ct) =>
        await _context.EtrDocuments.AsNoTracking()
            .Where(d => d.Id == id && d.ChildProfileId == _childId && d.IsActive)
            .Select(d => new EtrHeader(d.Id, d.EvaluationDate, d.UploadDate))
            .FirstOrDefaultAsync(ct)
        ?? throw new ToolExecutionException(NotFoundMessage);

    /// <summary>SchoolStudents this child is linked to through an active, accepted ChildLink — the one parent-visibility rule for school-side records.</summary>
    private IQueryable<int> LinkedStudentIds() =>
        _context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == _childId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId);

    // ------------------------------------------------------------------ helpers

    /// <param name="parentRef">The sourceRef (<c>kind:id</c>) of the record this one should be opened inside, when it has no page of its own.</param>
    private string Register(string kind, int id, string label, string? parentRef = null)
    {
        var sourceRef = $"{kind}:{id}";
        ReturnedRefs.Add(sourceRef);
        Labels[sourceRef] = label;
        if (parentRef != null)
        {
            var separator = parentRef.IndexOf(':');
            Parents[sourceRef] = new AdvocateCitationParent(parentRef[..separator], int.Parse(parentRef[(separator + 1)..], CultureInfo.InvariantCulture));
        }
        return sourceRef;
    }

    private static string IepRef(int iepId) => $"iep:{iepId}";

    private string StateValue() => _stateCode == null ? "unknown" : PromptText.Data(_stateCode);

    private static string? Str(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : PromptText.Data(PromptText.Truncate(value, max));

    private static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool IsCompleted(string? status) => string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);

    private static string IepLabel(DateTime? iepDate, DateTime uploadDate) => $"IEP {(iepDate ?? uploadDate):yyyy-MM-dd}";
    private static string EtrLabel(DateTime? evaluationDate, DateTime uploadDate) => $"ETR {(evaluationDate ?? uploadDate):yyyy-MM-dd}";
    private static string ProgressReportLabel(DateTime? periodEnd, DateTime uploadDate) => $"Progress report {(periodEnd ?? uploadDate):yyyy-MM-dd}";
    private static string GoalLabel(string? domain, string goalText) =>
        string.IsNullOrWhiteSpace(domain) ? PromptText.Truncate(PromptText.OneLine(goalText), MaxLabelChars) : $"{domain.Trim()} goal";
    private static string SectionLabel(string sectionType, string documentLabel) =>
        $"{documentLabel} — {sectionType.Replace('_', ' ')}";

    private static string? OptionalString(JsonElement input, string name)
    {
        if (!input.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()!.Trim();
        return text.Length == 0 ? null : text;
    }

    private static string RequireEnum(JsonElement input, string name, params string[] allowed)
    {
        var value = input.GetProperty(name).GetString()!.Trim().ToLowerInvariant();
        if (!allowed.Contains(value, StringComparer.Ordinal))
            throw new ToolExecutionException($"{name} must be one of: {string.Join(", ", allowed)}.");
        return value;
    }

    private static JsonArray StringArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var v in values) array.Add(Str(v, 200));
        return array;
    }

    /// <summary>A stored JSON column → a sanitised array (every string entity-escaped and bounded); non-array or unparsable → empty.</summary>
    private static JsonArray ParseArray(string? json)
    {
        var node = ParseOrText(json);
        return node as JsonArray ?? new JsonArray();
    }

    /// <summary>
    /// A stored JSON column → a sanitised copy with every string value entity-escaped and truncated to
    /// <see cref="MaxAnalysisStringChars"/>. A column that is not JSON is returned as one escaped text value; null stays null.
    /// </summary>
    private static JsonNode? ParseOrText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(raw);
        }
        catch (JsonException)
        {
            return JsonValue.Create(PromptText.Data(PromptText.Truncate(raw, MaxAnalysisStringChars)));
        }
        return Sanitize(parsed);
    }

    private static JsonNode? Sanitize(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonObject obj:
                var copy = new JsonObject();
                foreach (var (key, value) in obj)
                    copy[PromptText.Data(key)] = Sanitize(value);
                return copy;
            case JsonArray array:
                var list = new JsonArray();
                foreach (var item in array) list.Add(Sanitize(item));
                return list;
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonValue.Create(PromptText.Data(PromptText.Truncate(text, MaxAnalysisStringChars)));
            default:
                return node.DeepClone();
        }
    }

    /// <summary>
    /// Serialises the payload under <see cref="PerToolCharCap"/>: drops trailing items from the trimmable
    /// arrays (longest first, flagging <c>truncated: "size"</c>), then shortens the longest top-level string
    /// values, and only if that is not enough hard-cuts the JSON.
    /// </summary>
    private static string FitToCap(ToolPayload result)
    {
        var json = result.Payload.ToJsonString();
        if (json.Length <= PerToolCharCap) return json;

        result.Payload["truncated"] = true;
        result.Payload["reason"] = "size";
        json = result.Payload.ToJsonString();

        while (json.Length > PerToolCharCap)
        {
            var longest = result.Trimmable.Where(a => a.Count > 0).OrderByDescending(a => a.Count).FirstOrDefault();
            if (longest == null) break;
            longest.RemoveAt(longest.Count - 1);
            json = result.Payload.ToJsonString();
        }
        if (json.Length <= PerToolCharCap) return json;

        // Long scalar text (a section, a rendered draft) — halve the longest string until it fits.
        while (json.Length > PerToolCharCap)
        {
            var longestKey = result.Payload
                .Where(kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 200)
                .OrderByDescending(kv => kv.Value!.GetValue<string>().Length)
                .Select(kv => kv.Key)
                .FirstOrDefault();
            if (longestKey == null) break;
            var text = result.Payload[longestKey]!.GetValue<string>();
            result.Payload[longestKey] = text[..(text.Length / 2)] + "…";
            json = result.Payload.ToJsonString();
        }
        if (json.Length <= PerToolCharCap) return json;

        return json[..PerToolCharCap];
    }
}

/// <summary>Builds the strict JSON-Schema objects the advocate tools declare (<c>additionalProperties: false</c>, explicit <c>required</c>).</summary>
public static class ToolSchema
{
    public static JsonNode Object(params (string Name, string Type, string Description, bool Required)[] properties)
    {
        var props = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, type, description, isRequired) in properties)
        {
            props[name] = new JsonObject { ["type"] = type, ["description"] = description };
            if (isRequired) required.Add(name);
        }
        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = required,
            ["additionalProperties"] = false
        };
    }
}

/// <summary>
/// Validates model-supplied tool input against the tool's declared schema (object shape, required keys,
/// scalar types, no unknown keys, bounded string length). Throws <see cref="ToolExecutionException"/> so
/// the model sees an <c>is_error</c> result and can retry with corrected input.
/// </summary>
public static class ToolInputValidator
{
    public static void Validate(JsonElement input, JsonNode schema, int maxStringLength)
    {
        if (input.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            input = JsonDocument.Parse("{}").RootElement;
        if (input.ValueKind != JsonValueKind.Object)
            throw new ToolExecutionException("Tool input must be a JSON object.");

        var properties = schema["properties"] as JsonObject ?? new JsonObject();
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in schema["required"] as JsonArray ?? new JsonArray())
        {
            if (node?.GetValue<string>() is { } name) required.Add(name);
        }

        foreach (var name in required)
        {
            if (!input.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                throw new ToolExecutionException($"Missing required input '{name}'.");
        }

        foreach (var member in input.EnumerateObject())
        {
            if (!properties.TryGetPropertyValue(member.Name, out var propSchema) || propSchema == null)
                throw new ToolExecutionException($"Unknown input '{PromptText.Data(PromptText.Truncate(member.Name, 60))}'.");
            if (member.Value.ValueKind == JsonValueKind.Null) continue;

            var expected = propSchema["type"]?.GetValue<string>();
            var ok = expected switch
            {
                "string" => member.Value.ValueKind == JsonValueKind.String,
                "integer" => member.Value.ValueKind == JsonValueKind.Number && member.Value.TryGetInt32(out _),
                "number" => member.Value.ValueKind == JsonValueKind.Number,
                "boolean" => member.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => true
            };
            if (!ok)
                throw new ToolExecutionException($"Input '{member.Name}' must be a {expected}.");
            if (member.Value.ValueKind == JsonValueKind.String && member.Value.GetString()!.Length > maxStringLength)
                throw new ToolExecutionException($"Input '{member.Name}' must be {maxStringLength} characters or fewer.");
        }
    }
}
