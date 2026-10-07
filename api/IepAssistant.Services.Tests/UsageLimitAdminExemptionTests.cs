using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The per-child analysis cap applies to parents, not platform admins, whose usage is still recorded.
/// </summary>
public class UsageLimitAdminExemptionTests
{
    private const int Limit = 5;

    private static SubscriptionService Service(Domain.Data.ApplicationDbContext context) =>
        new(context, new ConfigurationBuilder().Build(), NullLogger<SubscriptionService>.Instance, TestSupport.TestLocalizers.Messages());

    private static void UseUpLimit(AnalysisRunTestFixture fixture, UserRole role)
    {
        using var context = fixture.CreateContext();
        context.Users.Single(u => u.Id == fixture.OwnerUserId).Role = role;
        for (var i = 0; i < Limit; i++)
        {
            context.UsageRecords.Add(new UsageRecord
            {
                UserId = fixture.OwnerUserId,
                ChildProfileId = fixture.ChildId,
                OperationType = "analysis",
            });
        }

        context.SaveChanges();
    }

    [Fact]
    public async Task Parent_AtLimit_IsRefused()
    {
        using var fixture = new AnalysisRunTestFixture();
        UseUpLimit(fixture, UserRole.Parent);

        using var context = fixture.CreateContext();
        var service = Service(context);

        Assert.False(await service.CanPerformAnalysisAsync(fixture.OwnerUserId, fixture.ChildId));
        Assert.Null(await service.TryReserveUsageAsync(fixture.OwnerUserId, fixture.ChildId, "analysis", Limit));
    }

    [Fact]
    public async Task Admin_AtLimit_IsAllowed_AndUsageStillRecorded()
    {
        using var fixture = new AnalysisRunTestFixture();
        UseUpLimit(fixture, UserRole.Admin);

        using (var context = fixture.CreateContext())
        {
            var service = Service(context);
            Assert.True(await service.CanPerformAnalysisAsync(fixture.OwnerUserId, fixture.ChildId));
            Assert.NotNull(await service.TryReserveUsageAsync(fixture.OwnerUserId, fixture.ChildId, "analysis", Limit));
        }

        using var verify = fixture.CreateContext();
        Assert.Equal(Limit + 1, verify.UsageRecords.Count(u => u.UserId == fixture.OwnerUserId && u.OperationType == "analysis"));
    }
}
