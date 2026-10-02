using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

public class AnalysisRunServiceTests
{
    // Each test gets its own fresh SQLite in-memory database (the per-child usage limit makes
    // a shared DB order-dependent), so the fixture is created per-test rather than via IClassFixture.

    // A hand-written fake — no Moq. Returns a canned response (or null) on every call, regardless of
    // call count. Used by CreateRunAsync-only tests, which never invoke Claude at all.
    private sealed class FakeClaudeClient : IClaudeClient
    {
        private readonly string? _response;
        public FakeClaudeClient(string? response) => _response = response;
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);
    }

    // Simulates graceful shutdown landing mid-Claude-call: the host cancels the token while the
    // request is in flight, and ClaudeClient propagates the cancellation rather than relabelling it
    // a timeout. This is the only way to reach ExecuteRunAsync's broad catch with a cancelled token.
    private sealed class CancellingClaudeClient : IClaudeClient
    {
        private readonly CancellationTokenSource _cts;
        public CancellingClaudeClient(CancellationTokenSource cts) => _cts = cts;

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<string?>(null);
        }
    }

    // A fake that fails the way ClaudeClient now does, on every call. The returns-a-string/
    // returns-null fakes above can never exercise the typed catch, which is the path that both
    // classifies the failure and (for a single-source run) refunds the reserved quota unit.
    private sealed class ThrowingClaudeClient : IClaudeClient
    {
        private readonly ClaudeFailureKind _kind;
        public ThrowingClaudeClient(ClaudeFailureKind kind) => _kind = kind;

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
            => throw new ClaudeApiException(_kind);
    }

    // The per-source engine now makes MULTIPLE Claude calls per run (one per source, plus a
    // synthesis call when 2+ sources complete), so a single canned response is not enough to test
    // it. This fake dequeues one scripted responder per call, in order, and records every request
    // (so a test can assert call count and inspect each prompt).
    private sealed class ScriptedClaudeClient : IClaudeClient
    {
        private readonly Queue<Func<string?>> _responders;
        public List<ClaudeCompletionRequest> Requests { get; } = [];
        public int CallCount => Requests.Count;

        public ScriptedClaudeClient(params string?[] responses)
            : this(responses.Select(r => (Func<string?>)(() => r)).ToArray())
        {
        }

        public ScriptedClaudeClient(params Func<string?>[] responders)
        {
            _responders = new Queue<Func<string?>>(responders);
        }

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_responders.Count == 0)
                throw new InvalidOperationException("ScriptedClaudeClient: no more scripted responses were queued.");
            return Task.FromResult(_responders.Dequeue()());
        }
    }

    private AnalysisRunService BuildService(ApplicationDbContext context, IClaudeClient claudeClient)
    {
        var accessService = new AccessService(context);
        var subscriptionService = new SubscriptionService(
            context,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            NullLogger<SubscriptionService>.Instance);
        var goalRepo = new ParentAdvocacyGoalRepository(context);

        return new AnalysisRunService(
            context,
            accessService,
            subscriptionService,
            goalRepo,
            claudeClient,
            NullLogger<AnalysisRunService>.Instance);
    }

    /// <summary>Builds a canned per-source call response (<c>SourceAnalysisResponse</c>'s shape —
    /// no "sources" wrapper: each call covers exactly one source).</summary>
    private static string BuildSourceJson(
        string sectionKind = "present_levels",
        IReadOnlyList<(int GoalId, string Rating)>? goals = null,
        string summary = "Structured summary text.")
    {
        var goalsJson = goals == null
            ? ""
            : $@",""goalAnalyses"": [{string.Join(",", goals.Select(g => $@"
        {{
          ""goalId"": {g.GoalId},
          ""goalText"": ""Goal text"",
          ""domain"": ""Reading"",
          ""smartAnalysis"": {{
            ""specific"": {{ ""rating"": ""{g.Rating}"", ""explanation"": ""x"" }},
            ""measurable"": {{ ""rating"": ""{g.Rating}"", ""explanation"": ""x"" }},
            ""achievable"": {{ ""rating"": ""{g.Rating}"", ""explanation"": ""x"" }},
            ""relevant"": {{ ""rating"": ""{g.Rating}"", ""explanation"": ""x"" }},
            ""timeBound"": {{ ""rating"": ""{g.Rating}"", ""explanation"": ""x"" }}
          }},
          ""overallRating"": ""{g.Rating}"",
          ""plainLanguageSummary"": ""Goal summary"",
          ""strengths"": [],
          ""concerns"": [],
          ""suggestedImprovements"": []
        }}"))}]";

        return $@"{{
          ""overallSummary"": ""{summary}"",
          ""sections"": [
            {{
              ""sectionKind"": ""{sectionKind}"",
              ""plainLanguageSummary"": ""Section summary"",
              ""keyPoints"": [""point""],
              ""redFlags"": [],
              ""legalReferences"": []
            }}
          ]{goalsJson},
          ""overallRedFlags"": []
        }}";
    }

    /// <summary>Builds a canned ETR per-source call response: the same base shape as
    /// <see cref="BuildSourceJson"/> plus the two ETR-only typed keys, etrCompleteness and
    /// etrEligibility.</summary>
    private static string BuildEtrSourceJson(
        string sectionKind = "eligibility",
        string summary = "ETR structured summary text.",
        string completenessRating = "strong",
        string eligibilityCategory = "Specific Learning Disability") => $@"{{
          ""overallSummary"": ""{summary}"",
          ""sections"": [
            {{
              ""sectionKind"": ""{sectionKind}"",
              ""plainLanguageSummary"": ""Section summary"",
              ""keyPoints"": [""point""],
              ""redFlags"": [],
              ""legalReferences"": []
            }}
          ],
          ""etrCompleteness"": {{
            ""evaluatedDomains"": [
              {{ ""domain"": ""Cognitive"", ""toolsUsed"": [""WISC-V""], ""adequacyRating"": ""{completenessRating}"", ""notes"": ""x"" }}
            ],
            ""missingDomains"": [],
            ""overallCompletenessRating"": ""{completenessRating}""
          }},
          ""etrEligibility"": {{
            ""statedCategory"": ""{eligibilityCategory}"",
            ""statedConclusion"": ""qualifies"",
            ""dataSupportsConclusion"": true,
            ""supportingEvidence"": [],
            ""contradictingEvidence"": [],
            ""alternativeConsiderations"": []
          }},
          ""overallRedFlags"": []
        }}";

    private static string BuildSynthesisJson() => @"{
      ""overallSummary"": ""Combined summary."",
      ""crossDocSynthesis"": { ""summary"": ""combined"", ""timeline"": [""t1""], ""contradictions"": [], ""progression"": ""improving"" },
      ""overallRedFlags"": []
    }";

    // --- CreateRunAsync: unaffected by the per-source execution refactor (Claude is never called). ---

    [Fact]
    public async Task CreateRunAsync_WithZeroSources_Fails()
    {
        using var _fixture = new AnalysisRunTestFixture();
        using var context = _fixture.CreateContext();
        var service = BuildService(context, new FakeClaudeClient(null));

        var result = await service.CreateRunAsync(
            _fixture.ChildId, _fixture.OwnerUserId, new List<AnalysisRunSourceRef>(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, context.AnalysisRuns.Count());
    }

    [Fact]
    public async Task CreateRunAsync_WithOneValidSource_Succeeds()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        using var context = _fixture.CreateContext();
        var service = BuildService(context, new FakeClaudeClient(null));

        var result = await service.CreateRunAsync(
            _fixture.ChildId, _fixture.OwnerUserId,
            new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var run = context.AnalysisRuns.Find(result.Data!.Id);
        Assert.NotNull(run);
        Assert.Equal(AnalysisRunStatus.Pending, run!.Status);
        Assert.Equal(1, context.AnalysisRunSources.Count(s => s.AnalysisRunId == run.Id));
    }

    [Fact]
    public async Task CreateRunAsync_WithDuplicateSources_DedupesAndWarns()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        using var context = _fixture.CreateContext();
        var service = BuildService(context, new FakeClaudeClient(null));

        var result = await service.CreateRunAsync(
            _fixture.ChildId, _fixture.OwnerUserId,
            new List<AnalysisRunSourceRef>
            {
                new(AnalysisSourceType.IepDocument, iepId),
                new(AnalysisSourceType.IepDocument, iepId),
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Message)); // warning present
        Assert.Equal(1, context.AnalysisRunSources.Count(s => s.AnalysisRunId == result.Data!.Id));
    }

    [Fact]
    public async Task CreateRunAsync_ZeroValidSources_RefundsReservedUnit()
    {
        using var _fixture = new AnalysisRunTestFixture();
        // Note: no document seeded — referencing id 9999 yields a snapshot of null (missing/unparsed),
        // so all sources are dropped and the run fails after reservation.

        int usageBefore;
        using (var preContext = _fixture.CreateContext())
        {
            usageBefore = preContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        using (var context = _fixture.CreateContext())
        {
            var service = BuildService(context, new FakeClaudeClient(null));
            var result = await service.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, 9999) },
                CancellationToken.None);

            Assert.False(result.Success);
        }

        using var verifyContext = _fixture.CreateContext();
        Assert.Equal(0, verifyContext.AnalysisRuns.Count());
        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter); // reservation refunded, no net usage
    }

    [Fact]
    public async Task CreateRunAsync_EtrOnlySource_ConsumesOneAnalysisUnit_AndSixthIsRefusedForNonAdmin()
    {
        // ETR sources are metered exactly like IEP sources: BuildSourceSnapshotAsync and the usage
        // reservation in CreateRunAsync do not branch on source type at all — this is a regression
        // test proving that stays true now that the legacy, unmetered ETR analyze endpoint is gone.
        using var _fixture = new AnalysisRunTestFixture();
        var etrId = _fixture.SeedEtrDocument();

        using var context = _fixture.CreateContext();
        var service = BuildService(context, new FakeClaudeClient(null));

        for (var i = 0; i < 5; i++)
        {
            var result = await service.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.EtrDocument, etrId) },
                CancellationToken.None);
            Assert.True(result.Success);
        }

        Assert.Equal(5, context.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis"));
        Assert.Equal(5, context.AnalysisRuns.Count());

        // The fixture's seeded owner is a Parent (User.Role's default) — not exempt from the per-child cap.
        var sixth = await service.CreateRunAsync(
            _fixture.ChildId, _fixture.OwnerUserId,
            new List<AnalysisRunSourceRef> { new(AnalysisSourceType.EtrDocument, etrId) },
            CancellationToken.None);

        Assert.False(sixth.Success);
        Assert.Equal(5, context.AnalysisRuns.Count());
        Assert.Equal(5, context.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis"));
    }

    // --- ExecuteRunAsync: one Claude call per source, then one synthesis call when 2+ complete. ---

    [Fact]
    public async Task ExecuteRunAsync_SingleSource_MakesOneCallAndSkipsSynthesis()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        var client = new ScriptedClaudeClient(BuildSourceJson());
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        // Exactly one call: the source call. No synthesis call for a single completed source.
        Assert.Equal(1, client.CallCount);

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status);
        Assert.Null(run.CrossDocSynthesis);
        Assert.Equal("Structured summary text.", run.OverallSummary);
    }

    [Fact]
    public async Task ExecuteRunAsync_ThreeSources_MakesThreeSourceCallsPlusOneSynthesisCall()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId1 = _fixture.SeedIepDocument();
        var etrId = _fixture.SeedEtrDocument();
        var iepId2 = _fixture.SeedIepDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef>
                {
                    new(AnalysisSourceType.IepDocument, iepId1),
                    new(AnalysisSourceType.EtrDocument, etrId),
                    new(AnalysisSourceType.IepDocument, iepId2),
                },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        var client = new ScriptedClaudeClient(
            BuildSourceJson("present_levels"),
            BuildSourceJson("eligibility"),
            BuildSourceJson("services"),
            BuildSynthesisJson());

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        Assert.Equal(4, client.CallCount); // 3 source calls + 1 synthesis call

        // The synthesis call must be fed each source's STRUCTURED output, never the raw documents
        // (docs/designs/2026-10-02-unified-analysis-design.md): it should carry the canned
        // "overallSummary" text each source call returned, but never the raw seeded IEP goal text.
        var synthesisRequest = client.Requests[^1];
        Assert.Contains("Structured summary text.", synthesisRequest.UserText);
        Assert.DoesNotContain("Student will improve reading fluency", synthesisRequest.UserText);

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status);
        Assert.NotNull(run.CrossDocSynthesis);
        Assert.Null(run.ErrorMessage);
        Assert.Equal(3, verifyContext.AnalysisRunSources.Count(s =>
            s.AnalysisRunId == runId && s.Status == AnalysisRunSourceStatus.Completed));
        Assert.Equal(3, verifyContext.AnalysisRunSections.Count(s => s.AnalysisRunId == runId));
    }

    [Fact]
    public async Task ExecuteRunAsync_OneOfTwoSourcesFails_RunCompletesWithThatSourceErrored_UsageNotRefunded()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var etrId = _fixture.SeedEtrDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef>
                {
                    new(AnalysisSourceType.IepDocument, iepId),
                    new(AnalysisSourceType.EtrDocument, etrId),
                },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        // First source (lower id — the IEP, added first) succeeds; the second (the ETR) throws.
        var client = new ScriptedClaudeClient(
            () => BuildSourceJson("present_levels"),
            () => throw new ClaudeApiException(ClaudeFailureKind.Transient));

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        Assert.Equal(2, client.CallCount); // two source calls; no synthesis (only 1 completed)

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status);
        Assert.NotNull(run.UsageRecordId); // not refunded — the run still completed

        var sources = verifyContext.AnalysisRunSources
            .Where(s => s.AnalysisRunId == runId).OrderBy(s => s.Id).ToList();
        Assert.Equal(AnalysisRunSourceStatus.Completed, sources[0].Status);
        Assert.Null(sources[0].ErrorMessage);
        Assert.Equal(AnalysisRunSourceStatus.Error, sources[1].Status);
        Assert.Equal(ClaudeFailureMessages.Transient, sources[1].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteRunAsync_AllSourcesFail_RunErrorsAndRefunds()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var etrId = _fixture.SeedEtrDocument();

        int usageBefore;
        using (var preContext = _fixture.CreateContext())
        {
            usageBefore = preContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef>
                {
                    new(AnalysisSourceType.IepDocument, iepId),
                    new(AnalysisSourceType.EtrDocument, etrId),
                },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        // One throws, one returns an unparseable (null) response — both count as a source failure.
        var client = new ScriptedClaudeClient(
            () => throw new ClaudeApiException(ClaudeFailureKind.Transient),
            () => null);

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Null(run.UsageRecordId); // refunded — every source failed

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter);

        var sources = verifyContext.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).ToList();
        Assert.All(sources, s => Assert.Equal(AnalysisRunSourceStatus.Error, s.Status));
    }

    [Fact]
    public async Task ExecuteRunAsync_NullClaudeResponse_SingleSource_ErrorsAndRefunds()
    {
        // Phase 1 behavior change from the pre-refactor single-call engine: "every source failed"
        // now ALWAYS refunds (accepted decision), regardless of failure kind — including
        // InvalidResponse. The old single-call engine deliberately did NOT refund InvalidResponse
        // (todos/P2-02), to stop one always-unparseable call from being retried for free; that
        // carve-out no longer applies once a run can contain several independently-failing sources.
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int usageBefore;
        using (var preContext = _fixture.CreateContext())
        {
            usageBefore = preContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(null)); // null => parse failure
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        // Single-source run: the run's ErrorMessage is that one source's own specific failure reason.
        Assert.Equal(ClaudeFailureMessages.InvalidResponse, run.ErrorMessage);
        Assert.Null(run.UsageRecordId);

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter);

        var source = verifyContext.AnalysisRunSources.Single(s => s.AnalysisRunId == runId);
        Assert.Equal(AnalysisRunSourceStatus.Error, source.Status);
        Assert.Equal(ClaudeFailureMessages.InvalidResponse, source.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteRunAsync_SynthesisFails_CompletesWithDeterministicMerge_NoRefund()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var etrId = _fixture.SeedEtrDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef>
                {
                    new(AnalysisSourceType.IepDocument, iepId),
                    new(AnalysisSourceType.EtrDocument, etrId),
                },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        var client = new ScriptedClaudeClient(
            () => BuildSourceJson("present_levels", summary: "IEP summary."),
            () => BuildSourceJson("eligibility", summary: "ETR summary."),
            () => throw new ClaudeApiException(ClaudeFailureKind.Transient)); // synthesis call fails

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        Assert.Equal(3, client.CallCount); // 2 source calls + 1 (failed) synthesis attempt

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status); // the sources themselves succeeded
        Assert.Null(run.CrossDocSynthesis);
        Assert.Contains("IEP summary.", run.OverallSummary);
        Assert.Contains("ETR summary.", run.OverallSummary);
        Assert.NotNull(run.ErrorMessage); // notes that the combined synthesis was skipped
        Assert.NotNull(run.UsageRecordId); // NOT refunded — both sources were genuinely billed
    }

    [Fact]
    public async Task ExecuteRunAsync_UnknownGoalId_IsDroppedFromIepGoalsSection()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int validGoalId;
        using (var context = _fixture.CreateContext())
        {
            validGoalId = context.Goals.Single(g => g.IepSection.IepDocumentId == iepId).Id;
        }

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        var unknownGoalId = validGoalId + 9999;
        var json = BuildSourceJson(goals: [(validGoalId, "green"), (unknownGoalId, "red")]);

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(json));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var goalsSection = verifyContext.AnalysisRunSections
            .Single(s => s.AnalysisRunId == runId && s.SectionKind == AnalysisRunSectionKinds.IepGoals);

        var payload = System.Text.Json.JsonSerializer.Deserialize<IepGoalsSectionPayload>(
            goalsSection.Analysis!, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(payload);
        Assert.Single(payload!.GoalAnalyses);
        Assert.Equal(validGoalId, payload.GoalAnalyses[0].GoalId);
    }

    [Fact]
    public async Task GetRunAsync_IepGoalsSection_RoundTripsThroughModel()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int validGoalId;
        using (var context = _fixture.CreateContext())
        {
            validGoalId = context.Goals.Single(g => g.IepSection.IepDocumentId == iepId).Id;
        }

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        var json = BuildSourceJson("present_levels", goals: [(validGoalId, "yellow")]);
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(json));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetRunAsync(runId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);

        var goalsSection = result.Data!.Sections.Single(s => s.SectionKind == AnalysisRunSectionKinds.IepGoals);
        Assert.Null(goalsSection.Analysis); // the ordinary payload stays null for this kind
        Assert.NotNull(goalsSection.GoalAnalyses);
        var goal = Assert.Single(goalsSection.GoalAnalyses!);
        Assert.Equal(validGoalId, goal.GoalId);
        Assert.Equal("yellow", goal.OverallRating);

        // An ordinary section keeps the Analysis shape, not GoalAnalyses.
        var ordinarySection = result.Data!.Sections.Single(s => s.SectionKind == "present_levels");
        Assert.NotNull(ordinarySection.Analysis);
        Assert.Null(ordinarySection.GoalAnalyses);

        // The source model surfaces its terminal status alongside the sections.
        var sourceModel = Assert.Single(result.Data!.Sources);
        Assert.Equal(nameof(AnalysisRunSourceStatus.Completed), sourceModel.Status);
        Assert.Null(sourceModel.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteRunAsync_EtrSource_PersistsCompletenessAndEligibilitySections_RoundTripsThroughGetRunAsync()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var etrId = _fixture.SeedEtrDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.EtrDocument, etrId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(BuildEtrSourceJson()));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetRunAsync(runId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        var run = result.Data!;

        var completenessSection = run.Sections.Single(s => s.SectionKind == AnalysisRunSectionKinds.EtrCompleteness);
        Assert.Null(completenessSection.Analysis); // the ordinary payload stays null for this kind
        Assert.NotNull(completenessSection.EtrCompleteness);
        Assert.Equal("strong", completenessSection.EtrCompleteness!.OverallCompletenessRating);
        Assert.Single(completenessSection.EtrCompleteness.EvaluatedDomains);

        var eligibilitySection = run.Sections.Single(s => s.SectionKind == AnalysisRunSectionKinds.EtrEligibility);
        Assert.Null(eligibilitySection.Analysis);
        Assert.NotNull(eligibilitySection.EtrEligibility);
        Assert.Equal("Specific Learning Disability", eligibilitySection.EtrEligibility!.StatedCategory);
        Assert.True(eligibilitySection.EtrEligibility.DataSupportsConclusion);

        // An ordinary section keeps the Analysis shape, not either ETR typed payload.
        var ordinarySection = run.Sections.Single(s => s.SectionKind == "eligibility");
        Assert.NotNull(ordinarySection.Analysis);
        Assert.Null(ordinarySection.EtrCompleteness);
        Assert.Null(ordinarySection.EtrEligibility);

        using var verifyContext = _fixture.CreateContext();
        Assert.Equal(AnalysisRunStatus.Completed, verifyContext.AnalysisRuns.Find(runId)!.Status);
    }

    [Fact]
    public async Task FailRunAsync_OnCompletedRun_IsNoOp()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        // Complete the run successfully.
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(BuildSourceJson()));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        int usageBefore;
        using (var midContext = _fixture.CreateContext())
        {
            Assert.Equal(AnalysisRunStatus.Completed, midContext.AnalysisRuns.Find(runId)!.Status);
            usageBefore = midContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        // FailRunAsync on an already-Completed run must be a no-op.
        using (var failContext = _fixture.CreateContext())
        {
            var failService = BuildService(failContext, new FakeClaudeClient(null));
            await failService.FailRunAsync(runId, "should be ignored", ct: CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status); // status unchanged
        Assert.Null(run.ErrorMessage);

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter); // no negative usage / no extra refund
    }

    [Fact]
    public async Task ExecuteRunAsync_RefundIsRunScoped_OnlyFailedRunsUnitReleased()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        // Reserve TWO runs for the SAME child (counts as 2 quota units).
        int firstRunId, secondRunId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));

            var first = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            Assert.True(first.Success);
            firstRunId = first.Data!.Id;

            var second = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            Assert.True(second.Success);
            secondRunId = second.Data!.Id;
        }

        int secondRunUsageId;
        using (var midContext = _fixture.CreateContext())
        {
            Assert.Equal(2, midContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis"));

            // Capture the second run's reserved usage id — it must survive the first run's failure.
            secondRunUsageId = midContext.AnalysisRuns.Find(secondRunId)!.UsageRecordId!.Value;
        }

        // Fail ONLY the first run (single source, so this is also an "all sources failed" run).
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ThrowingClaudeClient(ClaudeFailureKind.Configuration));
            await execService.ExecuteRunAsync(firstRunId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();

        var firstRun = verifyContext.AnalysisRuns.Find(firstRunId)!;
        Assert.Equal(AnalysisRunStatus.Error, firstRun.Status);
        Assert.Null(firstRun.UsageRecordId); // cleared after refund

        // Exactly one unit remains, and it is the SECOND run's reservation (run-scoped correctness).
        Assert.Equal(1, verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis"));
        Assert.NotNull(verifyContext.UsageRecords.Find(secondRunUsageId));

        var secondRun = verifyContext.AnalysisRuns.Find(secondRunId)!;
        Assert.Equal(secondRunUsageId, secondRun.UsageRecordId); // second reservation intact
    }

    [Fact]
    public async Task ExecuteRunAsync_ClaudeApiException_FailsRunAndRefundsExactlyOnce()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int usageBefore;
        using (var preContext = _fixture.CreateContext())
        {
            usageBefore = preContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        int runId;
        int reservedUsageRecordId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            Assert.True(created.Success);
            runId = created.Data!.Id;

            // Capture the exact reserved row so the refund can be verified by id, not by count.
            reservedUsageRecordId = createContext.AnalysisRuns.Find(runId)!.UsageRecordId!.Value;
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ThrowingClaudeClient(ClaudeFailureKind.Configuration));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;

        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Equal(ClaudeFailureMessages.Configuration, run.ErrorMessage);

        // The reservation must be released, not merely detached: verify the actual usage row is
        // gone and the pointer cleared so no later fail path can double-refund.
        Assert.Null(run.UsageRecordId);
        Assert.Null(verifyContext.UsageRecords.Find(reservedUsageRecordId));

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter);
    }

    [Fact]
    public async Task ExecuteRunAsync_ClaudeApiException_DoesNotLeakInternalDetailIntoErrorMessage()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ThrowingClaudeClient(ClaudeFailureKind.RateLimited));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;

        // The persisted message is the canned constant verbatim — never the exception's own text,
        // which for a real failure carries the model id and Anthropic request id.
        Assert.Equal(ClaudeFailureMessages.RateLimited, run.ErrorMessage);
        Assert.DoesNotContain("Claude call failed", run.ErrorMessage!);
    }

    [Fact]
    public async Task ExecuteRunAsync_CancelledDuringClaudeCall_StillRefundsQuotaUnit()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int usageBefore;
        using (var preContext = _fixture.CreateContext())
        {
            usageBefore = preContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        int runId;
        int reservedUsageRecordId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
            reservedUsageRecordId = createContext.AnalysisRuns.Find(runId)!.UsageRecordId!.Value;
        }

        // Shutdown arrives while the Claude call is in flight. FailRunAsync must run on
        // CancellationToken.None — handing it the cancelled ct makes the refund's SaveChangesAsync
        // throw, and the unit is leaked with no path left to reclaim it.
        using var cts = new CancellationTokenSource();
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new CancellingClaudeClient(cts));
            await execService.ExecuteRunAsync(runId, cts.Token);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Null(run.UsageRecordId);
        Assert.Null(verifyContext.UsageRecords.Find(reservedUsageRecordId));

        // Shutdown is neither a timeout nor an unexpected error — both would be lies written onto
        // an in-flight run by every deploy restart.
        Assert.Equal("Analysis was interrupted.", run.ErrorMessage);
        Assert.NotEqual(ClaudeFailureMessages.Timeout, run.ErrorMessage);
        Assert.NotEqual(ClaudeFailureMessages.Unknown, run.ErrorMessage);

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter);
    }

    [Fact]
    public async Task ExecuteRunAsync_WithPreCancelledToken_LeavesRunRecoverableByWorker()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int usageBefore;
        using (var preContext = _fixture.CreateContext())
        {
            usageBefore = preContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        }

        int runId;
        int reservedUsageRecordId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
            reservedUsageRecordId = createContext.AnalysisRuns.Find(runId)!.UsageRecordId!.Value;
        }

        // An already-cancelled token aborts on the very first query, before ExecuteRunAsync's try
        // block exists — so the cancellation propagates rather than being swallowed, and the run is
        // left untouched and still holding its reservation.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(null));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => execService.ExecuteRunAsync(runId, cts.Token));
        }

        using (var midContext = _fixture.CreateContext())
        {
            var stranded = midContext.AnalysisRuns.Find(runId)!;
            Assert.Equal(AnalysisRunStatus.Pending, stranded.Status);
            Assert.Equal(reservedUsageRecordId, stranded.UsageRecordId);
        }

        // AnalysisRunWorker's catch is what reclaims it, and it must succeed on a fresh scope with
        // an uncancelled token.
        using (var failContext = _fixture.CreateContext())
        {
            var failService = BuildService(failContext, new FakeClaudeClient(null));
            await failService.FailRunAsync(runId, "Analysis was interrupted.", ct: CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Null(run.UsageRecordId);
        Assert.Null(verifyContext.UsageRecords.Find(reservedUsageRecordId));

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter);
    }

    // --- FailStaleRunsAsync: the runtime sweep AnalysisRunWorker calls every 5 minutes. ---

    [Fact]
    public async Task FailStaleRunsAsync_FailsAndRefundsARunStuckRunningPastThreshold()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int staleRunId, freshRunId, staleUsageRecordId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));

            var stale = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            staleRunId = stale.Data!.Id;
            staleUsageRecordId = createContext.AnalysisRuns.Find(staleRunId)!.UsageRecordId!.Value;

            var fresh = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            freshRunId = fresh.Data!.Id;
        }

        // Simulate both runs having entered Running — one 31 minutes ago (stale), one 10 minutes ago
        // (not yet stale) — WITHOUT actually executing them. Raw SQL bypasses
        // ApplicationDbContext.SaveChangesAsync's auditing override, which would otherwise stamp
        // UpdatedAt back to "now" the moment either AnalysisRun entity is saved as Modified.
        using (var setupContext = _fixture.CreateContext())
        {
            await setupContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AnalysisRuns SET Status = 'Running', UpdatedAt = {DateTime.UtcNow.AddMinutes(-31)} WHERE Id = {staleRunId}");
            await setupContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AnalysisRuns SET Status = 'Running', UpdatedAt = {DateTime.UtcNow.AddMinutes(-10)} WHERE Id = {freshRunId}");
        }

        using (var sweepContext = _fixture.CreateContext())
        {
            var sweepService = BuildService(sweepContext, new FakeClaudeClient(null));
            await sweepService.FailStaleRunsAsync(TimeSpan.FromMinutes(30), CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();

        var staleAfter = verifyContext.AnalysisRuns.Find(staleRunId)!;
        Assert.Equal(AnalysisRunStatus.Error, staleAfter.Status);
        Assert.Null(staleAfter.UsageRecordId);
        Assert.Null(verifyContext.UsageRecords.Find(staleUsageRecordId));

        var freshAfter = verifyContext.AnalysisRuns.Find(freshRunId)!;
        Assert.Equal(AnalysisRunStatus.Running, freshAfter.Status); // untouched — not yet 30 minutes old
        Assert.NotNull(freshAfter.UsageRecordId);
    }

    // --- GetLatestForSourceAsync: the document-page "latest run including this document" read. ---

    private async Task<int> CreateAndExecuteSingleSourceRunAsync(
        AnalysisRunTestFixture fixture, int iepId, IReadOnlyList<(int GoalId, string Rating)>? goals = null)
    {
        int runId;
        using (var createContext = fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                fixture.ChildId, fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        using (var execContext = fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(BuildSourceJson(goals: goals)));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        return runId;
    }

    [Fact]
    public async Task GetLatestForSourceAsync_PicksNewestRunIncludingDocument()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        var olderRunId = await CreateAndExecuteSingleSourceRunAsync(_fixture, iepId);
        var newerRunId = await CreateAndExecuteSingleSourceRunAsync(_fixture, iepId);
        Assert.True(newerRunId > olderRunId);

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.IepDocument, iepId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(newerRunId, result.Data!.Id);
    }

    [Fact]
    public async Task GetLatestForSourceAsync_ReturnsOtherSources_ForMultiSourceRun()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var etrId = _fixture.SeedEtrDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef>
                {
                    new(AnalysisSourceType.IepDocument, iepId),
                    new(AnalysisSourceType.EtrDocument, etrId),
                },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(
                BuildSourceJson("present_levels"), BuildSourceJson("eligibility"), BuildSynthesisJson()));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.IepDocument, iepId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(runId, result.Data!.Id);
        var other = Assert.Single(result.Data!.OtherSources);
        Assert.Equal(nameof(AnalysisSourceType.EtrDocument), other.SourceType);
        Assert.Equal(etrId, other.SourceId);
        Assert.False(string.IsNullOrWhiteSpace(other.Label));
        Assert.False(result.Data!.Stale);
    }

    [Fact]
    public async Task GetLatestForSourceAsync_Stale_WhenGoalIdMissingFromCurrentGoals()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        int goalId;
        using (var context = _fixture.CreateContext())
        {
            goalId = context.Goals.Single(g => g.IepSection.IepDocumentId == iepId).Id;
        }

        var runId = await CreateAndExecuteSingleSourceRunAsync(_fixture, iepId, goals: [(goalId, "green")]);

        // The goal the run rated no longer exists on the document (e.g. a full re-parse).
        using (var mutate = _fixture.CreateContext())
        {
            mutate.Goals.Remove(mutate.Goals.Single(g => g.Id == goalId));
            mutate.SaveChanges();
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.IepDocument, iepId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(runId, result.Data!.Id);
        Assert.True(result.Data!.Stale);
    }

    [Fact]
    public async Task GetLatestForSourceAsync_Stale_WhenDocumentUpdatedAfterRun()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();

        await CreateAndExecuteSingleSourceRunAsync(_fixture, iepId);

        // The document was reprocessed after the run completed. Raw SQL bypasses the auditing
        // override (which would otherwise stamp UpdatedAt back to "now" on an ordinary EF update).
        using (var mutate = _fixture.CreateContext())
        {
            await mutate.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IepDocuments SET UpdatedAt = {DateTime.UtcNow.AddMinutes(5)} WHERE Id = {iepId}");
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.IepDocument, iepId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.Data!.Stale);
    }

    [Fact]
    public async Task GetLatestForSourceAsync_EtrSource_IncludesCompletenessAndEligibilitySections()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var etrId = _fixture.SeedEtrDocument();

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.EtrDocument, etrId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(BuildEtrSourceJson()));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.EtrDocument, etrId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(runId, result.Data!.Id);
        Assert.False(result.Data!.Stale);

        var completenessSection = result.Data!.Sections.Single(s => s.SectionKind == AnalysisRunSectionKinds.EtrCompleteness);
        Assert.Equal("strong", completenessSection.EtrCompleteness!.OverallCompletenessRating);

        var eligibilitySection = result.Data!.Sections.Single(s => s.SectionKind == AnalysisRunSectionKinds.EtrEligibility);
        Assert.Equal("Specific Learning Disability", eligibilitySection.EtrEligibility!.StatedCategory);
    }

    [Fact]
    public async Task GetLatestForSourceAsync_DoesNotReturnRun_ForDocumentOfAnotherChild()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        await CreateAndExecuteSingleSourceRunAsync(_fixture, iepId);

        int otherChildId;
        using (var context = _fixture.CreateContext())
        {
            var otherChild = new ChildProfile { UserId = _fixture.OwnerUserId, FirstName = "Other", LastName = "Child", IsActive = true };
            context.ChildProfiles.Add(otherChild);
            context.SaveChanges();
            otherChildId = otherChild.Id;

            // The same user genuinely owns otherChildId too — access alone must not be enough to read
            // the first child's document; this exercises the id-belongs-to-child check specifically,
            // not merely the access check.
            context.ChildAccesses.Add(new ChildAccess
            {
                ChildProfileId = otherChildId, UserId = _fixture.OwnerUserId, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        // The document genuinely belongs to _fixture.ChildId; a request naming a DIFFERENT child
        // (otherChildId) for the same document id must not leak the run.
        var result = await readService.GetLatestForSourceAsync(
            otherChildId, AnalysisSourceType.IepDocument, iepId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.False(result.Success);
    }
}
