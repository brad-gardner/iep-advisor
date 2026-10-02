using System.Text.Json;
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
    public async Task BackfillAsync_RebuildsStaleRunInPlace_WhenLegacyUpdatedAfterFirstBackfill()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepDocId = fixture.SeedIepDocument();

        int legacyId;
        using (var seed = fixture.CreateContext())
        {
            legacyId = SeedIepAnalysis(seed, iepDocId, status: "error", errorMessage: "boom");
        }

        int runId;
        using (var context = fixture.CreateContext())
        {
            var first = await BuildService(context).BackfillAsync();
            Assert.Equal(1, first.Created);
        }
        using (var verify = fixture.CreateContext())
        {
            var run = verify.AnalysisRuns.Single(r => r.BackfillSourceKey == $"IepAnalysis:{legacyId}");
            runId = run.Id;
            Assert.Equal(AnalysisRunStatus.Error, run.Status);
        }

        // The legacy analysis is re-run and completes successfully some time later — its UpdatedAt
        // moves past the run's own last-touched timestamp (set via sync SaveChanges, which — unlike
        // the service's own SaveChangesAsync — does not go through the auditing override, so the
        // explicit value sticks).
        using (var update = fixture.CreateContext())
        {
            var legacy = update.IepAnalyses.Single(a => a.Id == legacyId);
            legacy.Status = "completed";
            legacy.ErrorMessage = null;
            legacy.OverallSummary = "Updated after re-analysis";
            legacy.GoalAnalyses = """[{"goalId":1,"goalText":"Read 100 wpm","overallRating":"green","plainLanguageSummary":"Clear and measurable."}]""";
            legacy.UpdatedAt = DateTime.UtcNow.AddHours(1);
            update.SaveChanges();
        }

        BackfillResult second;
        using (var context = fixture.CreateContext())
        {
            second = await BuildService(context).BackfillAsync();
        }

        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
        Assert.Equal(0, second.SkippedExisting);

        using var final = fixture.CreateContext();
        var rebuilt = final.AnalysisRuns.Single(r => r.BackfillSourceKey == $"IepAnalysis:{legacyId}");
        Assert.Equal(runId, rebuilt.Id); // same run id — rebuilt in place, not a new run
        Assert.Equal(AnalysisRunStatus.Completed, rebuilt.Status); // status flips Error -> Completed
        Assert.Null(rebuilt.ErrorMessage);
        Assert.Equal("Updated after re-analysis", rebuilt.OverallSummary);

        var sources = final.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).ToList();
        var source = Assert.Single(sources);
        Assert.Equal(AnalysisRunSourceStatus.Completed, source.Status); // source Status set, not left Pending

        var sections = final.AnalysisRunSections.Where(s => s.AnalysisRunId == runId).ToList();
        Assert.DoesNotContain(sections, s => s.SectionKind == "annual_goals"); // old array shape gone
        var goalsSection = Assert.Single(sections, s => s.SectionKind == "iep_goals"); // converted to the object shape
        using var goalsDoc = JsonDocument.Parse(goalsSection.Analysis!);
        var goalAnalyses = goalsDoc.RootElement.GetProperty("goalAnalyses");
        Assert.Equal(JsonValueKind.Array, goalAnalyses.ValueKind); // {goalAnalyses:[...]}, not a bare array
        Assert.Equal(1, goalAnalyses.GetArrayLength());
        Assert.Equal("green", goalAnalyses[0].GetProperty("overallRating").GetString());
    }

    [Fact]
    public async Task BackfillAsync_LeavesUpToDateRun_Untouched()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepDocId = fixture.SeedIepDocument();

        using (var seed = fixture.CreateContext())
        {
            SeedIepAnalysis(seed, iepDocId);
        }

        int runId;
        using (var context = fixture.CreateContext())
        {
            await BuildService(context).BackfillAsync();
        }
        using (var verify = fixture.CreateContext())
        {
            runId = verify.AnalysisRuns.Single(r => r.BackfillSourceKey != null).Id;
        }

        BackfillResult second;
        using (var context = fixture.CreateContext())
        {
            second = await BuildService(context).BackfillAsync();
        }

        // Neither the UpdatedAt rule nor the shape rule fires (SeedIepAnalysis leaves GoalAnalyses
        // null, so there is no annual_goals section to begin with) — the run is left alone.
        Assert.Equal(0, second.Updated);
        Assert.Equal(1, second.SkippedExisting);

        using var final = fixture.CreateContext();
        Assert.Equal(runId, final.AnalysisRuns.Single(r => r.BackfillSourceKey != null).Id);
    }

    [Fact]
    public async Task BackfillAsync_RebuildsStaleEtrRunInPlace_ConvertsSnakeCaseSectionsAndNormalizesRedFlagSeverity()
    {
        using var fixture = new AnalysisRunTestFixture();
        var etrDocId = fixture.SeedEtrDocument();

        int legacyId;
        using (var seed = fixture.CreateContext())
        {
            legacyId = SeedEtrAnalysis(seed, etrDocId, status: "error",
                assessmentCompleteness: """{"evaluated_domains":[],"missing_domains":[],"overall_completeness_rating":"thin"}""",
                eligibilityReview: """{"stated_category":null,"stated_conclusion":null,"data_supports_conclusion":false,"supporting_evidence":[],"contradicting_evidence":[],"alternative_considerations":[]}""");
        }

        int runId;
        using (var context = fixture.CreateContext())
        {
            var first = await BuildService(context).BackfillAsync();
            Assert.Equal(1, first.Created);
        }
        using (var verify = fixture.CreateContext())
        {
            var run = verify.AnalysisRuns.Single(r => r.BackfillSourceKey == $"EtrAnalysis:{legacyId}");
            runId = run.Id;
            Assert.Equal(AnalysisRunStatus.Error, run.Status);
            // The initial create path deliberately keeps the OLD snake_case section kinds (mirrors the
            // IEP annual_goals array staying untouched on create) — converged on the NEXT pass below.
            Assert.Contains(verify.AnalysisRunSections.Where(s => s.AnalysisRunId == runId), s => s.SectionKind == "assessment_completeness");
        }

        // The legacy analysis is re-run and completes successfully some time later, with a mix of
        // high/medium/low red flags to exercise the severity normalization (high -> red, medium/low -> yellow).
        using (var update = fixture.CreateContext())
        {
            var legacy = update.EtrAnalyses.Single(a => a.Id == legacyId);
            legacy.Status = "completed";
            legacy.ErrorMessage = null;
            legacy.OverallSummary = "Updated after re-analysis";
            legacy.AssessmentCompleteness = """
                {"evaluated_domains":[{"domain":"Cognitive","tools_used":["WISC-V"],"adequacy_rating":"strong","notes":"solid"}],"missing_domains":[],"overall_completeness_rating":"strong"}
                """;
            legacy.EligibilityReview = """
                {"stated_category":"Specific Learning Disability","stated_conclusion":"qualifies","data_supports_conclusion":true,"supporting_evidence":[],"contradicting_evidence":[],"alternative_considerations":[]}
                """;
            legacy.OverallRedFlags = """
                [
                  {"severity":"high","category":"missing_domain","finding":"No adaptive testing","why_it_matters":"Adaptive concerns were raised by the parent.","parent_right_implicated":"Right to request an IEE"},
                  {"severity":"medium","category":"outdated_testing","finding":"Cognitive testing is 4 years old","why_it_matters":"May not reflect current functioning."},
                  {"severity":"low","category":"procedural","finding":"Minor formatting issue","why_it_matters":"Cosmetic only."}
                ]
                """;
            legacy.UpdatedAt = DateTime.UtcNow.AddHours(1);
            update.SaveChanges();
        }

        BackfillResult second;
        using (var context = fixture.CreateContext())
        {
            second = await BuildService(context).BackfillAsync();
        }

        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
        Assert.Equal(0, second.SkippedExisting);

        using var final = fixture.CreateContext();
        var rebuilt = final.AnalysisRuns.Single(r => r.BackfillSourceKey == $"EtrAnalysis:{legacyId}");
        Assert.Equal(runId, rebuilt.Id); // same run id — rebuilt in place, not a new run
        Assert.Equal(AnalysisRunStatus.Completed, rebuilt.Status);
        Assert.Null(rebuilt.ErrorMessage);
        Assert.Equal("Updated after re-analysis", rebuilt.OverallSummary);

        var sources = final.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).ToList();
        var source = Assert.Single(sources);
        Assert.Equal(AnalysisRunSourceStatus.Completed, source.Status);

        var sections = final.AnalysisRunSections.Where(s => s.AnalysisRunId == runId).ToList();
        Assert.DoesNotContain(sections, s => s.SectionKind is "assessment_completeness" or "eligibility"); // old shape gone

        var completenessSection = Assert.Single(sections, s => s.SectionKind == "etr_completeness");
        using (var completenessDoc = JsonDocument.Parse(completenessSection.Analysis!))
        {
            Assert.Equal("strong", completenessDoc.RootElement.GetProperty("overallCompletenessRating").GetString());
            var evaluatedDomains = completenessDoc.RootElement.GetProperty("evaluatedDomains");
            Assert.Equal(1, evaluatedDomains.GetArrayLength());
            Assert.Equal("Cognitive", evaluatedDomains[0].GetProperty("domain").GetString());
        }

        var eligibilitySection = Assert.Single(sections, s => s.SectionKind == "etr_eligibility");
        using (var eligibilityDoc = JsonDocument.Parse(eligibilitySection.Analysis!))
        {
            Assert.Equal("Specific Learning Disability", eligibilityDoc.RootElement.GetProperty("statedCategory").GetString());
            Assert.True(eligibilityDoc.RootElement.GetProperty("dataSupportsConclusion").GetBoolean());
        }

        // Severity mapping: high -> red; medium and low -> yellow. finding -> title; why_it_matters ->
        // description; parent_right_implicated -> legalBasis.
        using var redFlagsDoc = JsonDocument.Parse(rebuilt.OverallRedFlags!);
        var flags = redFlagsDoc.RootElement.EnumerateArray().ToList();
        Assert.Equal(3, flags.Count);
        Assert.Equal("red", flags[0].GetProperty("severity").GetString());
        Assert.Equal("No adaptive testing", flags[0].GetProperty("title").GetString());
        Assert.Equal("Adaptive concerns were raised by the parent.", flags[0].GetProperty("description").GetString());
        Assert.Equal("Right to request an IEE", flags[0].GetProperty("legalBasis").GetString());
        Assert.Equal("yellow", flags[1].GetProperty("severity").GetString());
        Assert.Equal("yellow", flags[2].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task BackfillAsync_LeavesUpToDateEtrRun_Untouched()
    {
        using var fixture = new AnalysisRunTestFixture();
        var etrDocId = fixture.SeedEtrDocument();

        using (var seed = fixture.CreateContext())
        {
            // No AssessmentCompleteness/EligibilityReview at all, so there is no legacy section shape
            // to trigger a rebuild, and the row is not touched again after the first backfill.
            SeedEtrAnalysis(seed, etrDocId);
        }

        int runId;
        using (var context = fixture.CreateContext())
        {
            await BuildService(context).BackfillAsync();
        }
        using (var verify = fixture.CreateContext())
        {
            runId = verify.AnalysisRuns.Single(r => r.BackfillSourceKey != null).Id;
        }

        BackfillResult second;
        using (var context = fixture.CreateContext())
        {
            second = await BuildService(context).BackfillAsync();
        }

        Assert.Equal(0, second.Updated);
        Assert.Equal(1, second.SkippedExisting);

        using var final = fixture.CreateContext();
        Assert.Equal(runId, final.AnalysisRuns.Single(r => r.BackfillSourceKey != null).Id);
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
