using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The child page generates meeting prep through <see cref="MeetingPrepService.GenerateFromGoalsAsync"/>.
/// It must ground the checklist in the child's latest parsed IEP when one exists, or Claude is told
/// the parent "has not yet received the IEP" about a child whose IEP was just analyzed.
/// </summary>
public class MeetingPrepAnchoringTests
{
    private sealed class AllowAll : IAccessService
    {
        public Task<AccessRole?> GetRoleAsync(int childId, int userId, CancellationToken ct = default) =>
            Task.FromResult<AccessRole?>(AccessRole.Owner);

        public Task<bool> HasMinimumRoleAsync(int childId, int userId, AccessRole minimumRole, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    // GenerateFromGoalsAsync touches only access and the context; the other collaborators are unused.
    private static MeetingPrepService Service(Domain.Data.ApplicationDbContext context) =>
        new(null!, null!, null!, new AllowAll(), null!, context, null!, TestSupport.TestLocalizers.Ai(), NullLogger<MeetingPrepService>.Instance);

    private static int SeedIep(AnalysisRunTestFixture fixture, DateTime? iepDate, string status = "parsed", bool isActive = true)
    {
        using var context = fixture.CreateContext();
        var doc = new IepDocument
        {
            ChildProfileId = fixture.ChildId,
            IepDate = iepDate,
            Status = status,
            IsActive = isActive,
        };
        context.IepDocuments.Add(doc);
        context.SaveChanges();
        return doc.Id;
    }

    private static async Task<MeetingPrepChecklist> Generate(AnalysisRunTestFixture fixture)
    {
        int id;
        using (var context = fixture.CreateContext())
        {
            var result = await Service(context).GenerateFromGoalsAsync(fixture.ChildId, fixture.OwnerUserId);
            Assert.True(result.Success);
            id = result.Data;
        }

        using var verify = fixture.CreateContext();
        return verify.Set<MeetingPrepChecklist>().Single(c => c.Id == id);
    }

    [Fact]
    public async Task AnchorsToLatestParsedIep()
    {
        using var fixture = new AnalysisRunTestFixture();
        SeedIep(fixture, new DateTime(2024, 10, 1));
        var latest = SeedIep(fixture, new DateTime(2026, 10, 1));
        SeedIep(fixture, new DateTime(2025, 10, 1));

        var checklist = await Generate(fixture);

        Assert.Equal(latest, checklist.IepDocumentId);
    }

    [Fact]
    public async Task SkipsUnparsedAndInactiveDocuments()
    {
        using var fixture = new AnalysisRunTestFixture();
        var parsed = SeedIep(fixture, new DateTime(2025, 10, 1));
        SeedIep(fixture, new DateTime(2026, 10, 1), status: "processing");
        SeedIep(fixture, new DateTime(2026, 11, 1), isActive: false);

        var checklist = await Generate(fixture);

        Assert.Equal(parsed, checklist.IepDocumentId);
    }

    [Fact]
    public async Task FallsBackToGoalsOnly_WhenChildHasNoParsedIep()
    {
        using var fixture = new AnalysisRunTestFixture();
        SeedIep(fixture, new DateTime(2026, 10, 1), status: "error");

        var checklist = await Generate(fixture);

        Assert.Null(checklist.IepDocumentId);
    }
}
