using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using IepAssistant.Services.Security;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Pilot-gates plan, phase 3 (C11 adoption slice): magic-link sign-in for staff invited as
/// RelatedServiceProvider/GeneralEducator. Real SQLite in-memory engine (same pattern as
/// <see cref="StaffInviteServiceTests"/>); the OrgRoles HasData seed is applied by EnsureCreated.
/// </summary>
public sealed class MagicLinkServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IConfiguration _configuration;

    public MagicLinkServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-0123456789",
                ["Jwt:Issuer"] = "IepAssistant.Api",
                ["Jwt:Audience"] = "IepAssistant.Client",
                ["Jwt:ExpiryInDays"] = "7",
                ["App:FrontendUrl"] = "http://localhost:5173"
            }!)
            .Build();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private MagicLinkService CreateService(ApplicationDbContext ctx, CapturingEmailService email)
        => new(ctx, email, new JwtTokenFactory(_configuration), _configuration, NullLogger<MagicLinkService>.Instance);

    // ----------------------------------------------------------------- seed helpers

    private int SeedDistrict(bool magicLinkEnabled = true, bool requireMfa = true)
    {
        using var ctx = CreateContext();
        var d = new District { Name = "Maple Ridge", StateCode = "OH", MagicLinkEnabled = magicLinkEnabled, RequireMfaForMagicLink = requireMfa };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedStaffUser(int districtId, int orgRoleId, string email = "staff@example.com", bool userActive = true, bool profileActive = true, bool mfaEnabled = false)
    {
        using var ctx = CreateContext();
        var user = new User
        {
            Email = email, PasswordHash = "x", FirstName = "Sam", LastName = "Staff",
            Role = UserRole.Educator, IsActive = userActive, MfaEnabled = mfaEnabled
        };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        ctx.StaffProfiles.Add(new StaffProfile
        {
            UserId = user.Id, DistrictId = districtId, OrgRoleId = orgRoleId, IsActive = profileActive
        });
        ctx.SaveChanges();
        return user.Id;
    }

    // ================================================================= RequestAsync: eligibility

    [Theory]
    [InlineData(OrgRoleIds.RelatedServiceProvider)]
    [InlineData(OrgRoleIds.GeneralEducator)]
    public async Task RequestAsync_EligibleRole_SendsEmail(int orgRoleId)
    {
        var districtId = SeedDistrict();
        SeedStaffUser(districtId, orgRoleId, "provider@example.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).RequestAsync("provider@example.com");

        Assert.NotNull(email.LastMagicLinkUrl);
        Assert.Contains("token=", email.LastMagicLinkUrl);

        using var verifyCtx = CreateContext();
        Assert.Single(verifyCtx.MagicLinkTokens);
    }

    [Theory]
    [InlineData(OrgRoleIds.DistrictAdmin)]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task RequestAsync_IneligibleRole_DoesNotSendEmail(int orgRoleId)
    {
        var districtId = SeedDistrict();
        SeedStaffUser(districtId, orgRoleId, "notprovider@example.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).RequestAsync("notprovider@example.com");

        Assert.Null(email.LastMagicLinkUrl);
        using var verifyCtx = CreateContext();
        Assert.Empty(verifyCtx.MagicLinkTokens);
    }

    [Fact]
    public async Task RequestAsync_DisabledDistrict_DoesNotSendEmail()
    {
        var districtId = SeedDistrict(magicLinkEnabled: false);
        SeedStaffUser(districtId, OrgRoleIds.RelatedServiceProvider, "provider2@example.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).RequestAsync("provider2@example.com");

        Assert.Null(email.LastMagicLinkUrl);
    }

    [Fact]
    public async Task RequestAsync_UnknownEmail_DoesNotThrow_AndSendsNoEmail()
    {
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).RequestAsync("nobody-at-all@example.com");

        Assert.Null(email.LastMagicLinkUrl);
    }

    [Fact]
    public async Task RequestAsync_InactiveStaffProfile_DoesNotSendEmail()
    {
        var districtId = SeedDistrict();
        SeedStaffUser(districtId, OrgRoleIds.GeneralEducator, "inactive@example.com", profileActive: false);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).RequestAsync("inactive@example.com");

        Assert.Null(email.LastMagicLinkUrl);
    }

    [Fact]
    public async Task RequestAsync_ExceedsRateLimit_StopsSendingAfterFiveInWindow()
    {
        var districtId = SeedDistrict();
        SeedStaffUser(districtId, OrgRoleIds.GeneralEducator, "busy@example.com");

        for (var i = 0; i < 5; i++)
        {
            var email = new CapturingEmailService();
            using var ctx = CreateContext();
            await CreateService(ctx, email).RequestAsync("busy@example.com");
            Assert.NotNull(email.LastMagicLinkUrl);
        }

        var sixthEmail = new CapturingEmailService();
        using (var ctx = CreateContext())
            await CreateService(ctx, sixthEmail).RequestAsync("busy@example.com");

        Assert.Null(sixthEmail.LastMagicLinkUrl);

        using var verifyCtx = CreateContext();
        Assert.Equal(5, verifyCtx.MagicLinkTokens.Count());
    }

    // ================================================================= ConsumeAsync

    private async Task<string> RequestAndCaptureRawTokenAsync(string email, int districtId, int orgRoleId, bool mfaEnabled = false)
    {
        SeedStaffUser(districtId, orgRoleId, email, mfaEnabled: mfaEnabled);
        var capture = new CapturingEmailService();
        using var ctx = CreateContext();
        await CreateService(ctx, capture).RequestAsync(email);
        Assert.NotNull(capture.LastMagicLinkUrl);
        var uri = new Uri(capture.LastMagicLinkUrl!);
        var rawQuery = uri.Query.TrimStart('?'); // "token=<url-escaped-value>"
        const string prefix = "token=";
        Assert.StartsWith(prefix, rawQuery);
        return System.Net.WebUtility.UrlDecode(rawQuery.Substring(prefix.Length));
    }

    [Fact]
    public async Task ConsumeAsync_ValidToken_NoMfaRequired_ReturnsFullAuthResult()
    {
        var districtId = SeedDistrict(requireMfa: false);
        var rawToken = await RequestAndCaptureRawTokenAsync("consume1@example.com", districtId, OrgRoleIds.GeneralEducator);

        using var ctx = CreateContext();
        var result = await CreateService(ctx, new CapturingEmailService()).ConsumeAsync(rawToken);

        Assert.True(result.Success);
        Assert.False(result.RequiresMfa);
        Assert.False(result.MfaSetupRequired);
        Assert.NotNull(result.AuthResult);
        Assert.Equal("consume1@example.com", result.AuthResult!.User.Email);
    }

    [Fact]
    public async Task ConsumeAsync_UserHasMfaEnabled_ReturnsMfaPendingToken_NotFullAuth()
    {
        var districtId = SeedDistrict();
        var rawToken = await RequestAndCaptureRawTokenAsync("consume2@example.com", districtId, OrgRoleIds.RelatedServiceProvider, mfaEnabled: true);

        using var ctx = CreateContext();
        var result = await CreateService(ctx, new CapturingEmailService()).ConsumeAsync(rawToken);

        Assert.True(result.Success);
        Assert.True(result.RequiresMfa);
        Assert.False(result.MfaSetupRequired);
        Assert.NotNull(result.MfaPendingToken);
        Assert.Null(result.AuthResult);
    }

    [Fact]
    public async Task ConsumeAsync_MfaRequiredByDistrict_NotEnrolled_ReturnsMfaSetupRequired_NoToken()
    {
        var districtId = SeedDistrict(requireMfa: true);
        var rawToken = await RequestAndCaptureRawTokenAsync("consume3@example.com", districtId, OrgRoleIds.GeneralEducator, mfaEnabled: false);

        using var ctx = CreateContext();
        var result = await CreateService(ctx, new CapturingEmailService()).ConsumeAsync(rawToken);

        Assert.True(result.Success);
        Assert.True(result.MfaSetupRequired);
        Assert.False(result.RequiresMfa);
        Assert.Null(result.AuthResult);
        Assert.Null(result.MfaPendingToken);
    }

    [Fact]
    public async Task ConsumeAsync_SingleUse_SecondConsumeFails()
    {
        var districtId = SeedDistrict(requireMfa: false);
        var rawToken = await RequestAndCaptureRawTokenAsync("consume4@example.com", districtId, OrgRoleIds.GeneralEducator);

        using (var ctx = CreateContext())
        {
            var first = await CreateService(ctx, new CapturingEmailService()).ConsumeAsync(rawToken);
            Assert.True(first.Success);
        }

        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx, new CapturingEmailService()).ConsumeAsync(rawToken);
            Assert.False(second.Success);
        }
    }

    [Fact]
    public async Task ConsumeAsync_ExpiredToken_Fails()
    {
        var districtId = SeedDistrict(requireMfa: false);
        var rawToken = await RequestAndCaptureRawTokenAsync("consume5@example.com", districtId, OrgRoleIds.GeneralEducator);

        using (var ctx = CreateContext())
        {
            var token = ctx.MagicLinkTokens.Single();
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            ctx.SaveChanges();
        }

        using var verifyCtx = CreateContext();
        var result = await CreateService(verifyCtx, new CapturingEmailService()).ConsumeAsync(rawToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ConsumeAsync_UnknownToken_Fails()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx, new CapturingEmailService()).ConsumeAsync("not-a-real-token");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ConsumeAsync_DeactivatedUser_Fails()
    {
        var districtId = SeedDistrict(requireMfa: false);
        var rawToken = await RequestAndCaptureRawTokenAsync("consume6@example.com", districtId, OrgRoleIds.GeneralEducator);

        using (var ctx = CreateContext())
        {
            var user = ctx.Users.Single(u => u.Email == "consume6@example.com");
            user.IsActive = false;
            ctx.SaveChanges();
        }

        using var verifyCtx = CreateContext();
        var result = await CreateService(verifyCtx, new CapturingEmailService()).ConsumeAsync(rawToken);

        Assert.False(result.Success);
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Captures the magic-link URL passed to the email so tests can extract the raw token.</summary>
    private sealed class CapturingEmailService : TestSupport.TestEmailServiceBase
    {
        public string? LastMagicLinkUrl { get; private set; }

        public override Task SendMagicLinkEmailAsync(string toEmail, string firstName, string magicLinkUrl, CancellationToken ct = default)
        {
            LastMagicLinkUrl = magicLinkUrl;
            return Task.CompletedTask;
        }
    }
}
