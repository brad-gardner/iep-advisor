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

    [Fact]
    public async Task CreateRunAsync_WithMoreThanFiveSources_Fails_NoUsageReserved()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepIds = Enumerable.Range(0, 6).Select(_ => _fixture.SeedIepDocument()).ToList();

        using var context = _fixture.CreateContext();
        var service = BuildService(context, new FakeClaudeClient(null));

        var result = await service.CreateRunAsync(
            _fixture.ChildId, _fixture.OwnerUserId,
            iepIds.Select(id => new AnalysisRunSourceRef(AnalysisSourceType.IepDocument, id)).ToList(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("Choose up to 5 documents for one analysis.", result.Message);
        Assert.Equal(0, context.AnalysisRuns.Count());
        Assert.Equal(0, context.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis"));
    }

    [Fact]
    public async Task CreateRunAsync_SixSourcesThatDedupeToFive_Succeeds()
    {
        // The cap is checked AFTER deduping identical (type, id) references, not before.
        using var _fixture = new AnalysisRunTestFixture();
        var iepIds = Enumerable.Range(0, 5).Select(_ => _fixture.SeedIepDocument()).ToList();

        using var context = _fixture.CreateContext();
        var service = BuildService(context, new FakeClaudeClient(null));

        var sources = iepIds.Select(id => new AnalysisRunSourceRef(AnalysisSourceType.IepDocument, id)).ToList();
        sources.Add(sources[0]); // duplicate of the first — dedupes back down to 5

        var result = await service.CreateRunAsync(_fixture.ChildId, _fixture.OwnerUserId, sources, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(5, context.AnalysisRunSources.Count(s => s.AnalysisRunId == result.Data!.Id));
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
    public async Task ExecuteRunAsync_AllSourcesFail_MixedFailureKinds_AnyInvalidResponse_RunErrorsNoRefund()
    {
        // Refund rule (review pass 2, item 2 — widened from the original "every failure was
        // InvalidResponse" carve-out): a MIXED all-failed run with even ONE InvalidResponse failure
        // (alongside a real provider/transient failure here) must NOT refund — only a run where NO
        // failure was InvalidResponse refunds (see the all-transient test below).
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

        // One throws a real provider failure, one returns an unparseable (null) response.
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
        Assert.NotNull(run.UsageRecordId); // NOT refunded — one failure was InvalidResponse

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore + 1, usageAfter); // consumed, not refunded

        var sources = verifyContext.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).ToList();
        Assert.All(sources, s => Assert.Equal(AnalysisRunSourceStatus.Error, s.Status));
    }

    [Fact]
    public async Task ExecuteRunAsync_AllSourcesFail_AllTransient_MultiSource_RunErrorsAndRefunds()
    {
        // Pure provider/transient failures (no InvalidResponse anywhere) must still refund — the refund
        // rule only withholds the unit when at least one failure was InvalidResponse (see the mixed-kind
        // test above, which now expects NO refund because it includes one).
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
            () => throw new ClaudeApiException(ClaudeFailureKind.Transient),
            () => throw new ClaudeApiException(ClaudeFailureKind.RateLimited));

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Null(run.UsageRecordId); // refunded — no InvalidResponse at all
    }

    [Fact]
    public async Task ExecuteRunAsync_NullClaudeResponse_SingleSource_ErrorsWithoutRefund()
    {
        // Restored carve-out (item D, todos/P2-02's original rationale): when EVERY source failed and
        // EVERY failure was specifically an unparseable Claude response, the call was still genuinely
        // billed and a document engineered to always produce unparseable JSON must not become a free
        // retry loop — refundQuota: false. A MIXED or all-transient all-failure still refunds (see the
        // tests above).
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
        Assert.NotNull(run.UsageRecordId); // NOT refunded — every failure was InvalidResponse

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore + 1, usageAfter); // consumed, not refunded

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
    public async Task ExecuteRunAsync_NullListsInClaudeResponse_AreNormalizedToEmpty_NoNullReferenceException()
    {
        // Claude is free to return an explicit JSON null for an array field instead of omitting it or
        // returning [] — System.Text.Json overwrites the C# default [] initializer with null in that
        // case. Unnormalized, BuildSynthesisUserText dereferences response.Sections.Count for every
        // completed source feeding the synthesis call, which would NRE and take the whole run down.
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

        const string sourceWithNulls = """
            {
              "overallSummary": "Summary.",
              "sections": null,
              "goalAnalyses": null,
              "overallRedFlags": null
            }
            """;
        const string synthesisWithNulls = """
            {
              "overallSummary": "Combined.",
              "crossDocSynthesis": null,
              "overallRedFlags": null
            }
            """;

        var client = new ScriptedClaudeClient(sourceWithNulls, sourceWithNulls, synthesisWithNulls);

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None); // must not throw
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status);
        Assert.Equal("[]", run.OverallRedFlags); // normalized, not the literal string "null"
    }

    [Fact]
    public async Task ExecuteRunAsync_ReservedOrEmptySectionKind_IsSanitized()
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

        // An ordinary section whose sectionKind collides with a reserved, structurally distinct typed
        // kind this engine uses (iep_goals is an object, not an AnalysisRunSectionResult); one with
        // mixed case and punctuation that should just be sanitized, not forced to "other"; and one
        // that is empty after sanitization.
        const string response = """
            {
              "overallSummary": "Summary.",
              "sections": [
                { "sectionKind": "IEP_GOALS", "plainLanguageSummary": "x", "keyPoints": [], "redFlags": [], "legalReferences": [] },
                { "sectionKind": "Weird Kind!! With Spaces", "plainLanguageSummary": "y", "keyPoints": [], "redFlags": [], "legalReferences": [] },
                { "sectionKind": "", "plainLanguageSummary": "z", "keyPoints": [], "redFlags": [], "legalReferences": [] }
              ],
              "overallRedFlags": []
            }
            """;

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new ScriptedClaudeClient(response));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var sections = verifyContext.AnalysisRunSections
            .Where(s => s.AnalysisRunId == runId)
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        Assert.Equal(3, sections.Count);
        Assert.Equal("other", sections[0].SectionKind); // collided with the reserved iep_goals kind
        Assert.Equal("weirdkindwithspaces", sections[1].SectionKind); // sanitized, not forced to "other"
        Assert.Equal("other", sections[2].SectionKind); // empty after sanitization
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
    public async Task GetRunAsync_MultiSourceRun_SectionsAreCorrectlyAttributedPerSource_NoCrossContamination()
    {
        // Review pass 2, item 1: GetRunAsync now loads Sources and Sections in two SEPARATE queries
        // (rather than projecting both sibling collections in one query, which EF Core joins into a
        // cartesian product without QuerySplittingBehavior configured). This is a safety net for that
        // split: with 2 sources and one section per source, a mistake in the refactor (e.g. querying
        // sections for the wrong run, or losing the AnalysisRunSourceId linkage) would show up here as
        // the wrong count or a section attributed to the wrong source.
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
            BuildSourceJson("present_levels", summary: "IEP summary."),
            BuildSourceJson("eligibility", summary: "ETR summary."),
            BuildSynthesisJson());

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var readContext = _fixture.CreateContext();
        var readService = BuildService(readContext, new FakeClaudeClient(null));
        var result = await readService.GetRunAsync(runId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(result.Success);
        var run = result.Data!;

        Assert.Equal(2, run.Sources.Count);
        Assert.Equal(2, run.Sections.Count); // exactly one section per source — no cartesian duplication

        var iepSourceId = run.Sources.Single(s => s.SourceType == nameof(AnalysisSourceType.IepDocument)).Id;
        var etrSourceId = run.Sources.Single(s => s.SourceType == nameof(AnalysisSourceType.EtrDocument)).Id;

        var iepSection = run.Sections.Single(s => s.SectionKind == "present_levels");
        Assert.Equal(iepSourceId, iepSection.AnalysisRunSourceId);

        var etrSection = run.Sections.Single(s => s.SectionKind == "eligibility");
        Assert.Equal(etrSourceId, etrSection.AnalysisRunSourceId);
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

    [Fact]
    public async Task FailRunAsync_WithUpdatedAtCutoff_LeavesARunAloneThatHeartbeatedAfterTheCutoffWasComputed()
    {
        // FailStaleRunsAsync computes its cutoff and SELECTs stale run ids first, then calls
        // FailRunAsync once per id. If a run heartbeats (makes genuine progress, UpdatedAt moving past
        // the cutoff) in the window between that SELECT and its own turn in the loop, FailRunAsync's
        // conditional update must re-check the SAME cutoff — not just Status == Running — so the run is
        // left alone instead of being wrongly failed and refunded. This exercises that re-check directly
        // via the updatedAtCutoff parameter, simulating exactly the race FailStaleRunsAsync is exposed to.
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

        var cutoff = DateTime.UtcNow.AddMinutes(-30);

        using (var setupContext = _fixture.CreateContext())
        {
            // The sweep's SELECT would have seen this run as stale (UpdatedAt before cutoff)...
            await setupContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AnalysisRuns SET Status = 'Running', UpdatedAt = {cutoff.AddMinutes(-1)} WHERE Id = {runId}");
        }

        using (var heartbeatContext = _fixture.CreateContext())
        {
            // ...but the run heartbeated (made progress) between that SELECT and this call.
            await heartbeatContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AnalysisRuns SET UpdatedAt = {DateTime.UtcNow} WHERE Id = {runId}");
        }

        using (var failContext = _fixture.CreateContext())
        {
            var failService = BuildService(failContext, new FakeClaudeClient(null));
            await failService.FailRunAsync(
                runId, "stale", refundQuota: true, ct: CancellationToken.None, updatedAtCutoff: cutoff);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Running, run.Status); // left alone — no longer actually stale
        Assert.NotNull(run.UsageRecordId); // not refunded
    }

    [Fact]
    public async Task FailRunAsync_WithUpdatedAtCutoff_StillFailsARunThatIsGenuinelyStillStale()
    {
        // The mirror image of the test above: when the run's UpdatedAt is still at or before the
        // cutoff (no heartbeat landed in between), FailRunAsync must proceed exactly as it did before
        // updatedAtCutoff existed.
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

        var cutoff = DateTime.UtcNow.AddMinutes(-30);

        using (var setupContext = _fixture.CreateContext())
        {
            await setupContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AnalysisRuns SET Status = 'Running', UpdatedAt = {cutoff.AddMinutes(-1)} WHERE Id = {runId}");
        }

        using (var failContext = _fixture.CreateContext())
        {
            var failService = BuildService(failContext, new FakeClaudeClient(null));
            await failService.FailRunAsync(
                runId, "stale", refundQuota: true, ct: CancellationToken.None, updatedAtCutoff: cutoff);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Null(run.UsageRecordId); // refunded — genuinely still stale
    }

    [Fact]
    public async Task ExecuteRunAsync_Heartbeat_RefreshesUpdatedAtBeforeEachSourceCallAndBeforeSynthesis()
    {
        // Each Claude call is capped well under the sweep's 30-minute stale threshold by HttpClient's
        // own timeout, but WITHOUT a heartbeat the run's UpdatedAt is only stamped once, on entering
        // Running — so a long multi-source run could still look stale to the sweep partway through.
        // This backdates UpdatedAt inside the FIRST source call's own responder (simulating 31 minutes
        // of "no progress" since the run entered Running) and proves the SECOND source call's own
        // heartbeat — stamped before that call is dispatched — had already refreshed it, and likewise
        // for the synthesis call.
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

        void AssertUpdatedAtIsFresh()
        {
            using var check = _fixture.CreateContext();
            var updatedAt = check.AnalysisRuns.AsNoTracking().Single(r => r.Id == runId).UpdatedAt;
            Assert.True(updatedAt > DateTime.UtcNow.AddMinutes(-1), "Expected the run's heartbeat to have refreshed UpdatedAt before this call.");
        }

        var client = new ScriptedClaudeClient(
            () =>
            {
                using var backdate = _fixture.CreateContext();
                backdate.Database.ExecuteSqlInterpolated(
                    $"UPDATE AnalysisRuns SET UpdatedAt = {DateTime.UtcNow.AddMinutes(-31)} WHERE Id = {runId}");
                return BuildSourceJson("present_levels", summary: "IEP summary.");
            },
            () =>
            {
                AssertUpdatedAtIsFresh(); // the second source call's own heartbeat already fired
                return BuildSourceJson("eligibility", summary: "ETR summary.");
            },
            () =>
            {
                AssertUpdatedAtIsFresh(); // the synthesis call's own heartbeat already fired
                return BuildSynthesisJson();
            });

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        Assert.Equal(3, client.CallCount);
        using var verifyContext = _fixture.CreateContext();
        Assert.Equal(AnalysisRunStatus.Completed, verifyContext.AnalysisRuns.Find(runId)!.Status);
    }

    [Fact]
    public async Task ExecuteRunAsync_SweepWinsMidRun_HeartbeatStopsFurtherProcessing_UntouchedSourceStaysError()
    {
        // The per-source heartbeat is now a CONDITIONAL update (Id == runId && Status == Running), not a
        // tracked save — so if the stale-run sweep fails (and refunds) this run WHILE the first of two
        // sources is being processed, the heartbeat check guarding the SECOND source must find the run
        // no longer Running and stop immediately: no Claude call for that second source, and its
        // Error status (set by the sweep's FailRunAsync, which marks every still-Pending/Running source
        // Error) must never be touched again by the executor.
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

        const string sweepMessage = "The analysis took too long to complete. Please try again.";

        // Exactly ONE responder is scripted: if the executor made a second Claude call (for the second
        // source, or a synthesis call), ScriptedClaudeClient would throw "no more scripted responses
        // were queued" and fail the test outright — CallCount alone wouldn't catch a call made after
        // this assertion runs, but the queue exhaustion would.
        var client = new ScriptedClaudeClient(() =>
        {
            using var sweepContext = _fixture.CreateContext();
            var sweepService = BuildService(sweepContext, new FakeClaudeClient(null));
            sweepService.FailRunAsync(runId, sweepMessage, refundQuota: true, ct: CancellationToken.None)
                .GetAwaiter().GetResult();
            return BuildSourceJson("present_levels");
        });

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        Assert.Equal(1, client.CallCount); // no further Claude calls after the sweep won

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Equal(sweepMessage, run.ErrorMessage);
        Assert.Null(run.UsageRecordId); // refunded by the sweep

        var sources = verifyContext.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).OrderBy(s => s.Id).ToList();
        // The second source was never reached by the executor once the heartbeat caught the race — it
        // stays exactly as the sweep's FailRunAsync left it.
        Assert.Equal(AnalysisRunSourceStatus.Error, sources[1].Status);
        Assert.Equal(sweepMessage, sources[1].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteRunAsync_SweepWinsTerminalRace_ExecutorsLateCompletedWriteIsDropped_NoDoubleRefund()
    {
        // Simulates the sweep failing (and refunding) a run WHILE the executor's single Claude call is
        // still "in flight" (a separate AnalysisRunService instance — its own DbContext — races in from
        // inside the scripted Claude responder, exactly as FailStaleRunsAsync would from a different
        // scope). When the executor's call "returns" and it tries to complete the run, it must lose:
        // the sweep's Error state and its single refund must be the only visible effect.
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

        const string sweepMessage = "The analysis took too long to complete. Please try again.";

        var client = new ScriptedClaudeClient(() =>
        {
            using var sweepContext = _fixture.CreateContext();
            var sweepService = BuildService(sweepContext, new FakeClaudeClient(null));
            sweepService.FailRunAsync(runId, sweepMessage, refundQuota: true, ct: CancellationToken.None)
                .GetAwaiter().GetResult();
            return BuildSourceJson();
        });

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, client);
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;

        // The sweep's Error transition wins; the executor's later Completed write must not overwrite it.
        Assert.Equal(AnalysisRunStatus.Error, run.Status);
        Assert.Equal(sweepMessage, run.ErrorMessage);
        Assert.Null(run.OverallSummary); // the executor's result was never persisted
        Assert.Null(run.UsageRecordId); // released exactly once, by the sweep

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(0, usageAfter); // refunded once — not left dangling, not double-refunded
    }

    [Fact]
    public async Task FailRunAsync_MarksEveryPendingOrRunningSourceAsError()
    {
        // A run can be failed (host shutdown, the sweep, an unexpected exception) while one of its
        // sources is still sitting at Pending/Running — that source must flip to Error in the same
        // transition, or a document page polling it would wait forever.
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

        // Simulate the run having made it partway through execution: one source Completed, the other
        // still Running — without actually running ExecuteRunAsync (raw SQL, like the stale-sweep test
        // above, so the auditing override does not get in the way).
        using (var setup = _fixture.CreateContext())
        {
            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AnalysisRuns SET Status = 'Running' WHERE Id = {runId}");
            var sources = setup.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).OrderBy(s => s.Id).ToList();
            sources[0].Status = AnalysisRunSourceStatus.Completed;
            sources[1].Status = AnalysisRunSourceStatus.Running;
            setup.SaveChanges();
        }

        using (var failContext = _fixture.CreateContext())
        {
            var failService = BuildService(failContext, new FakeClaudeClient(null));
            await failService.FailRunAsync(runId, "An unexpected error occurred during analysis.", refundQuota: true, ct: CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);

        var finalSources = verifyContext.AnalysisRunSources.Where(s => s.AnalysisRunId == runId).OrderBy(s => s.Id).ToList();
        Assert.Equal(AnalysisRunSourceStatus.Completed, finalSources[0].Status); // already-terminal source untouched
        Assert.Equal(AnalysisRunSourceStatus.Error, finalSources[1].Status); // still-Running source flipped to Error
        Assert.NotNull(finalSources[1].ErrorMessage);
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
    public async Task GetLatestForSourceAsync_MultiSourceRun_SectionsAreScopedToTheRequestedSourceOnly()
    {
        // Item I: the projection-based rewrite no longer Include(Sources).Include(Sections)'s the
        // whole run (which would carry every source's sections in a cartesian join with every source's
        // SourceContentSnapshot) — only the MATCHED source's own sections come back.
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
        var iepResult = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.IepDocument, iepId, _fixture.OwnerUserId, CancellationToken.None);
        var etrResult = await readService.GetLatestForSourceAsync(
            _fixture.ChildId, AnalysisSourceType.EtrDocument, etrId, _fixture.OwnerUserId, CancellationToken.None);

        Assert.True(iepResult.Success);
        Assert.True(etrResult.Success);

        var iepSourceId = iepResult.Data!.Sources.Single(s => s.SourceType == nameof(AnalysisSourceType.IepDocument)).Id;
        var etrSourceId = etrResult.Data!.Sources.Single(s => s.SourceType == nameof(AnalysisSourceType.EtrDocument)).Id;

        Assert.NotEmpty(iepResult.Data!.Sections);
        Assert.All(iepResult.Data!.Sections, s => Assert.Equal(iepSourceId, s.AnalysisRunSourceId));

        Assert.NotEmpty(etrResult.Data!.Sections);
        Assert.All(etrResult.Data!.Sections, s => Assert.Equal(etrSourceId, s.AnalysisRunSourceId));
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
