using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// IEP comparison's red-flag counts and resolved/persisting/new diff now come from each IEP's latest
/// COMPLETED analysis run that includes it, not the retired per-document IepAnalysis row.
/// </summary>
public class IepComparisonServiceTests
{
    private sealed class AllowAll : IAccessService
    {
        public Task<AccessRole?> GetRoleAsync(int childId, int userId, CancellationToken ct = default) =>
            Task.FromResult<AccessRole?>(AccessRole.Owner);

        public Task<bool> HasMinimumRoleAsync(int childId, int userId, AccessRole minimumRole, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private static IepComparisonService Service(Domain.Data.ApplicationDbContext context) =>
        new(context, new ChildProfileRepository(context), new AllowAll());

    private static void AddCompletedRun(
        Domain.Data.ApplicationDbContext context, int childId, int iepDocumentId, string? runOverallRedFlagsJson,
        string? sectionRedFlagsJson = null, int sourceCount = 1)
    {
        var run = new AnalysisRun { ChildProfileId = childId, Status = AnalysisRunStatus.Completed, OverallRedFlags = runOverallRedFlagsJson };
        context.AnalysisRuns.Add(run);
        context.SaveChanges();

        var source = new AnalysisRunSource
        {
            AnalysisRunId = run.Id, SourceType = AnalysisSourceType.IepDocument, SourceId = iepDocumentId,
            SourceLabel = "IEP", Status = AnalysisRunSourceStatus.Completed
        };
        context.AnalysisRunSources.Add(source);
        context.SaveChanges();

        if (sourceCount > 1)
        {
            // A second (unrelated) source, just to make the run multi-source for the rollup rule.
            context.AnalysisRunSources.Add(new AnalysisRunSource
            {
                AnalysisRunId = run.Id, SourceType = AnalysisSourceType.EtrDocument, SourceId = 999_000 + iepDocumentId,
                SourceLabel = "ETR", Status = AnalysisRunSourceStatus.Completed
            });
            context.SaveChanges();
        }

        if (sectionRedFlagsJson != null)
        {
            context.AnalysisRunSections.Add(new AnalysisRunSection
            {
                AnalysisRunId = run.Id, AnalysisRunSourceId = source.Id, SectionKind = "present_levels",
                Analysis = $$"""{"sectionKind":"present_levels","plainLanguageSummary":"x","keyPoints":[],"redFlags":{{sectionRedFlagsJson}},"legalReferences":[]}""",
                DisplayOrder = 0
            });
            context.SaveChanges();
        }
    }

    [Fact]
    public async Task GetTimelineAsync_ReadsRedFlagCountAndHasAnalysis_FromLatestCompletedRun()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepId = fixture.SeedIepDocument();

        using (var context = fixture.CreateContext())
        {
            // Single-source run: its OverallRedFlags (2 flags) count; the section's own flag (1) adds a third.
            AddCompletedRun(
                context, fixture.ChildId, iepId,
                runOverallRedFlagsJson: """[{"severity":"red","title":"A"},{"severity":"yellow","title":"B"}]""",
                sectionRedFlagsJson: """[{"severity":"yellow","title":"C"}]""");
        }

        using var context2 = fixture.CreateContext();
        var result = await Service(context2).GetTimelineAsync(fixture.ChildId, fixture.OwnerUserId, CancellationToken.None);

        Assert.NotNull(result);
        var entry = Assert.Single(result!.Ieps);
        Assert.True(entry.HasAnalysis);
        Assert.Equal(3, entry.RedFlagCount);
    }

    [Fact]
    public async Task GetTimelineAsync_MultiSourceRun_ExcludesRunLevelRedFlags_KeepsSectionFlags()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepId = fixture.SeedIepDocument();

        using (var context = fixture.CreateContext())
        {
            // Multi-source: the run's OverallRedFlags is the cross-document synthesis view and must
            // NOT be counted for this one document — only this source's own section flag counts.
            AddCompletedRun(
                context, fixture.ChildId, iepId,
                runOverallRedFlagsJson: """[{"severity":"red","title":"Cross-doc concern"}]""",
                sectionRedFlagsJson: """[{"severity":"yellow","title":"Doc-specific concern"}]""",
                sourceCount: 2);
        }

