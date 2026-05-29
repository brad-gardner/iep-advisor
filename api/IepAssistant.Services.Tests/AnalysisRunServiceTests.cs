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

    // A hand-written fake — no Moq. Returns a canned response (or null).
    private sealed class FakeClaudeClient : IClaudeClient
    {
        private readonly string? _response;
        public FakeClaudeClient(string? response) => _response = response;
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);
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

    private static string BuildCannedJson(IReadOnlyList<(AnalysisSourceType type, int id)> sources, bool withSynthesis)
    {
        var sourceBlocks = string.Join(",", sources.Select(s => $@"
        {{
          ""sourceType"": ""{s.type}"",
          ""sourceId"": {s.id},
          ""sections"": [
            {{
              ""sectionKind"": ""annual_goals"",
              ""plainLanguageSummary"": ""Summary for {s.type} {s.id}"",
              ""keyPoints"": [""point a""],
              ""redFlags"": [],
              ""legalReferences"": []
            }}
          ]
        }}"));

        var synthesis = withSynthesis
            ? @",""crossDocSynthesis"": { ""summary"": ""combined"", ""timeline"": [""t1""], ""contradictions"": [], ""progression"": ""improving"" }"
            : "";

        return $@"{{
          ""overallSummary"": ""Overall summary."",
          ""sources"": [{sourceBlocks}]{synthesis},
          ""overallRedFlags"": []
        }}";
    }

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
    public async Task ExecuteRunAsync_MultiSource_CompletesWithSynthesis()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var etrId = _fixture.SeedEtrDocument();
        var sources = new List<(AnalysisSourceType, int)>
        {
            (AnalysisSourceType.IepDocument, iepId),
            (AnalysisSourceType.EtrDocument, etrId),
        };

        int runId;
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                sources.Select(s => new AnalysisRunSourceRef(s.Item1, s.Item2)).ToList(),
                CancellationToken.None);
            Assert.True(created.Success);
            runId = created.Data!.Id;
        }

        var json = BuildCannedJson(sources, withSynthesis: true);
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(json));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status);
        Assert.NotNull(run.CrossDocSynthesis);
        Assert.Equal(2, verifyContext.AnalysisRunSections.Count(s => s.AnalysisRunId == runId));
    }

    [Fact]
    public async Task ExecuteRunAsync_SingleSource_LeavesCrossDocSynthesisNull()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var sources = new List<(AnalysisSourceType, int)> { (AnalysisSourceType.IepDocument, iepId) };

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

        // Even if the model returns a synthesis block, single-source must drop it.
        var json = BuildCannedJson(sources, withSynthesis: true);
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(json));
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status);
        Assert.Null(run.CrossDocSynthesis);
    }

    [Fact]
    public async Task ExecuteRunAsync_NullClaudeResponse_ErrorsAndRefundsUsage()
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
        using (var createContext = _fixture.CreateContext())
        {
            var createService = BuildService(createContext, new FakeClaudeClient(null));
            var created = await createService.CreateRunAsync(
                _fixture.ChildId, _fixture.OwnerUserId,
                new List<AnalysisRunSourceRef> { new(AnalysisSourceType.IepDocument, iepId) },
                CancellationToken.None);
            runId = created.Data!.Id;
        }

        // Reservation happened at create — usage incremented by exactly one.
        using (var midContext = _fixture.CreateContext())
        {
            var usageMid = midContext.UsageRecords.Count(u =>
                u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
            Assert.Equal(usageBefore + 1, usageMid);
        }

        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(null)); // null => parse failure
            await execService.ExecuteRunAsync(runId, CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Error, run.Status);

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter); // refunded
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

        // Fail ONLY the first run (null Claude response => parse failure => refund).
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(null));
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
    public async Task FailRunAsync_OnCompletedRun_IsNoOp()
    {
        using var _fixture = new AnalysisRunTestFixture();
        var iepId = _fixture.SeedIepDocument();
        var sources = new List<(AnalysisSourceType, int)> { (AnalysisSourceType.IepDocument, iepId) };

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
        var json = BuildCannedJson(sources, withSynthesis: false);
        using (var execContext = _fixture.CreateContext())
        {
            var execService = BuildService(execContext, new FakeClaudeClient(json));
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
            await failService.FailRunAsync(runId, "should be ignored", CancellationToken.None);
        }

        using var verifyContext = _fixture.CreateContext();
        var run = verifyContext.AnalysisRuns.Find(runId)!;
        Assert.Equal(AnalysisRunStatus.Completed, run.Status); // status unchanged
        Assert.Null(run.ErrorMessage);

        var usageAfter = verifyContext.UsageRecords.Count(u =>
            u.UserId == _fixture.OwnerUserId && u.ChildProfileId == _fixture.ChildId && u.OperationType == "analysis");
        Assert.Equal(usageBefore, usageAfter); // no negative usage / no extra refund
    }
}
