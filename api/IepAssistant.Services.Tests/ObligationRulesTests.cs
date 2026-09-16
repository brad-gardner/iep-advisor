using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>Pure unit coverage for <see cref="ObligationRules"/> (plan 4, decision 2) — no DB.</summary>
public class ObligationRulesTests
{
    private static readonly DateTime Today = new(2026, 9, 15);

    [Theory]
    [InlineData("OH", ObligationRules.OhProfile)]
    [InlineData("oh", ObligationRules.OhProfile)]
    [InlineData(" OH ", ObligationRules.OhProfile)]
    [InlineData("CA", ObligationRules.DefaultProfile)]
    [InlineData(null, ObligationRules.DefaultProfile)]
    public void ResolveProfile_MapsStateCodeCaseInsensitively(string? stateCode, string expected)
    {
        Assert.Equal(expected, ObligationRules.ResolveProfile(stateCode));
    }

    [Fact]
    public void ResolveAnnualReviewDue_ExplicitDate_WinsOverIepDateFallback()
    {
        var explicitDue = new DateTime(2026, 12, 1);
        var (due, label) = ObligationRules.ResolveAnnualReviewDue(explicitDue, new DateTime(2026, 1, 1));

        Assert.Equal(explicitDue, due);
        Assert.Contains("due date", label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveAnnualReviewDue_NoExplicitDate_FallsBackToIepDatePlus365Days()
    {
        var iepDate = new DateTime(2026, 1, 1);
        var (due, label) = ObligationRules.ResolveAnnualReviewDue(null, iepDate);

        Assert.Equal(iepDate.AddDays(365), due);
        Assert.Contains("365", label);
    }

    [Fact]
    public void ResolveAnnualReviewDue_NoDatesAtAll_ReturnsNull()
    {
        var (due, _) = ObligationRules.ResolveAnnualReviewDue(null, null);
        Assert.Null(due);
    }

    [Fact]
    public void ResolveReevaluationDue_ExplicitDate_WinsOverEtrDateFallback()
    {
        var explicitDue = new DateTime(2027, 6, 1);
        var (due, _) = ObligationRules.ResolveReevaluationDue(explicitDue, new DateTime(2024, 1, 1));

        Assert.Equal(explicitDue, due);
    }

    [Fact]
    public void ResolveReevaluationDue_NoExplicitDate_FallsBackToEtrDatePlus3Years()
    {
        var etrDate = new DateTime(2024, 3, 10);
        var (due, label) = ObligationRules.ResolveReevaluationDue(null, etrDate);

        Assert.Equal(etrDate.AddYears(3), due);
        Assert.Contains("3 years", label);
    }

    [Fact]
    public void ResolveReevaluationDue_NoDatesAtAll_ReturnsNull()
    {
        var (due, _) = ObligationRules.ResolveReevaluationDue(null, null);
        Assert.Null(due);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("2026-01-01", null, true)]
    [InlineData("2026-01-01", "2024-01-01", false)]
    [InlineData(null, "2024-01-01", false)]
    public void NeedsEtrDue_OnlyWhenIepExistsAndEtrDateMissing(string? iep, string? etr, bool expected)
    {
        DateTime? iepDate = iep == null ? null : DateTime.Parse(iep);
        DateTime? etrDate = etr == null ? null : DateTime.Parse(etr);

        Assert.Equal(expected, ObligationRules.NeedsEtrDue(iepDate, etrDate));
    }

    [Fact]
    public void ResolveStatus_NoDate_IsUnknown()
    {
        Assert.Equal(ObligationStatus.Unknown, ObligationRules.ResolveStatus(null, Today));
    }

    [Fact]
    public void ResolveStatus_PastDate_IsOverdue()
    {
        Assert.Equal(ObligationStatus.Overdue, ObligationRules.ResolveStatus(Today.AddDays(-1), Today));
    }

    [Fact]
    public void ResolveStatus_ExactlyAtDueSoonBoundary_IsDueSoon()
    {
        // 30 days out, inclusive.
        Assert.Equal(ObligationStatus.DueSoon, ObligationRules.ResolveStatus(Today.AddDays(30), Today));
    }

    [Fact]
    public void ResolveStatus_OneDayPastDueSoonBoundary_IsUpcoming()
    {
        Assert.Equal(ObligationStatus.Upcoming, ObligationRules.ResolveStatus(Today.AddDays(31), Today));
    }

    [Fact]
    public void ResolveStatus_Today_IsDueSoon()
    {
        Assert.Equal(ObligationStatus.DueSoon, ObligationRules.ResolveStatus(Today, Today));
    }

    [Fact]
    public void DaysUntilDue_NoDate_ReturnsNull()
    {
        Assert.Null(ObligationRules.DaysUntilDue(null, Today));
    }

    [Fact]
    public void DaysUntilDue_ComputesWholeDays()
    {
        Assert.Equal(10, ObligationRules.DaysUntilDue(Today.AddDays(10), Today));
        Assert.Equal(-5, ObligationRules.DaysUntilDue(Today.AddDays(-5), Today));
    }
}
