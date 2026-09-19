using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The one rule for a child's state: linked district → linked school → owning parent's profile → unknown, each
/// rung normalised to a USPS code and skipped when unrecognised.
/// </summary>
public sealed class ChildStateResolverTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ChildStateResolverTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private int SeedChild(string prefix, string? ownerState)
    {
        using var ctx = CreateContext();
        var owner = new User { Email = $"{prefix}@example.com", PasswordHash = "x", FirstName = "Dana", LastName = "Parent", Role = UserRole.Parent, State = ownerState };
        ctx.Users.Add(owner);
        ctx.SaveChanges();
        var child = new ChildProfile { UserId = owner.Id, FirstName = "Jordan" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        return child.Id;
    }

    private void SeedLink(string prefix, int childId, string? districtState, string? schoolState, bool active = true, bool accepted = true, DateTime? acceptedAt = null)
    {
        using var ctx = CreateContext();
        var district = new District { Name = prefix, StateCode = districtState };
        ctx.Districts.Add(district);
        ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = prefix, StateCode = schoolState };
        ctx.Schools.Add(school);
        ctx.SaveChanges();
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan" };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();
        ctx.ChildLinks.Add(new ChildLink
        {
            ChildProfileId = childId, SchoolStudentId = student.Id, IsActive = active,
            AcceptedAt = accepted ? acceptedAt ?? DateTime.UtcNow : null, LinkedAt = DateTime.UtcNow, InviteExpiresAt = DateTime.UtcNow.AddDays(14)
        });
        ctx.SaveChanges();
    }

    private async Task<string?> ResolveAsync(int childId)
    {
        using var ctx = CreateContext();
        return await ChildStateResolver.ResolveAsync(ctx, childId);
    }

    [Fact]
    public async Task Resolve_UsesTheLinkedDistrictFirst()
    {
        var childId = SeedChild("district", ownerState: "PA");
        SeedLink("district", childId, districtState: "oh", schoolState: "NY");

        Assert.Equal("OH", await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_FallsThroughToTheSchool_WhenTheDistrictHasNoUsableState()
    {
        var childId = SeedChild("school", ownerState: "PA");
        SeedLink("school", childId, districtState: "??", schoolState: "Ohio");

        Assert.Equal("OH", await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_FallsThroughToTheOwner_WhenTheLinkedRecordHasNoState()
    {
        var childId = SeedChild("owner", ownerState: "pa");
        SeedLink("owner", childId, districtState: null, schoolState: "");

        Assert.Equal("PA", await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_UsesTheOwner_WhenThereIsNoLink()
    {
        var childId = SeedChild("nolink", ownerState: "OH");

        Assert.Equal("OH", await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_IgnoresInactiveAndUnacceptedLinks()
    {
        var childId = SeedChild("deadlinks", ownerState: "PA");
        SeedLink("inactive", childId, districtState: "OH", schoolState: "OH", active: false);
        SeedLink("pending", childId, districtState: "OH", schoolState: "OH", accepted: false);

        Assert.Equal("PA", await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_PrefersTheMostRecentlyAcceptedLink()
    {
        var childId = SeedChild("recent", ownerState: null);
        SeedLink("older", childId, districtState: "PA", schoolState: null, acceptedAt: DateTime.UtcNow.AddDays(-30));
        SeedLink("newer", childId, districtState: "OH", schoolState: null, acceptedAt: DateTime.UtcNow.AddDays(-1));

        Assert.Equal("OH", await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_ReturnsNull_WhenNoRungHasARecognisedState()
    {
        var childId = SeedChild("unknown", ownerState: "Narnia");
        SeedLink("unknown", childId, districtState: "XX", schoolState: "Ontario");

        Assert.Null(await ResolveAsync(childId));
    }

    [Fact]
    public async Task Resolve_UnknownChild_IsNull()
    {
        Assert.Null(await ResolveAsync(999_999));
    }

    [Theory]
    [InlineData("OH", "OH")]
    [InlineData("oh", "OH")]
    [InlineData(" Oh ", "OH")]
    [InlineData("Ohio", "OH")]
    [InlineData("ohio", "OH")]
    [InlineData("New York", "NY")]
    [InlineData("district of columbia", "DC")]
    [InlineData("DC", "DC")]
    [InlineData("Wyoming", "WY")]
    public void Normalize_AcceptsCodesAndFullNamesInAnyCase(string input, string expected)
    {
        Assert.Equal(expected, ChildStateResolver.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    [InlineData("O")]
    [InlineData("OHI")]
    [InlineData("Ohio, USA")]
    [InlineData("Puerto Rico")]
    [InlineData("PR")]
    [InlineData("Ontario")]
    [InlineData("<script>")]
    public void Normalize_RejectsAnythingThatIsNotAStateOrDc(string? input)
    {
        Assert.Null(ChildStateResolver.Normalize(input));
    }

    public void Dispose() => _connection.Dispose();
}
