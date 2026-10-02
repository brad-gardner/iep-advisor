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
/// Meeting prep Mode A (anchored to an IEP) now reads the latest COMPLETED analysis run that includes
/// the IEP, not the retired per-document IepAnalysis row — equivalent info: that source's own sections
/// (summary/key points/red flags), the run's red flags only when the run is single-source, and the
/// source's own <c>iep_goals</c> ratings.
/// </summary>
public class MeetingPrepModeATests
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
          "questionsToAsk": [{"text":"What baseline was used?","context":"reading","legalBasis":null}],
          "redFlagsToRaise": [{"text":"Vague goal","context":"measurability","legalBasis":null}],
          "preparationNotes": [{"text":"Bring the last report card","context":"evidence","legalBasis":null}]
        }
        """;

    // GenerateChecklistAsync touches only access, subscription, the goal repo and the context — the
    // document repositories are unused on this path (the checklist's IepDocument is loaded via Include).
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
    public async Task GenerateChecklistAsync_ModeA_ReadsRunGoalRatings_SectionsAndRedFlags_IntoThePrompt()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepId = fixture.SeedIepDocument();

        int goalId;
        using (var context = fixture.CreateContext())
        {
            goalId = context.Goals.Single(g => g.IepSection.IepDocumentId == iepId).Id;
        }

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            // A completed, single-source run including this IEP — its OverallRedFlags is promoted
            // from this document's own call, so it belongs in the prompt.
            var run = new AnalysisRun
            {
                ChildProfileId = fixture.ChildId,
                Status = AnalysisRunStatus.Completed,
                OverallRedFlags = """[{"severity":"red","title":"No measurable baseline","description":"The goal has no numeric starting point.","legalBasis":"34 CFR 300.320(a)(2)"}]"""
            };
            context.AnalysisRuns.Add(run);
            context.SaveChanges();

            var source = new AnalysisRunSource
            {
                AnalysisRunId = run.Id, SourceType = AnalysisSourceType.IepDocument, SourceId = iepId,
                SourceLabel = "IEP", Status = AnalysisRunSourceStatus.Completed
            };
            context.AnalysisRunSources.Add(source);
            context.SaveChanges();

            context.AnalysisRunSections.AddRange(
                new AnalysisRunSection
                {
                    AnalysisRunId = run.Id, AnalysisRunSourceId = source.Id, SectionKind = "present_levels",
                    Analysis = """{"sectionKind":"present_levels","plainLanguageSummary":"Reads below grade level.","keyPoints":["needs fluency support"],"redFlags":[],"legalReferences":[]}""",
                    DisplayOrder = 0
                },
                new AnalysisRunSection
                {
                    AnalysisRunId = run.Id, AnalysisRunSourceId = source.Id, SectionKind = AnalysisRunSectionKinds.IepGoals,
                    Analysis = JsonSerializer.Serialize(new IepGoalsSectionPayload
                    {
                        GoalAnalyses =
                        [
                            new GoalAnalysisResult
                            {
                                GoalId = goalId,
                                GoalText = "Read 100 wpm with 90% accuracy",
                                OverallRating = "yellow",
                                PlainLanguageSummary = "Needs a clearer measurement method."
                            }
                        ]
                    }),
                    DisplayOrder = 1
                });
            context.SaveChanges();

            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId, IepDocumentId = iepId, Status = "pending",
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

        // This source's own section content (summary + key points) is in the prompt.
        Assert.Contains("Reads below grade level.", prompt);
        Assert.Contains("needs fluency support", prompt);

        // The run's red flags, promoted from this single-source run's own call.
        Assert.Contains("No measurable baseline", prompt);

        // The source's own iep_goals ratings.
        Assert.Contains("Needs a clearer measurement method.", prompt);
        Assert.Contains("\"overallRating\":\"yellow\"", prompt);

        // Every analysis-derived block is wrapped in <iep_analysis> tags (item H), matching the
        // established pattern for untrusted/data-only content the system prompt also names.
        Assert.Contains("<iep_analysis>", prompt);
        Assert.Contains("</iep_analysis>", prompt);

        using var verify = fixture.CreateContext();
        var checklist2 = verify.Set<MeetingPrepChecklist>().Single(c => c.Id == checklistId);
        Assert.Equal("completed", checklist2.Status);
    }

    [Fact]
    public async Task GenerateChecklistAsync_ModeA_NoCompletedRun_FallsBackToSectionsOnly()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepId = fixture.SeedIepDocument();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId, IepDocumentId = iepId, Status = "pending",
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
        Assert.DoesNotContain("IEP ANALYSIS SUMMARY", prompt);
        Assert.DoesNotContain("GOAL ANALYSIS CONCERNS", prompt);

        using var verify = fixture.CreateContext();
        Assert.Equal("completed", verify.Set<MeetingPrepChecklist>().Single(c => c.Id == checklistId).Status);
    }
}
