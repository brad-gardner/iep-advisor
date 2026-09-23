using IepAssistant.Api.Seeding;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Guards the demo district's roster and document content. `seed-demo` runs against a real database and
/// takes minutes, so the cheap contradictions — a parent linked to the wrong child, a student in a building
/// that does not teach their grade, a disability category with no goals written for it — are caught here
/// instead of halfway through a seed run.
/// </summary>
public class DemoRosterTests
{
    [Fact]
    public void Roster_IsSelfConsistent()
    {
        // Every rule lives in DemoRoster.Validate: parent/child surnames, unique emails, the student
        // account, a District Admin with no school, every building staffed, every grade taught where the
        // student sits. A failure message names the offending row.
        DemoRoster.Validate();
    }

    [Fact]
    public void EveryStageOfTheProcess_HasAtLeastOneStudent()
    {
        // The point of the demo district is that every screen has something in it.
        foreach (var track in Enum.GetValues<DemoRoster.Track>())
            Assert.Contains(DemoRoster.Students, s => s.Track == track);
    }

    [Fact]
    public void EveryBuilding_HasStudentsAndTwoCaseManagers()
    {
        for (var schoolIndex = 0; schoolIndex < DemoRoster.Schools.Length; schoolIndex++)
        {
            Assert.Contains(DemoRoster.Students, s => s.SchoolIndex == schoolIndex);
            Assert.True(DemoRoster.CaseManagersAt(schoolIndex).Count() >= 2,
                $"'{DemoRoster.Schools[schoolIndex].Name}' should have more than one case manager, so a caseload split is visible.");
        }
    }

    [Fact]
    public void SpecificLearningDisability_IsTheLargestGroup()
    {
        // IDEA's real shape. A roster that drifts away from it stops looking like a district's.
        var counts = DemoRoster.Students
            .Where(s => s.Disability != null)
            .GroupBy(s => s.Disability!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var largest = counts.OrderByDescending(kv => kv.Value).First();
        Assert.Equal(DisabilityCategory.SpecificLearningDisability, largest.Key);
    }

    [Fact]
    public void OnlyStudentsUnderEvaluation_LackADisabilityCategory()
    {
        foreach (var student in DemoRoster.Students.Where(s => s.Disability == null))
            Assert.Equal(DemoRoster.Track.EvaluationInProgress, student.Track);
    }

    [Theory]
    [MemberData(nameof(AllDisabilityCategoriesAndUndetermined))]
    public void EveryDisabilityCategory_ProducesUsableIepContent(DisabilityCategory? disability)
    {
        var goals = DemoIepContent.GoalsFor(disability, GradeLevel.G4, "Sam");
        Assert.True(goals.Count >= 2, "A demo IEP needs at least two goals to be worth looking at.");
        foreach (var goal in goals)
        {
            Assert.False(string.IsNullOrWhiteSpace(goal.Domain));
            Assert.False(string.IsNullOrWhiteSpace(goal.Baseline));
            Assert.False(string.IsNullOrWhiteSpace(goal.TargetCriteria));
            Assert.False(string.IsNullOrWhiteSpace(goal.MeasurementMethod));
            // Progress observations are recorded in the goal's unit, looked up by domain.
            Assert.False(string.IsNullOrWhiteSpace(goal.Unit));
            Assert.Contains("Sam", goal.GoalText);
        }

        // One unit per domain, or AddGoalObservationsAsync would pick an arbitrary one.
        Assert.Equal(goals.Select(g => g.Domain).Distinct().Count(), goals.Count);

        Assert.NotEmpty(DemoIepContent.ServicesFor(disability, GradeLevel.G4));
        Assert.NotEmpty(DemoIepContent.AccommodationsFor(disability, GradeLevel.G4));
    }

    [Theory]
    [InlineData(GradeLevel.K, false)]
    [InlineData(GradeLevel.G8, false)]
    [InlineData(GradeLevel.G9, true)]
    [InlineData(GradeLevel.G12, true)]
    public void TransitionPlanning_AppearsFromGradeNine(GradeLevel grade, bool expected)
    {
        // Ohio requires postsecondary transition planning from age 14 — grade 9 here.
        var transition = DemoIepContent.TransitionFor(grade, "Sam");
        Assert.Equal(expected, transition.Count > 0);
        if (expected)
            Assert.All(transition, t =>
            {
                Assert.False(string.IsNullOrWhiteSpace(t.GoalArea));
                Assert.False(string.IsNullOrWhiteSpace(t.Services));
            });
    }

    [Fact]
    public void RelatedServiceStaff_AreResolvable()
    {
        // DemoSeeder looks these four up by surname; a rename in the roster must not leave it throwing.
        Assert.Equal(OrgRoleIds.RelatedServiceProvider, DemoRoster.Slp.OrgRoleId);
        Assert.Equal(OrgRoleIds.RelatedServiceProvider, DemoRoster.OccupationalTherapist.OrgRoleId);
        Assert.Equal(OrgRoleIds.RelatedServiceProvider, DemoRoster.PhysicalTherapist.OrgRoleId);
        Assert.Equal(OrgRoleIds.RelatedServiceProvider, DemoRoster.Psychologist.OrgRoleId);
    }

    [Fact]
    public void EveryDemoEmail_IsOnTheReservedFictionalDomain()
    {
        // DemoSeeder.ResetAsync sweeps stranded accounts by this domain; an address off it would survive a
        // reset and block the next seed on a duplicate email.
        var emails = DemoRoster.Staff.Select(s => s.Email)
            .Concat(DemoRoster.Parents.Select(p => p.Email))
            .Concat(new[] { DemoRoster.DistrictAdmin.Email, DemoRoster.StudentAccountEmail });

        Assert.All(emails, email => Assert.EndsWith("@" + DemoRoster.EmailDomain, email));
    }

    public static TheoryData<DisabilityCategory?> AllDisabilityCategoriesAndUndetermined()
    {
        var data = new TheoryData<DisabilityCategory?>();
        foreach (var category in Enum.GetValues<DisabilityCategory>())
            data.Add(category);
        data.Add(null); // a student still being evaluated
        return data;
    }
}