        using var context2 = fixture.CreateContext();
        var result = await Service(context2).GetTimelineAsync(fixture.ChildId, fixture.OwnerUserId, CancellationToken.None);

        var entry = Assert.Single(result!.Ieps);
        Assert.True(entry.HasAnalysis);
        Assert.Equal(1, entry.RedFlagCount);
    }

    [Fact]
    public async Task GetTimelineAsync_MultipleDocuments_EachGetsItsOwnLatestRunRedFlags()
    {
        // Item K: GetTimelineAsync now batches the "latest completed run's red flags per document"
        // lookup (one query for the latest source per document, one for those sources' sections)
        // instead of querying per document in a loop. This is the regression risk that batching
        // introduces: documents' red flags must never leak into each other via the GroupBy.
        using var fixture = new AnalysisRunTestFixture();
        var iepId1 = fixture.SeedIepDocument();
        var iepId2 = fixture.SeedIepDocument();

        using (var context = fixture.CreateContext())
        {
            AddCompletedRun(context, fixture.ChildId, iepId1,
                runOverallRedFlagsJson: """[{"severity":"red","title":"A"}]""");
            AddCompletedRun(context, fixture.ChildId, iepId2,
                runOverallRedFlagsJson: """[{"severity":"yellow","title":"B"},{"severity":"yellow","title":"C"}]""");
        }

        using var context2 = fixture.CreateContext();
        var result = await Service(context2).GetTimelineAsync(fixture.ChildId, fixture.OwnerUserId, CancellationToken.None);

        Assert.Equal(2, result!.Ieps.Count);
        var entry1 = result.Ieps.Single(e => e.Id == iepId1);
        var entry2 = result.Ieps.Single(e => e.Id == iepId2);
        Assert.True(entry1.HasAnalysis);
        Assert.Equal(1, entry1.RedFlagCount);
        Assert.True(entry2.HasAnalysis);
        Assert.Equal(2, entry2.RedFlagCount);
    }

    [Fact]
    public async Task GetTimelineAsync_NoCompletedRun_HasAnalysisFalse_ZeroRedFlags()
    {
        using var fixture = new AnalysisRunTestFixture();
        fixture.SeedIepDocument();

        using var context = fixture.CreateContext();
        var result = await Service(context).GetTimelineAsync(fixture.ChildId, fixture.OwnerUserId, CancellationToken.None);

        var entry = Assert.Single(result!.Ieps);
        Assert.False(entry.HasAnalysis);
        Assert.Equal(0, entry.RedFlagCount);
    }

    [Fact]
    public async Task CompareAsync_RedFlagResolution_ReadsFromEachIepsLatestCompletedRun()
    {
        using var fixture = new AnalysisRunTestFixture();
        var olderIepId = fixture.SeedIepDocument();
        var newerIepId = fixture.SeedIepDocument();

        using (var context = fixture.CreateContext())
        {
            // Backdate the older IEP so CompareAsync orders them correctly.
            context.IepDocuments.Single(d => d.Id == olderIepId).IepDate = new DateTime(2024, 1, 1);
            context.IepDocuments.Single(d => d.Id == newerIepId).IepDate = new DateTime(2026, 1, 1);
            context.SaveChanges();

            AddCompletedRun(context, fixture.ChildId, olderIepId,
                runOverallRedFlagsJson: """[{"severity":"red","title":"No baseline","description":"d"}]""");
            AddCompletedRun(context, fixture.ChildId, newerIepId,
                runOverallRedFlagsJson: """[{"severity":"yellow","title":"Still vague","description":"d"}]""");
        }

        using var compareContext = fixture.CreateContext();
        var result = await Service(compareContext).CompareAsync(olderIepId, newerIepId, fixture.OwnerUserId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(olderIepId, result!.OlderIepId);
        Assert.Equal(newerIepId, result.NewerIepId);
        Assert.Single(result.RedFlagResolution.Resolved); // "No baseline" has no match in the newer flags
        Assert.Single(result.RedFlagResolution.NewFlags); // "Still vague" is new
        Assert.Empty(result.RedFlagResolution.Persisting);
        Assert.Equal(1, result.Summary.RedFlagsResolved);
        Assert.Equal(1, result.Summary.NewRedFlags);
    }
}
