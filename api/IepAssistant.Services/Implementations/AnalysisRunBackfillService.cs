using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Migrates legacy <see cref="IepAnalysis"/> / <see cref="EtrAnalysis"/> rows into single-source
/// <see cref="AnalysisRun"/> rows. Processes in batches (resumable: a crash mid-run leaves already
/// committed batches intact, and re-running skips them via the unique <c>BackfillSourceKey</c>).
/// Both legacy row types are upserted the same way: a legacy row already backfilled is left alone
/// unless it changed since (its <c>UpdatedAt</c> moved on) or its run still holds a pre-conversion
/// shape (IEP: the <c>annual_goals</c> array; ETR: the snake_case <c>assessment_completeness</c> /
/// <c>eligibility</c> sections), in which case the run's sources and sections are rebuilt in place —
/// same run id — from the legacy row's current data.
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
            iep.SkippedOrphan + etr.SkippedOrphan,
            iep.Updated + etr.Updated);

        _logger.LogInformation(
            "AnalysisRun backfill complete: Created={Created}, Updated={Updated}, SkippedExisting={SkippedExisting}, SkippedOrphan={SkippedOrphan}",
            result.Created, result.Updated, result.SkippedExisting, result.SkippedOrphan);

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
        int created = 0, skippedExisting = 0, skippedOrphan = 0, updated = 0;
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

                var existingRun = await _context.AnalysisRuns
                    .Include(r => r.Sources)
                    .Include(r => r.Sections)
                    .FirstOrDefaultAsync(r => r.BackfillSourceKey == key, ct);

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

                if (existingRun != null)
                {
                    if (!NeedsIepRebuild(legacy, existingRun))
                    {
                        skippedExisting++;
                        continue;
                    }

                    await RebuildIepRunAsync(existingRun, legacy, doc, ct);
                    updated++;
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
                    SourceContentSnapshot = null,
                    // Phase-1 P3: a freshly backfilled source must not be left at its default Pending —
                    // the migration's one-time data step only fixed rows that existed AT migration time
                    // (AddAnalysisRunSourceStatus), not rows this hosted service inserts on later boots.
                    Status = status == AnalysisRunStatus.Completed ? AnalysisRunSourceStatus.Completed : AnalysisRunSourceStatus.Error
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

        return new BackfillResult(created, skippedExisting, skippedOrphan, updated);
    }

    /// <summary>A previously backfilled run needs rebuilding when the legacy row changed since (its
    /// <c>UpdatedAt</c> moved past the run's own last-touched timestamp), or the run still holds the
    /// pre-conversion <c>annual_goals</c> ARRAY shape that <c>AnalysisRunSectionResult</c> cannot read.</summary>
    private static bool NeedsIepRebuild(IepAnalysis legacy, AnalysisRun run) =>
        legacy.UpdatedAt > run.UpdatedAt || HasLegacyGoalArrayShape(run);

    private static bool HasLegacyGoalArrayShape(AnalysisRun run) =>
        run.Sections.Any(s => s.SectionKind == "annual_goals" && IsJsonArray(s.Analysis));

    private static bool IsJsonArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Rebuilds one already-backfilled IEP run's sources and sections from the legacy row's CURRENT
    /// data, in place: same run id, old sources/sections removed and replaced in one transaction. Unlike
    /// the initial create path, the goal ratings are converted to the <c>iep_goals</c> OBJECT shape here
    /// (the create path is left emitting the old <c>annual_goals</c> array shape — see the type doc
    /// comment — so a freshly created run converges to the new shape on the NEXT backfill pass, the same
    /// one this method already handles).
    /// </summary>
    private async Task RebuildIepRunAsync(AnalysisRun run, IepAnalysis legacy, IepDocument doc, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        _context.AnalysisRunSections.RemoveRange(run.Sections);
        _context.AnalysisRunSources.RemoveRange(run.Sources);

        var (status, errorMessage) = MapStatus(legacy.Status, legacy.ErrorMessage);
        var sourceStatus = status == AnalysisRunStatus.Completed ? AnalysisRunSourceStatus.Completed : AnalysisRunSourceStatus.Error;

        run.Status = status;
        run.ErrorMessage = errorMessage;
        run.OverallSummary = legacy.OverallSummary;
        run.OverallRedFlags = legacy.OverallRedFlags;
        run.AdvocacyGapAnalysis = legacy.AdvocacyGapAnalysis;
        run.ParentGoalsSnapshot = legacy.ParentGoalsSnapshot;

        var newSource = new AnalysisRunSource
        {
            AnalysisRunId = run.Id,
            SourceType = AnalysisSourceType.IepDocument,
            SourceId = legacy.IepDocumentId,
            SourceLabel = $"IEP — {doc.MeetingType} {doc.IepDate:yyyy-MM-dd}",
            SourceContentSnapshot = null,
            Status = sourceStatus
        };
        _context.AnalysisRunSources.Add(newSource);

        // First save: the removals commit and newSource gets its generated Id, which the sections below
        // need as their AnalysisRunSourceId (a plain FK-by-value column, not a navigation EF can fix up).
        await _context.SaveChangesAsync(ct);

        var newSections = BuildSectionsFromJsonArray(legacy.SectionAnalyses, legacy.Id, "IepAnalysis");

        if (!string.IsNullOrWhiteSpace(legacy.GoalAnalyses))
        {
            var goalsJson = BuildIepGoalsSectionJson(legacy.GoalAnalyses, legacy.Id);
            if (goalsJson != null)
            {
                newSections.Add(new AnalysisRunSection
                {
                    SectionKind = AnalysisRunSectionKinds.IepGoals,
                    Analysis = goalsJson,
                    DisplayOrder = newSections.Count
                });
            }
        }

        foreach (var section in newSections)
        {
            section.AnalysisRunId = run.Id;
            section.AnalysisRunSourceId = newSource.Id;
            _context.AnalysisRunSections.Add(section);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _context.ChangeTracker.Clear();
    }

    /// <summary>Converts a legacy <c>GoalAnalyses</c> JSON ARRAY into the <c>iep_goals</c> section's OBJECT
    /// shape (<c>{ "goalAnalyses": [...] }</c>), round-tripping through the real <see cref="GoalAnalysisResult"/>
    /// shape so the stored JSON matches exactly what the live engine itself produces. Malformed legacy JSON
    /// is logged and skipped (the run is still rebuilt; it just gets no <c>iep_goals</c> section), matching
    /// <see cref="BuildSectionsFromJsonArray"/>'s own best-effort handling.</summary>
    private string? BuildIepGoalsSectionJson(string goalAnalysesJson, int legacyId)
    {
        try
        {
            var goals = JsonSerializer.Deserialize<List<GoalAnalysisResult>>(goalAnalysesJson, CaseInsensitiveJsonOptions);
            if (goals is null)
                return null;
            return JsonSerializer.Serialize(new IepGoalsSectionPayload { GoalAnalyses = goals }, CamelCaseJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse GoalAnalyses for IepAnalysis {LegacyId}; iep_goals section skipped", legacyId);
            return null;
        }
    }

    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions CamelCaseJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private async Task<BackfillResult> BackfillEtrAsync(CancellationToken ct)
    {
        int created = 0, skippedExisting = 0, skippedOrphan = 0, updated = 0;
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

                var existingRun = await _context.AnalysisRuns
                    .Include(r => r.Sources)
                    .Include(r => r.Sections)
                    .FirstOrDefaultAsync(r => r.BackfillSourceKey == key, ct);

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

                if (existingRun != null)
                {
                    if (!NeedsEtrRebuild(legacy, existingRun))
                    {
                        skippedExisting++;
                        continue;
                    }

                    await RebuildEtrRunAsync(existingRun, legacy, doc, ct);
                    updated++;
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
                    SourceContentSnapshot = null,
                    // Phase-1 P3 (see the IEP path above): do not leave a freshly backfilled source at
                    // its default Pending.
                    Status = status == AnalysisRunStatus.Completed ? AnalysisRunSourceStatus.Completed : AnalysisRunSourceStatus.Error
                };
                run.Sources.Add(source);

                // The initial create path deliberately keeps the OLD snake_case section kinds and the
                // legacy (high/medium/low) red-flag shape untouched — exactly like the IEP create
                // path keeps the old annual_goals array shape (see RebuildIepRunAsync's doc comment).
                // NeedsEtrRebuild below recognizes these old section kind names, so a freshly created
                // run converges to the new etr_completeness / etr_eligibility shape and normalized red
                // flags on the NEXT backfill pass — the same one this method already handles.
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

        return new BackfillResult(created, skippedExisting, skippedOrphan, updated);
    }

    /// <summary>A previously backfilled ETR run needs rebuilding when the legacy row changed since (its
    /// <c>UpdatedAt</c> moved past the run's own last-touched timestamp), or the run still holds the
    /// pre-conversion snake_case <c>assessment_completeness</c> / <c>eligibility</c> section kinds.</summary>
    private static bool NeedsEtrRebuild(EtrAnalysis legacy, AnalysisRun run) =>
        legacy.UpdatedAt > run.UpdatedAt || HasLegacyEtrSectionShape(run);

    private static bool HasLegacyEtrSectionShape(AnalysisRun run) =>
        run.Sections.Any(s => s.SectionKind is "assessment_completeness" or "eligibility");

    /// <summary>
    /// Rebuilds one already-backfilled ETR run's sources and sections from the legacy row's CURRENT
    /// data, in place: same run id, old sources/sections removed and replaced in one transaction.
    /// Converts the legacy snake_case <c>AssessmentCompleteness</c> / <c>EligibilityReview</c> JSON into
    /// the <c>etr_completeness</c> / <c>etr_eligibility</c> typed, camelCase sections, and normalizes the
    /// legacy <c>EtrRedFlag</c> red flags (severity high/medium/low, finding/why_it_matters) into the
    /// run's own <see cref="RedFlag"/> shape (severity red/yellow, title/description) — mirroring
    /// RebuildIepRunAsync's conversion of the legacy IEP shape above.
    /// </summary>
    private async Task RebuildEtrRunAsync(AnalysisRun run, EtrAnalysis legacy, EtrDocument doc, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        _context.AnalysisRunSections.RemoveRange(run.Sections);
        _context.AnalysisRunSources.RemoveRange(run.Sources);

        var (status, errorMessage) = MapStatus(legacy.Status, legacy.ErrorMessage);
        var sourceStatus = status == AnalysisRunStatus.Completed ? AnalysisRunSourceStatus.Completed : AnalysisRunSourceStatus.Error;

        run.Status = status;
        run.ErrorMessage = errorMessage;
        run.OverallSummary = legacy.OverallSummary;
        run.OverallRedFlags = NormalizeEtrRedFlagsJson(legacy.OverallRedFlags, legacy.Id);
        run.AdvocacyGapAnalysis = legacy.AdvocacyGapAnalysis;
        run.ParentGoalsSnapshot = legacy.ParentGoalsSnapshot;

        var newSource = new AnalysisRunSource
        {
            AnalysisRunId = run.Id,
            SourceType = AnalysisSourceType.EtrDocument,
            SourceId = legacy.EtrDocumentId,
            SourceLabel = $"ETR — {doc.EvaluationType} {doc.EvaluationDate:yyyy-MM-dd}",
            SourceContentSnapshot = null,
            Status = sourceStatus
        };
        _context.AnalysisRunSources.Add(newSource);

        // First save: the removals commit and newSource gets its generated Id (see
        // RebuildIepRunAsync's matching comment above).
        await _context.SaveChangesAsync(ct);

        var newSections = new List<AnalysisRunSection>();

        var completenessJson = BuildEtrCompletenessSectionJson(legacy.AssessmentCompleteness, legacy.Id);
        if (completenessJson != null)
        {
            newSections.Add(new AnalysisRunSection
            {
                SectionKind = AnalysisRunSectionKinds.EtrCompleteness,
                Analysis = completenessJson,
                DisplayOrder = newSections.Count
            });
        }

        var eligibilityJson = BuildEtrEligibilitySectionJson(legacy.EligibilityReview, legacy.Id);
        if (eligibilityJson != null)
        {
            newSections.Add(new AnalysisRunSection
            {
                SectionKind = AnalysisRunSectionKinds.EtrEligibility,
                Analysis = eligibilityJson,
                DisplayOrder = newSections.Count
            });
        }

        foreach (var section in newSections)
        {
            section.AnalysisRunId = run.Id;
            section.AnalysisRunSourceId = newSource.Id;
            _context.AnalysisRunSections.Add(section);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _context.ChangeTracker.Clear();
    }

    /// <summary>Converts the legacy snake_case <c>AssessmentCompleteness</c> JSON into the
    /// <c>etr_completeness</c> section's camelCase object shape, round-tripping through the real
    /// <see cref="AssessmentCompletenessResult"/> / <see cref="EtrCompletenessSectionPayload"/> shapes so
    /// the stored JSON matches exactly what the live engine itself produces. Malformed legacy JSON is
    /// logged and skipped (the run is still rebuilt; it just gets no <c>etr_completeness</c> section).</summary>
    private string? BuildEtrCompletenessSectionJson(string? legacyJson, int legacyId)
    {
        if (string.IsNullOrWhiteSpace(legacyJson))
            return null;

        try
        {
            var legacy = JsonSerializer.Deserialize<AssessmentCompletenessResult>(legacyJson, CaseInsensitiveJsonOptions);
            if (legacy is null)
                return null;

            var payload = new EtrCompletenessSectionPayload
            {
                EvaluatedDomains = legacy.EvaluatedDomains.Select(d => new EtrEvaluatedDomain
                {
                    Domain = d.Domain,
                    ToolsUsed = d.ToolsUsed,
                    AdequacyRating = d.AdequacyRating,
                    Notes = d.Notes
                }).ToList(),
                MissingDomains = legacy.MissingDomains.Select(d => new EtrMissingDomain
                {
                    Domain = d.Domain,
                    Rationale = d.Rationale
                }).ToList(),
                OverallCompletenessRating = legacy.OverallCompletenessRating
            };
            return JsonSerializer.Serialize(payload, CamelCaseJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse AssessmentCompleteness for EtrAnalysis {LegacyId}; etr_completeness section skipped", legacyId);
            return null;
        }
    }

    /// <summary>Converts the legacy snake_case <c>EligibilityReview</c> JSON into the
    /// <c>etr_eligibility</c> section's camelCase object shape. See
    /// <see cref="BuildEtrCompletenessSectionJson"/> for the malformed-JSON handling contract.</summary>
    private string? BuildEtrEligibilitySectionJson(string? legacyJson, int legacyId)
    {
        if (string.IsNullOrWhiteSpace(legacyJson))
            return null;

        try
        {
            var legacy = JsonSerializer.Deserialize<EligibilityReviewResult>(legacyJson, CaseInsensitiveJsonOptions);
            if (legacy is null)
                return null;

            var payload = new EtrEligibilitySectionPayload
            {
                StatedCategory = legacy.StatedCategory,
                StatedConclusion = legacy.StatedConclusion,
                DataSupportsConclusion = legacy.DataSupportsConclusion,
                SupportingEvidence = legacy.SupportingEvidence,
                ContradictingEvidence = legacy.ContradictingEvidence,
                AlternativeConsiderations = legacy.AlternativeConsiderations,
                Notes = legacy.Notes
            };
            return JsonSerializer.Serialize(payload, CamelCaseJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse EligibilityReview for EtrAnalysis {LegacyId}; etr_eligibility section skipped", legacyId);
            return null;
        }
    }

    /// <summary>Converts a legacy <c>EtrRedFlag[]</c> JSON array (severity high/medium/low,
    /// finding/why_it_matters) into the run's own <see cref="RedFlag"/> shape (severity red/yellow,
    /// title/description), per the accepted mapping: high→red, medium→low→yellow;
    /// finding→title; why_it_matters→description; parent_right_implicated→legalBasis (the closest
    /// available field — a specific parent right is itself a legal reference). Malformed or empty
    /// input yields an empty array rather than failing the rebuild.</summary>
    private string NormalizeEtrRedFlagsJson(string? legacyJson, int legacyId)
    {
        if (string.IsNullOrWhiteSpace(legacyJson))
            return JsonSerializer.Serialize(new List<RedFlag>(), CamelCaseJsonOptions);

        try
        {
            var legacyFlags = JsonSerializer.Deserialize<List<EtrRedFlag>>(legacyJson, CaseInsensitiveJsonOptions) ?? [];
            var normalized = legacyFlags.Select(f => new RedFlag
            {
                Severity = string.Equals(f.Severity, "high", StringComparison.OrdinalIgnoreCase) ? "red" : "yellow",
                Title = f.Finding,
                Description = f.WhyItMatters,
                LegalBasis = string.IsNullOrWhiteSpace(f.ParentRightImplicated) ? null : f.ParentRightImplicated
            }).ToList();
            return JsonSerializer.Serialize(normalized, CamelCaseJsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Could not parse OverallRedFlags for EtrAnalysis {LegacyId}; normalized to an empty list", legacyId);
            return JsonSerializer.Serialize(new List<RedFlag>(), CamelCaseJsonOptions);
        }
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
