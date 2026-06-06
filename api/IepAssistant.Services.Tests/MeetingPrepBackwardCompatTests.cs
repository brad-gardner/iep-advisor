using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P2a guardrails: the legacy IepAnalysis / EtrAnalysis path and the Meeting Prep path must
/// compile and round-trip without the (now-dropped) SuggestedQuestions column, and the analysis
/// models must no longer surface a SuggestedQuestions property.
/// </summary>
public class MeetingPrepBackwardCompatTests
{
    [Fact]
    public void IepAnalysis_RoundTrips_WithoutSuggestedQuestionsColumn()
    {
        using var fixture = new AnalysisRunTestFixture();
        var iepDocId = fixture.SeedIepDocument();

        int analysisId;
        using (var context = fixture.CreateContext())
        {
            var analysis = new IepAnalysis
            {
                IepDocumentId = iepDocId,
                Status = "completed",
                OverallSummary = "Summary without suggested questions",
                OverallRedFlags = "[\"flag\"]",
            };
            context.IepAnalyses.Add(analysis);
            context.SaveChanges();
            analysisId = analysis.Id;
        }

        using (var verify = fixture.CreateContext())
        {
            var loaded = verify.IepAnalyses.Single(a => a.Id == analysisId);
            Assert.Equal("completed", loaded.Status);
            Assert.Equal("Summary without suggested questions", loaded.OverallSummary);
        }
    }

    [Fact]
    public void EtrAnalysis_RoundTrips_WithoutSuggestedQuestionsColumn()
    {
        using var fixture = new AnalysisRunTestFixture();
        var etrDocId = fixture.SeedEtrDocument();

        int analysisId;
        using (var context = fixture.CreateContext())
        {
            var analysis = new EtrAnalysis
            {
                EtrDocumentId = etrDocId,
                Status = "completed",
                OverallSummary = "ETR summary without suggested questions",
                OverallRedFlags = "[\"etr flag\"]",
            };
            context.EtrAnalyses.Add(analysis);
            context.SaveChanges();
            analysisId = analysis.Id;
        }

        using (var verify = fixture.CreateContext())
        {
            var loaded = verify.EtrAnalyses.Single(a => a.Id == analysisId);
            Assert.Equal("completed", loaded.Status);
            Assert.Equal("ETR summary without suggested questions", loaded.OverallSummary);
        }
    }

    [Fact]
    public void MeetingPrepChecklist_RoundTrips_WithMeetingDate()
    {
        using var fixture = new AnalysisRunTestFixture();
        var meetingDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId,
                MeetingDate = meetingDate,
                Status = "pending",
                IsActive = true,
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        using (var verify = fixture.CreateContext())
        {
            var loaded = verify.Set<MeetingPrepChecklist>().Single(m => m.Id == checklistId);
            Assert.Equal(meetingDate, loaded.MeetingDate);
        }
    }

    [Fact]
    public void MeetingPrepChecklist_AllowsNullMeetingDate()
    {
        using var fixture = new AnalysisRunTestFixture();

        int checklistId;
        using (var context = fixture.CreateContext())
        {
            var checklist = new MeetingPrepChecklist
            {
                ChildProfileId = fixture.ChildId,
                MeetingDate = null,
                Status = "pending",
                IsActive = true,
            };
            context.Set<MeetingPrepChecklist>().Add(checklist);
            context.SaveChanges();
            checklistId = checklist.Id;
        }

        using (var verify = fixture.CreateContext())
        {
            var loaded = verify.Set<MeetingPrepChecklist>().Single(m => m.Id == checklistId);
            Assert.Null(loaded.MeetingDate);
        }
    }

    [Theory]
    [InlineData(typeof(IepAnalysis))]
    [InlineData(typeof(EtrAnalysis))]
    [InlineData(typeof(AnalysisResponse))]
    [InlineData(typeof(EtrAnalysisResponse))]
    [InlineData(typeof(IepAnalysisModel))]
    [InlineData(typeof(EtrAnalysisModel))]
    public void AnalysisTypes_DoNotExpose_SuggestedQuestions(Type type)
    {
        Assert.Null(type.GetProperty("SuggestedQuestions"));
    }
}
