using System.Text.Json;
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

/// <summary>
/// Meeting prep Mode C (anchored to an ETR) now reads the latest COMPLETED analysis run that includes
/// the ETR, not the retired per-document EtrAnalysis row — equivalent info: that source's own ordinary
/// sections (summary/key points/red flags), the run's red flags only when the run is single-source, and
/// the source's own <c>etr_completeness</c> / <c>etr_eligibility</c> sections. Mirrors
/// <see cref="MeetingPrepModeATests"/> for the IEP path. The legacy read had NO status filter on the
/// EtrAnalysis row; reading only a COMPLETED run is an intentional tightening.
/// </summary>
public class MeetingPrepModeCTests
{
    private sealed class AllowAll : IAccessService
    {
        public Task<AccessRole?> GetRoleAsync(int childId, int userId, CancellationToken ct = default) =>
            Task.FromResult<AccessRole?>(AccessRole.Owner);

        public Task<bool> HasMinimumRoleAsync(int childId, int userId, AccessRole minimumRole, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private sealed class RecordingClaudeClient : IClaudeClient
    {
        private readonly string _response;
        public RecordingClaudeClient(string response) => _response = response;
        public List<ClaudeCompletionRequest> Requests { get; } = [];

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<string?>(_response);
        }
    }

    private const string CannedMeetingPrepResponse = """
        {
          "questionsToAsk": [{"text":"Which domains were evaluated?","context":"completeness","legalBasis":null}],
          "redFlagsToRaise": [{"text":"Missing adaptive testing","context":"child find","legalBasis":"34 CFR 300.304"}],
          "preparationNotes": [{"text":"Bring prior evaluations","context":"evidence","legalBasis":null}]
        }
        """;

    // GenerateChecklistAsync touches only access, subscription, the goal repo and the context — the
    // document repositories are unused on this path (the checklist's EtrDocument is loaded via Include).
    private static MeetingPrepService Service(ApplicationDbContext context, IClaudeClient claudeClient) =>
        new(
            null!,
            null!,
            new ParentAdvocacyGoalRepository(context),
            new AllowAll(),
            new SubscriptionService(context, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), NullLogger<SubscriptionService>.Instance),
            context,
            claudeClient,
            NullLogger<MeetingPrepService>.Instance);

    [Fact]
    public async Task GenerateChecklistAsync_ModeC_ReadsRunSections_CompletenessEligibilityAndRedFlags_IntoThePrompt()
    {
        using var fixture = new AnalysisRunTestFixture();
        var etrId = fixture.SeedEtrDocument();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            // A completed, single-source run including this ETR — its OverallRedFlags is promoted
            // from this document's own call, so it belongs in the prompt.
            var run = new AnalysisRun
            {
                ChildProfileId = fixture.ChildId,
                Status = AnalysisRunStatus.Completed,
                OverallRedFlags = """[{"severity":"red","title":"Missing adaptive domain","description":"Adaptive functioning was never assessed.","legalBasis":"34 CFR 300.304(c)(4)"}]"""
            };
            context.AnalysisRuns.Add(run);
            context.SaveChanges();

            var source = new AnalysisRunSource
            {
                AnalysisRunId = run.Id, SourceType = AnalysisSourceType.EtrDocument, SourceId = etrId,
                SourceLabel = "ETR", Status = AnalysisRunSourceStatus.Completed
            };
            context.AnalysisRunSources.Add(source);
            context.SaveChanges();

            context.AnalysisRunSections.AddRange(
                new AnalysisRunSection
                {
                    AnalysisRunId = run.Id, AnalysisRunSourceId = source.Id, SectionKind = "eligibility",
                    Analysis = """{"sectionKind":"eligibility","plainLanguageSummary":"The team found SLD eligibility.","keyPoints":["WISC-V administered"],"redFlags":[],"legalReferences":[]}""",
                    DisplayOrder = 0
                },
                new AnalysisRunSection
                {
                    AnalysisRunId = run.Id, AnalysisRunSourceId = source.Id, SectionKind = AnalysisRunSectionKinds.EtrCompleteness,
                    Analysis = JsonSerializer.Serialize(new EtrCompletenessSectionPayload
                    {
                        OverallCompletenessRating = "thin",
                        MissingDomains = [new EtrMissingDomain { Domain = "Adaptive behavior", Rationale = "Parent raised concerns about daily living skills." }]
                    }),
                    DisplayOrder = 1
                },
                new AnalysisRunSection
                {
                    AnalysisRunId = run.Id, AnalysisRunSourceId = source.Id, SectionKind = AnalysisRunSectionKinds.EtrEligibility,
                    Analysis = JsonSerializer.Serialize(new EtrEligibilitySectionPayload
                    {
                        StatedCategory = "Specific Learning Disability",
                        StatedConclusion = "qualifies",
                        DataSupportsConclusion = true
                    }),
                    DisplayOrder = 2
                });
            context.SaveChanges();

            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId, EtrDocumentId = etrId, Status = "pending",
                CreatedById = fixture.OwnerUserId, UpdatedById = fixture.OwnerUserId
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        var client = new RecordingClaudeClient(CannedMeetingPrepResponse);
        using (var execContext = fixture.CreateContext())
        {
            await Service(execContext, client).GenerateChecklistAsync(checklistId, CancellationToken.None);
        }

        var prompt = Assert.Single(client.Requests).UserText;

        // This source's own ordinary section content (summary + key points) is in the prompt.
        Assert.Contains("The team found SLD eligibility.", prompt);
        Assert.Contains("WISC-V administered", prompt);

        // The typed etr_completeness / etr_eligibility sections.
        Assert.Contains("\"overallCompletenessRating\":\"thin\"", prompt);
        Assert.Contains("Adaptive behavior", prompt);
        Assert.Contains("\"statedCategory\":\"Specific Learning Disability\"", prompt);

        // The run's red flags, promoted from this single-source run's own call.
        Assert.Contains("Missing adaptive domain", prompt);

        // Every analysis-derived block is wrapped in <etr_analysis> tags (item H), matching main's
        // pre-refactor behavior and the tag the system prompt's SECURITY sentence already names.
        Assert.Contains("<etr_analysis>", prompt);
        Assert.Contains("</etr_analysis>", prompt);

        using var verify = fixture.CreateContext();
        var checklist2 = verify.Set<MeetingPrepChecklist>().Single(c => c.Id == checklistId);
        Assert.Equal("completed", checklist2.Status);
    }

