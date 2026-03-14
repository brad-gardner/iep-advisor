using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class IepComparisonService : IIepComparisonService
{
    private readonly ApplicationDbContext _context;
    private readonly IChildProfileRepository _childRepository;

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IepComparisonService(ApplicationDbContext context, IChildProfileRepository childRepository)
    {
        _context = context;
        _childRepository = childRepository;
    }

    public async Task<TimelineResult?> GetTimelineAsync(int childId, int userId, CancellationToken ct = default)
    {
        var child = await _childRepository.GetByIdForUserAsync(childId, userId, ct);
        if (child == null)
            return null;

        var documents = await _context.IepDocuments
            .Where(d => d.ChildProfileId == childId && d.IsActive)
            .OrderByDescending(d => d.IepDate)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        var documentIds = documents.Select(d => d.Id).ToList();

        var sectionCounts = await _context.IepSections
            .Where(s => documentIds.Contains(s.IepDocumentId))
            .GroupBy(s => s.IepDocumentId)
            .Select(g => new { IepDocumentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var goalCounts = await _context.Goals
            .Where(g => documentIds.Contains(g.IepSection.IepDocumentId))
            .GroupBy(g => g.IepSection.IepDocumentId)
            .Select(g => new { IepDocumentId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var analyses = await _context.IepAnalyses
            .Where(a => documentIds.Contains(a.IepDocumentId) && a.Status == "completed")
            .ToListAsync(ct);

        var sectionCountMap = sectionCounts.ToDictionary(x => x.IepDocumentId, x => x.Count);
        var goalCountMap = goalCounts.ToDictionary(x => x.IepDocumentId, x => x.Count);
        var analysisMap = analyses
            .GroupBy(a => a.IepDocumentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());

        var entries = documents.Select(d =>
        {
            var hasAnalysis = analysisMap.ContainsKey(d.Id);
            var redFlagCount = 0;

            if (hasAnalysis && analysisMap[d.Id].OverallRedFlags != null)
            {
                try
                {
                    var flags = JsonSerializer.Deserialize<List<RedFlag>>(
                        analysisMap[d.Id].OverallRedFlags!, CaseInsensitiveOptions);
                    redFlagCount = flags?.Count ?? 0;
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }

            return new TimelineEntry
            {
                Id = d.Id,
                IepDate = d.IepDate,
                MeetingType = d.MeetingType,
                Status = d.Status,
                GoalCount = goalCountMap.GetValueOrDefault(d.Id),
                SectionCount = sectionCountMap.GetValueOrDefault(d.Id),
                RedFlagCount = redFlagCount,
                HasAnalysis = hasAnalysis
            };
        }).ToList();

        return new TimelineResult
        {
            ChildId = childId,
            Ieps = entries
        };
    }

    public async Task<ComparisonResult?> CompareAsync(int iepId, int otherIepId, int userId, CancellationToken ct = default)
    {
        var iep1 = await _context.IepDocuments
            .Include(d => d.ChildProfile)
            .FirstOrDefaultAsync(d => d.Id == iepId && d.IsActive, ct);

        var iep2 = await _context.IepDocuments
            .Include(d => d.ChildProfile)
            .FirstOrDefaultAsync(d => d.Id == otherIepId && d.IsActive, ct);

        if (iep1 == null || iep2 == null)
            return null;

        // Both must belong to the same child and the user must own that child
        if (iep1.ChildProfileId != iep2.ChildProfileId)
            return null;

        var child = await _childRepository.GetByIdForUserAsync(iep1.ChildProfileId, userId, ct);
        if (child == null)
            return null;

        // Determine older vs newer
        var date1 = iep1.IepDate ?? iep1.CreatedAt;
        var date2 = iep2.IepDate ?? iep2.CreatedAt;
        var (older, newer) = date1 <= date2 ? (iep1, iep2) : (iep2, iep1);

        // Load sections + goals for both
        var olderSections = await _context.IepSections
            .Include(s => s.Goals)
            .Where(s => s.IepDocumentId == older.Id)
            .ToListAsync(ct);

        var newerSections = await _context.IepSections
            .Include(s => s.Goals)
            .Where(s => s.IepDocumentId == newer.Id)
            .ToListAsync(ct);

        var goalChanges = CompareGoals(olderSections, newerSections);
        var sectionChanges = CompareSections(olderSections, newerSections);
        var redFlagResolution = await CompareRedFlagsAsync(older.Id, newer.Id, ct);

        var olderGoalCount = olderSections.SelectMany(s => s.Goals).Count();
        var newerGoalCount = newerSections.SelectMany(s => s.Goals).Count();
        var unchangedCount = olderGoalCount - goalChanges.Removed.Count - goalChanges.Modified.Count;
        if (unchangedCount < 0) unchangedCount = 0;

        return new ComparisonResult
        {
            OlderIepId = older.Id,
            NewerIepId = newer.Id,
            OlderDate = older.IepDate,
            NewerDate = newer.IepDate,
            GoalChanges = goalChanges,
            SectionChanges = sectionChanges,
            RedFlagResolution = redFlagResolution,
            Summary = new ComparisonSummary
            {
                GoalsAdded = goalChanges.Added.Count,
                GoalsRemoved = goalChanges.Removed.Count,
                GoalsModified = goalChanges.Modified.Count,
                GoalsUnchanged = unchangedCount,
                SectionsAdded = sectionChanges.Added.Count,
                SectionsRemoved = sectionChanges.Removed.Count,
                RedFlagsResolved = redFlagResolution.Resolved.Count,
                RedFlagsPersisting = redFlagResolution.Persisting.Count,
                NewRedFlags = redFlagResolution.NewFlags.Count
            }
        };
    }

    private static GoalChanges CompareGoals(List<IepSection> olderSections, List<IepSection> newerSections)
    {
        var olderGoals = olderSections.SelectMany(s => s.Goals).ToList();
        var newerGoals = newerSections.SelectMany(s => s.Goals).ToList();

        var added = new List<GoalDiff>();
        var removed = new List<GoalDiff>();
        var modified = new List<ModifiedGoalDiff>();

        var matchedOlder = new HashSet<int>();
        var matchedNewer = new HashSet<int>();

        // Group by domain for matching
        var olderByDomain = olderGoals.GroupBy(g => g.Domain?.ToLowerInvariant() ?? "").ToDictionary(g => g.Key, g => g.ToList());
        var newerByDomain = newerGoals.GroupBy(g => g.Domain?.ToLowerInvariant() ?? "").ToDictionary(g => g.Key, g => g.ToList());

        var allDomains = olderByDomain.Keys.Union(newerByDomain.Keys).ToList();

        foreach (var domain in allDomains)
        {
            var domainOlder = olderByDomain.GetValueOrDefault(domain, []);
            var domainNewer = newerByDomain.GetValueOrDefault(domain, []);

            // Find best matches by text similarity
            foreach (var og in domainOlder)
            {
                Goal? bestMatch = null;
                double bestSimilarity = 0;

                foreach (var ng in domainNewer)
                {
                    if (matchedNewer.Contains(ng.Id)) continue;

                    var similarity = TextSimilarity(og.GoalText, ng.GoalText);
                    if (similarity > bestSimilarity)
                    {
                        bestSimilarity = similarity;
                        bestMatch = ng;
                    }
                }

                if (bestMatch != null && bestSimilarity > 0.5)
                {
                    matchedOlder.Add(og.Id);
                    matchedNewer.Add(bestMatch.Id);

                    // Check for field-by-field changes
                    var changes = CompareGoalFields(og, bestMatch);
                    if (changes.Count > 0)
                    {
                        modified.Add(new ModifiedGoalDiff
                        {
                            Domain = og.Domain ?? bestMatch.Domain,
                            OlderGoalText = og.GoalText,
                            NewerGoalText = bestMatch.GoalText,
                            Changes = changes
                        });
                    }
                    // else: matched but unchanged — not reported
                }
            }
        }

        // Unmatched older goals = removed
        foreach (var og in olderGoals.Where(g => !matchedOlder.Contains(g.Id)))
        {
            removed.Add(new GoalDiff { Domain = og.Domain, GoalText = og.GoalText });
        }

        // Unmatched newer goals = added
        foreach (var ng in newerGoals.Where(g => !matchedNewer.Contains(g.Id)))
        {
            added.Add(new GoalDiff { Domain = ng.Domain, GoalText = ng.GoalText });
        }

        return new GoalChanges { Added = added, Removed = removed, Modified = modified };
    }

    private static List<GoalChangeDetail> CompareGoalFields(Goal older, Goal newer)
    {
        var changes = new List<GoalChangeDetail>();

        if (!StringEquals(older.GoalText, newer.GoalText))
            changes.Add(new GoalChangeDetail { Field = "GoalText", Older = older.GoalText, Newer = newer.GoalText });

        if (!StringEquals(older.Baseline, newer.Baseline))
            changes.Add(new GoalChangeDetail { Field = "Baseline", Older = older.Baseline, Newer = newer.Baseline });

        if (!StringEquals(older.TargetCriteria, newer.TargetCriteria))
            changes.Add(new GoalChangeDetail { Field = "TargetCriteria", Older = older.TargetCriteria, Newer = newer.TargetCriteria });

        if (!StringEquals(older.MeasurementMethod, newer.MeasurementMethod))
            changes.Add(new GoalChangeDetail { Field = "MeasurementMethod", Older = older.MeasurementMethod, Newer = newer.MeasurementMethod });

        if (!StringEquals(older.Timeframe, newer.Timeframe))
            changes.Add(new GoalChangeDetail { Field = "Timeframe", Older = older.Timeframe, Newer = newer.Timeframe });

        return changes;
    }

    private static bool StringEquals(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return true;
        return string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static SectionChanges CompareSections(List<IepSection> olderSections, List<IepSection> newerSections)
    {
        var olderTypes = olderSections.Select(s => s.SectionType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newerTypes = newerSections.Select(s => s.SectionType).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SectionChanges
        {
            Added = newerTypes.Except(olderTypes, StringComparer.OrdinalIgnoreCase).ToList(),
            Removed = olderTypes.Except(newerTypes, StringComparer.OrdinalIgnoreCase).ToList(),
            InBoth = olderTypes.Intersect(newerTypes, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<RedFlagResolution> CompareRedFlagsAsync(int olderIepId, int newerIepId, CancellationToken ct)
    {
        var olderAnalysis = await _context.IepAnalyses
            .Where(a => a.IepDocumentId == olderIepId && a.Status == "completed")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var newerAnalysis = await _context.IepAnalyses
            .Where(a => a.IepDocumentId == newerIepId && a.Status == "completed")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var olderFlags = DeserializeRedFlags(olderAnalysis?.OverallRedFlags);
        var newerFlags = DeserializeRedFlags(newerAnalysis?.OverallRedFlags);

        if (olderFlags.Count == 0 && newerFlags.Count == 0)
            return new RedFlagResolution();

        var resolved = new List<RedFlagChange>();
        var persisting = new List<RedFlagChange>();
        var newFlags = new List<RedFlagChange>();

        var matchedNewer = new HashSet<int>();

        foreach (var olderFlag in olderFlags)
        {
            RedFlag? bestMatch = null;
            double bestSimilarity = 0;
            int bestIndex = -1;

            for (int i = 0; i < newerFlags.Count; i++)
            {
                if (matchedNewer.Contains(i)) continue;

                var similarity = TextSimilarity(olderFlag.Title, newerFlags[i].Title);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestMatch = newerFlags[i];
                    bestIndex = i;
                }
            }

            if (bestMatch != null && bestSimilarity > 0.6)
            {
                matchedNewer.Add(bestIndex);
                persisting.Add(new RedFlagChange
                {
                    Title = bestMatch.Title,
                    Description = bestMatch.Description
                });
            }
            else
            {
                resolved.Add(new RedFlagChange
                {
                    Title = olderFlag.Title,
                    Description = olderFlag.Description
                });
            }
        }

        for (int i = 0; i < newerFlags.Count; i++)
        {
            if (matchedNewer.Contains(i)) continue;
            newFlags.Add(new RedFlagChange
            {
                Title = newerFlags[i].Title,
                Description = newerFlags[i].Description
            });
        }

        return new RedFlagResolution
        {
            Resolved = resolved,
            Persisting = persisting,
            NewFlags = newFlags
        };
    }

    private List<RedFlag> DeserializeRedFlags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<RedFlag>>(json, CaseInsensitiveOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));

        return dp[a.Length, b.Length];
    }

    private static double TextSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b)) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var distance = LevenshteinDistance(a.ToLowerInvariant(), b.ToLowerInvariant());
        return 1.0 - ((double)distance / Math.Max(a.Length, b.Length));
    }
}
