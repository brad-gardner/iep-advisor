using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Migrates legacy <see cref="IepAnalysis"/> / <see cref="EtrAnalysis"/> rows into single-source
/// <see cref="AnalysisRun"/> rows. Processes in batches (resumable: a crash mid-run leaves already
/// committed batches intact, and re-running skips them via the unique <c>BackfillSourceKey</c>).
/// </summary>
public class AnalysisRunBackfillService : IAnalysisRunBackfillService
{
    private const int BatchSize = 200;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalysisRunBackfillService> _logger;

    public AnalysisRunBackfillService(
        ApplicationDbContext context,
        ILogger<AnalysisRunBackfillService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BackfillResult> BackfillAsync(CancellationToken ct = default)
    {
        var iep = await BackfillIepAsync(ct);
        var etr = await BackfillEtrAsync(ct);

        var result = new BackfillResult(
            iep.Created + etr.Created,
            iep.SkippedExisting + etr.SkippedExisting,
            iep.SkippedOrphan + etr.SkippedOrphan);

        _logger.LogInformation(
            "AnalysisRun backfill complete: Created={Created}, SkippedExisting={SkippedExisting}, SkippedOrphan={SkippedOrphan}",
            result.Created, result.SkippedExisting, result.SkippedOrphan);

        return result;
    }

    // A run plus the loose section definitions whose AnalysisRunSourceId can only be set after the
    // run + its single source are saved (the source Id is database-generated). LegacyCreatedAt is
    // restored after the first save because the auditing SaveChanges override stamps CreatedAt to now.
    private sealed record PendingRun(
        AnalysisRun Run,
        AnalysisRunSource Source,
        List<AnalysisRunSection> Sections,
        DateTime LegacyCreatedAt);

    private async Task<BackfillResult> BackfillIepAsync(CancellationToken ct)
    {
        int created = 0, skippedExisting = 0, skippedOrphan = 0;
        var lastId = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await _context.IepAnalyses
                .AsNoTracking()
                .Where(a => a.Id > lastId)
                .OrderBy(a => a.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            var pending = new List<PendingRun>();

            foreach (var legacy in batch)
            {
                lastId = legacy.Id;
                var key = $"IepAnalysis:{legacy.Id}";

                if (await _context.AnalysisRuns.AnyAsync(r => r.BackfillSourceKey == key, ct))
                {
                    skippedExisting++;
                    continue;
                }

                var doc = await _context.IepDocuments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == legacy.IepDocumentId, ct);

                if (doc is null)
                {
                    _logger.LogWarning(
                        "Skipping orphaned IepAnalysis {Id}: IepDocument {DocId} not found",
                        legacy.Id, legacy.IepDocumentId);
                    skippedOrphan++;
                    continue;
                }

                var (status, errorMessage) = MapStatus(legacy.Status, legacy.ErrorMessage);

                var run = new AnalysisRun
                {
                    ChildProfileId = doc.ChildProfileId,
                    Status = status,
                    OverallSummary = legacy.OverallSummary,
                    OverallRedFlags = legacy.OverallRedFlags,
                    AdvocacyGapAnalysis = legacy.AdvocacyGapAnalysis,
                    ParentGoalsSnapshot = legacy.ParentGoalsSnapshot,
                    CrossDocSynthesis = null,
                    BackfillSourceKey = key,
                    ErrorMessage = errorMessage,
                    CreatedAt = legacy.CreatedAt
                };

                var source = new AnalysisRunSource
                {
                    SourceType = AnalysisSourceType.IepDocument,
                    SourceId = legacy.IepDocumentId,
                    SourceLabel = $"IEP — {doc.MeetingType} {doc.IepDate:yyyy-MM-dd}",
                    SourceContentSnapshot = null
                };
                run.Sources.Add(source);

                var sections = BuildSectionsFromJsonArray(legacy.SectionAnalyses, legacy.Id, "IepAnalysis");

                if (!string.IsNullOrWhiteSpace(legacy.GoalAnalyses))
                {
                    sections.Add(new AnalysisRunSection
                    {
                        SectionKind = "annual_goals",
                        Analysis = legacy.GoalAnalyses,
                        DisplayOrder = sections.Count
                    });
                }

                _context.AnalysisRuns.Add(run);
                pending.Add(new PendingRun(run, source, sections, legacy.CreatedAt));
                created++;
            }

            await PersistBatchAsync(pending, lastId, created, skippedExisting, skippedOrphan, "IepAnalysis", ct);

            if (batch.Count < BatchSize)
                break;
        }

        return new BackfillResult(created, skippedExisting, skippedOrphan);
    }