    [Fact]
    public async Task GenerateChecklistAsync_ModeC_NoCompletedRun_FallsBackToSectionsOnly()
    {
        using var fixture = new AnalysisRunTestFixture();
        var etrId = fixture.SeedEtrDocument();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId, EtrDocumentId = etrId, Status = "pending",
                CreatedById = fixture.OwnerUserId, UpdatedById = fixture.OwnerUserId
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        var client = new RecordingClaudeClient(CannedMeetingPrepResponse);
        using (var execContext = fixture.CreateContext())
        {
            await Service(execContext, client).GenerateChecklistAsync(checklistId, CancellationToken.None);
        }

        var prompt = Assert.Single(client.Requests).UserText;
        Assert.DoesNotContain("ETR ANALYSIS SUMMARY", prompt);
        Assert.DoesNotContain("ASSESSMENT COMPLETENESS", prompt);
        Assert.DoesNotContain("ELIGIBILITY REVIEW", prompt);

        using var verify = fixture.CreateContext();
        Assert.Equal("completed", verify.Set<MeetingPrepChecklist>().Single(c => c.Id == checklistId).Status);
    }

    [Fact]
    public async Task GenerateChecklistAsync_ModeC_RunRunning_FallsBackToSectionsOnly()
    {
        // A run can be Completed overall while THIS ETR's own source is still in flight (a sibling
        // source succeeded) — Mode C must not read an in-flight source's analysis.
        using var fixture = new AnalysisRunTestFixture();
        var etrId = fixture.SeedEtrDocument();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var run = new AnalysisRun { ChildProfileId = fixture.ChildId, Status = AnalysisRunStatus.Completed };
            context.AnalysisRuns.Add(run);
            context.SaveChanges();
            context.AnalysisRunSources.Add(new AnalysisRunSource
            {
                AnalysisRunId = run.Id, SourceType = AnalysisSourceType.EtrDocument, SourceId = etrId,
                SourceLabel = "ETR", Status = AnalysisRunSourceStatus.Running
            });
            context.SaveChanges();

            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId, EtrDocumentId = etrId, Status = "pending",
                CreatedById = fixture.OwnerUserId, UpdatedById = fixture.OwnerUserId
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        var client = new RecordingClaudeClient(CannedMeetingPrepResponse);
        using (var execContext = fixture.CreateContext())
        {
            await Service(execContext, client).GenerateChecklistAsync(checklistId, CancellationToken.None);
        }

        var prompt = Assert.Single(client.Requests).UserText;
        Assert.DoesNotContain("ETR ANALYSIS SUMMARY", prompt);
    }
}
