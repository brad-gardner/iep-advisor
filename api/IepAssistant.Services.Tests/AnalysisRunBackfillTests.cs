using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using Xunit;

namespace IepAssistant.Services.Tests;

public class AnalysisRunBackfillTests
{
    private static AnalysisRunBackfillService BuildService(ApplicationDbContext context)
        => new(context, NullLogger<AnalysisRunBackfillService>.Instance);

    private static int SeedIepAnalysis(
        ApplicationDbContext context,
        int iepDocumentId,
        string status = "completed",
        string? sectionAnalyses = null,
        string? goalAnalyses = null,
        string? errorMessage = null)
    {
        var analysis = new IepAnalysis
        {
            IepDocumentId = iepDocumentId,
            Status = status,
            SectionAnalyses = sectionAnalyses,
            GoalAnalyses = goalAnalyses,
            OverallSummary = "IEP overall summary",
            OverallRedFlags = "[\"flag\"]",
            AdvocacyGapAnalysis = "{\"gap\":true}",
            ParentGoalsSnapshot = "[{\"goal\":\"read\"}]",
            ErrorMessage = errorMessage,
            CreatedAt = new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc)
        };
        context.IepAnalyses.Add(analysis);
        context.SaveChanges();
        return analysis.Id;
    }

    private static int SeedEtrAnalysis(
        ApplicationDbContext context,
        int etrDocumentId,
        string status = "completed",
        string? assessmentCompleteness = null,
        string? eligibilityReview = null)
    {
        var analysis = new EtrAnalysis
        {
            EtrDocumentId = etrDocumentId,
            Status = status,
            AssessmentCompleteness = assessmentCompleteness,
            EligibilityReview = eligibilityReview,
            OverallSummary = "ETR overall summary",
            OverallRedFlags = "[\"etr flag\"]",
            AdvocacyGapAnalysis = "{\"gap\":false}",
            ParentGoalsSnapshot = "[{\"goal\":\"math\"}]",
            CreatedAt = new DateTime(2024, 12, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        context.EtrAnalyses.Add(analysis);
        context.SaveChanges();
        return analysis.Id;
    }

    [Fact]
    public async Task BackfillAsync_CreatesOneRunPerLegacyAnalysis_WithSourceAndSections()
    {
        using var fixture = new AnalysisRunTestFixture();

        var iepDocId = fixture.SeedIepDocument();
        var etrDocId = fixture.SeedEtrDocument();

        var sectionAnalyses = """
            [
              {"sectionType":"present_levels","plainLanguageSummary":"PLOP"},
              {"sectionType":"services","plainLanguageSummary":"Services"}
            ]
            """;

        using (var seed = fixture.CreateContext())
        {
            SeedIepAnalysis(seed, iepDocId, sectionAnalyses: sectionAnalyses, goalAnalyses: "[{\"goal\":\"g\"}]");
            SeedEtrAnalysis(seed, etrDocId,
                assessmentCompleteness: "{\"complete\":true}",
                eligibilityReview: "{\"eligible\":true}");
        }

        BackfillResult result;
        using (var context = fixture.CreateContext())
        {
            result = await BuildService(context).BackfillAsync();
        }

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.SkippedExisting);
        Assert.Equal(0, result.SkippedOrphan);

        using var verify = fixture.CreateContext();

        // Row-count parity: one backfilled run per seeded legacy analysis.
        Assert.Equal(2, verify.AnalysisRuns.Count(r => r.BackfillSourceKey != null));

        // Each run has exactly one source.
        foreach (var run in verify.AnalysisRuns.Where(r => r.BackfillSourceKey != null))
        {
            Assert.Equal(1, verify.AnalysisRunSources.Count(s => s.AnalysisRunId == run.Id));
        }

        var iepRun = verify.AnalysisRuns.Single(r => r.BackfillSourceKey!.StartsWith("IepAnalysis:"));
        var iepSource = verify.AnalysisRunSources.Single(s => s.AnalysisRunId == iepRun.Id);
        Assert.Equal(AnalysisSourceType.IepDocument, iepSource.SourceType);
        Assert.Equal(iepDocId, iepSource.SourceId);
        Assert.StartsWith("IEP — ", iepSource.SourceLabel);

        var iepSections = verify.AnalysisRunSections
            .Where(s => s.AnalysisRunId == iepRun.Id)
            .OrderBy(s => s.DisplayOrder)
            .ToList();
        // 2 from SectionAnalyses + 1 from GoalAnalyses.
        Assert.Equal(3, iepSections.Count);
        Assert.Equal("present_levels", iepSections[0].SectionKind);
        Assert.Equal("services", iepSections[1].SectionKind);
        Assert.Equal("annual_goals", iepSections[2].SectionKind);
        Assert.All(iepSections, s => Assert.Equal(iepSource.Id, s.AnalysisRunSourceId));

        var etrRun = verify.AnalysisRuns.Single(r => r.BackfillSourceKey!.StartsWith("EtrAnalysis:"));
        var etrSource = verify.AnalysisRunSources.Single(s => s.AnalysisRunId == etrRun.Id);
        Assert.Equal(AnalysisSourceType.EtrDocument, etrSource.SourceType);
        Assert.Equal(etrDocId, etrSource.SourceId);
        Assert.StartsWith("ETR — ", etrSource.SourceLabel);

        var etrSections = verify.AnalysisRunSections
            .Where(s => s.AnalysisRunId == etrRun.Id)
            .OrderBy(s => s.DisplayOrder)
            .ToList();
        Assert.Equal(2, etrSections.Count);
        Assert.Equal("assessment_completeness", etrSections[0].SectionKind);
        Assert.Equal("eligibility", etrSections[1].SectionKind);

        // Carried-across fields. The AnalysisRun model has no suggested-questions concept
        // at all (the legacy SuggestedQuestions column was dropped in P2a), so there is
        // nothing for the backfill to copy — meeting-relevant questions now live only in
        // Meeting Prep.
        Assert.Equal("IEP overall summary", iepRun.OverallSummary);
        Assert.Equal("[\"flag\"]", iepRun.OverallRedFlags);
        Assert.Equal("{\"gap\":true}", iepRun.AdvocacyGapAnalysis);
        Assert.Equal("[{\"goal\":\"read\"}]", iepRun.ParentGoalsSnapshot);
        Assert.Null(iepRun.CrossDocSynthesis);
        Assert.Equal(new DateTime(2025, 4, 1, 12, 0, 0, DateTimeKind.Utc), iepRun.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task BackfillAsync_IsIdempotent_OnSecondRun()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepDocId = fixture.SeedIepDocument();
        var etrDocId = fixture.SeedEtrDocument();

        using (var seed = fixture.CreateContext())
        {
            SeedIepAnalysis(seed, iepDocId);
            SeedEtrAnalysis(seed, etrDocId);
        }

        using (var context = fixture.CreateContext())
        {
            var first = await BuildService(context).BackfillAsync();
            Assert.Equal(2, first.Created);
        }

        int countAfterFirst;
        using (var verify = fixture.CreateContext())
        {
            countAfterFirst = verify.AnalysisRuns.Count(r => r.BackfillSourceKey != null);
        }

        BackfillResult second;
        using (var context = fixture.CreateContext())
        {
            second = await BuildService(context).BackfillAsync();
        }

        Assert.Equal(0, second.Created);
        Assert.Equal(2, second.SkippedExisting);
        Assert.Equal(0, second.SkippedOrphan);

        using var after = fixture.CreateContext();
        Assert.Equal(countAfterFirst, after.AnalysisRuns.Count(r => r.BackfillSourceKey != null));
    }

    [Fact]
    public async Task BackfillAsync_SkipsOrphan_WhenDocumentMissing()
    {
        using var fixture = new AnalysisRunTestFixture();

        // A hard cascade FK normally prevents an orphan from existing, so we insert an IepAnalysis
        // pointing at a non-existent document with FK enforcement briefly disabled.
        using (var seed = fixture.CreateContext())
        {
            seed.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
            SeedIepAnalysis(seed, iepDocumentId: 9999);
            seed.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        }

        BackfillResult result;
        using (var context = fixture.CreateContext())
        {
            result = await BuildService(context).BackfillAsync();
        }

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.SkippedOrphan);

        using var verify = fixture.CreateContext();
        Assert.Equal(0, verify.AnalysisRuns.Count(r => r.BackfillSourceKey != null));
    }

    [Theory]
    [InlineData("completed", AnalysisRunStatus.Completed, null, null)]
    [InlineData("error", AnalysisRunStatus.Error, "boom", "boom")]
    [InlineData("analyzing", AnalysisRunStatus.Error, null, "Legacy analysis was not completed.")]
    [InlineData("pending", AnalysisRunStatus.Error, null, "Legacy analysis was not completed.")]
    public async Task BackfillAsync_MapsStatusCorrectly(
        string legacyStatus,
        AnalysisRunStatus expectedStatus,
        string? legacyError,
        string? expectedError)
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepDocId = fixture.SeedIepDocument();

        using (var seed = fixture.CreateContext())
        {
            SeedIepAnalysis(seed, iepDocId, status: legacyStatus, errorMessage: legacyError);
        }

        using (var context = fixture.CreateContext())
        {
            await BuildService(context).BackfillAsync();
        }

        using var verify = fixture.CreateContext();
        var run = verify.AnalysisRuns.Single(r => r.BackfillSourceKey != null);
        Assert.Equal(expectedStatus, run.Status);
        Assert.Equal(expectedError, run.ErrorMessage);
    }

    [Fact]
    public async Task BackfillAsync_DoesNotAbort_OnMalformedSectionJson()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepDocId = fixture.SeedIepDocument();

        using (var seed = fixture.CreateContext())
        {
            SeedIepAnalysis(seed, iepDocId, sectionAnalyses: "{ this is not valid json [");
        }

        BackfillResult result;
        using (var context = fixture.CreateContext())
        {
            result = await BuildService(context).BackfillAsync();
        }

        // The run is still created; just the sections are skipped.
        Assert.Equal(1, result.Created);

        using var verify = fixture.CreateContext();
        var run = verify.AnalysisRuns.Single(r => r.BackfillSourceKey != null);
        Assert.Equal(0, verify.AnalysisRunSections.Count(s => s.AnalysisRunId == run.Id));
        Assert.Equal(1, verify.AnalysisRunSources.Count(s => s.AnalysisRunId == run.Id));
    }
}