    private async Task<BackfillResult> BackfillEtrAsync(CancellationToken ct)
    {
        int created = 0, skippedExisting = 0, skippedOrphan = 0;
        var lastId = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var batch = await _context.EtrAnalyses
                .AsNoTracking()
                .Where(a => a.Id > lastId)
                .OrderBy(a => a.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            var pending = new List<PendingRun>();

            foreach (var legacy in batch)
            {
                lastId = legacy.Id;
                var key = $"EtrAnalysis:{legacy.Id}";

                if (await _context.AnalysisRuns.AnyAsync(r => r.BackfillSourceKey == key, ct))
                {
                    skippedExisting++;
                    continue;
                }

                var doc = await _context.EtrDocuments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == legacy.EtrDocumentId, ct);

                if (doc is null)
                {
                    _logger.LogWarning(
                        "Skipping orphaned EtrAnalysis {Id}: EtrDocument {DocId} not found",
                        legacy.Id, legacy.EtrDocumentId);
                    skippedOrphan++;
                    continue;
                }

                var (status, errorMessage) = MapStatus(legacy.Status, legacy.ErrorMessage);

                var run = new AnalysisRun
                {
                    ChildProfileId = doc.ChildProfileId,
                    Status = status,
                    OverallSummary = legacy.OverallSummary,
                    OverallRedFlags = legacy.OverallRedFlags,
                    AdvocacyGapAnalysis = legacy.AdvocacyGapAnalysis,
                    ParentGoalsSnapshot = legacy.ParentGoalsSnapshot,
                    CrossDocSynthesis = null,
                    BackfillSourceKey = key,
                    ErrorMessage = errorMessage,
                    CreatedAt = legacy.CreatedAt
                };

                var source = new AnalysisRunSource
                {
                    SourceType = AnalysisSourceType.EtrDocument,
                    SourceId = legacy.EtrDocumentId,
                    SourceLabel = $"ETR — {doc.EvaluationType} {doc.EvaluationDate:yyyy-MM-dd}",
                    SourceContentSnapshot = null
                };
                run.Sources.Add(source);

                var sections = new List<AnalysisRunSection>();
                if (!string.IsNullOrWhiteSpace(legacy.AssessmentCompleteness))
                {
                    sections.Add(new AnalysisRunSection
                    {
                        SectionKind = "assessment_completeness",
                        Analysis = legacy.AssessmentCompleteness,
                        DisplayOrder = sections.Count
                    });
                }
                if (!string.IsNullOrWhiteSpace(legacy.EligibilityReview))
                {
                    sections.Add(new AnalysisRunSection
                    {
                        SectionKind = "eligibility",
                        Analysis = legacy.EligibilityReview,
                        DisplayOrder = sections.Count
                    });
                }

                _context.AnalysisRuns.Add(run);
                pending.Add(new PendingRun(run, source, sections, legacy.CreatedAt));
                created++;
            }

            await PersistBatchAsync(pending, lastId, created, skippedExisting, skippedOrphan, "EtrAnalysis", ct);

            if (batch.Count < BatchSize)
                break;
        }

        return new BackfillResult(created, skippedExisting, skippedOrphan);
    }

    /// <summary>
    /// Saves the runs (+ their single source) to obtain database-generated keys, then attaches the
    /// loose sections (which carry an int? AnalysisRunSourceId, not a navigation) and saves again.
    /// Clears the change tracker so each batch stays bounded in memory.
    /// </summary>
    private async Task PersistBatchAsync(
        List<PendingRun> pending,
        int lastId,
        int created,
        int skippedExisting,
        int skippedOrphan,
        string legacyType,
        CancellationToken ct)
    {
        if (pending.Count > 0)
        {
            // First save: AnalysisRun + AnalysisRunSource get their generated Ids. The auditing
            // SaveChanges override stamps CreatedAt to now; we restore the legacy value below.
            await _context.SaveChangesAsync(ct);

            foreach (var p in pending)
            {
                // Restore the legacy CreatedAt (overwritten by the auditing interceptor on insert).
                p.Run.CreatedAt = p.LegacyCreatedAt;

                foreach (var section in p.Sections)
                {
                    section.AnalysisRunId = p.Run.Id;
                    section.AnalysisRunSourceId = p.Source.Id;
                    _context.AnalysisRunSections.Add(section);
                }
            }

            // Second save: sections linked to the now-persisted run + source, plus the restored
            // CreatedAt (the run is now Modified, so the override leaves CreatedAt untouched).
            await _context.SaveChangesAsync(ct);
        }

        _context.ChangeTracker.Clear();

        _logger.LogInformation(
            "{LegacyType} backfill progress: lastId={LastId}, created={Created}, skippedExisting={SkippedExisting}, skippedOrphan={SkippedOrphan}",
            legacyType, lastId, created, skippedExisting, skippedOrphan);
    }

    /// <summary>
    /// Maps a legacy status string onto an <see cref="AnalysisRunStatus"/>. Non-terminal legacy
    /// states (pending/analyzing) are treated as Error because no usable output exists. The legacy
    /// ErrorMessage is preserved only when the legacy status was itself "error".
    /// </summary>
    private static (AnalysisRunStatus Status, string? ErrorMessage) MapStatus(string? legacyStatus, string? legacyError)
    {
        return legacyStatus switch
        {
            "completed" => (AnalysisRunStatus.Completed, null),
            "error" => (AnalysisRunStatus.Error, legacyError),
            _ => (AnalysisRunStatus.Error, "Legacy analysis was not completed.")
        };
    }

    /// <summary>
    /// Best-effort: deserializes a legacy JSON array of section objects into <see cref="AnalysisRunSection"/>
    /// rows. Malformed JSON is logged and skipped so it never aborts the whole backfill.
    /// </summary>
    private List<AnalysisRunSection> BuildSectionsFromJsonArray(string? sectionsJson, int legacyId, string legacyType)
    {
        var sections = new List<AnalysisRunSection>();

        if (string.IsNullOrWhiteSpace(sectionsJson))
            return sections;

        try
        {
            var elements = JsonSerializer.Deserialize<JsonElement[]>(sectionsJson);
            if (elements is null)
                return sections;

            for (var i = 0; i < elements.Length; i++)
            {
                var element = elements[i];
                var sectionKind = element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty("sectionType", out var st)
                        ? st.GetString() ?? "other"
                        : "other";

                sections.Add(new AnalysisRunSection
                {
                    SectionKind = sectionKind,
                    Analysis = element.GetRawText(),
                    DisplayOrder = i
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse SectionAnalyses for {LegacyType} {LegacyId}; sections skipped",
                legacyType, legacyId);
            return new List<AnalysisRunSection>();
        }

        return sections;
    }
}
