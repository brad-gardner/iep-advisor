using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Api.DTOs.Auth;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P2 coverage for open district self-serve signup (<see cref="AuthService.RegisterDistrictAsync"/>):
/// the happy path (User+District+StaffProfile in one transaction, DistrictAdmin profile, JWT with the
/// Educator role), the security-critical "always a new District" rule (duplicate names create distinct
/// rows — never join an existing org), email-collision rejection with no partial state, and transaction
/// atomicity (failure after the User insert leaves no orphan District). Real SQLite in-memory engine,
/// same fixture style as <see cref="StudentInviteServiceTests"/>.
/// </summary>
public sealed class AuthServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IConfiguration _configuration;

    public AuthServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-0123456789",
                ["Jwt:Issuer"] = "IepAssistant.Api",
                ["Jwt:Audience"] = "IepAssistant.Client",
                ["Jwt:ExpiryInDays"] = "7"
            })
            .Build();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private AuthService CreateService(ApplicationDbContext ctx)
        => new(_configuration, new UserRepository(ctx), ctx);

    private static RegisterDistrictModel Model(string email, string district, string? state = "OH") => new()
    {
        Email = email,
        Password = "Password123!",
        FirstName = "Dana",
        LastName = "Admin",
        DistrictName = district,
        StateCode = state
    };

    // ----------------------------------------------------------------- happy path

    [Fact]
    public async Task RegisterDistrict_Success_CreatesUserDistrictAndDistrictAdminProfile()
    {
        RegisterDistrictResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).RegisterDistrictAsync(Model("admin@district.org", "Maple District"));

        Assert.True(result.Success);
        Assert.NotNull(result.AuthResult);
        Assert.False(string.IsNullOrWhiteSpace(result.AuthResult!.Token));
        Assert.Equal("Educator", result.AuthResult.User.Role);
        Assert.Equal("admin@district.org", result.AuthResult.User.Email);

        using (var ctx = CreateContext())
        {
            var user = ctx.Users.Single(u => u.Email == "admin@district.org");
            Assert.Equal(UserRole.Educator, user.Role);
            Assert.True(user.IsActive);
            Assert.Equal("active", user.SubscriptionStatus);
            Assert.NotNull(user.SubscriptionExpiresAt);

            var district = ctx.Districts.Single(d => d.Name == "Maple District");
            Assert.Equal("OH", district.StateCode);

            var profile = ctx.StaffProfiles.Single(p => p.UserId == user.Id);
            Assert.Equal(district.Id, profile.DistrictId);
            Assert.Null(profile.SchoolId);
            Assert.Equal(OrgRoleIds.DistrictAdmin, profile.OrgRoleId);
            Assert.True(profile.IsActive);
        }
    }

    [Fact]
    public async Task RegisterDistrict_Success_MintsJwtWithEducatorRoleClaim()
    {
        RegisterDistrictResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).RegisterDistrictAsync(Model("jwt@district.org", "Cedar District"));

        Assert.True(result.Success);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AuthResult!.Token);
        var roleClaim = jwt.Claims.Single(c => c.Type == ClaimTypes.Role);
        Assert.Equal("Educator", roleClaim.Value);

        var userId = result.AuthResult.User.Id;
        var subClaim = jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier);
        Assert.Equal(userId.ToString(), subClaim.Value);
        Assert.Equal("jwt@district.org", jwt.Claims.Single(c => c.Type == ClaimTypes.Email).Value);
    }

    // ----------------------------------------------------------------- duplicate district name

    [Fact]
    public async Task RegisterDistrict_DuplicateDistrictName_CreatesDistinctDistricts()
    {
        // Two unrelated admins sign up with the SAME district name + state. This must NEVER join them
        // into one org — each gets its own District row (security requirement).
        RegisterDistrictResult first, second;
        using (var ctx = CreateContext())
            first = await CreateService(ctx).RegisterDistrictAsync(Model("first@dup.org", "Riverside District"));
        using (var ctx = CreateContext())
            second = await CreateService(ctx).RegisterDistrictAsync(Model("second@dup.org", "Riverside District"));

        Assert.True(first.Success);
        Assert.True(second.Success);

        using (var ctx = CreateContext())
        {
            var districts = ctx.Districts.Where(d => d.Name == "Riverside District").ToList();
            Assert.Equal(2, districts.Count);

            var firstUser = ctx.Users.Single(u => u.Email == "first@dup.org");
            var secondUser = ctx.Users.Single(u => u.Email == "second@dup.org");
            var firstProfile = ctx.StaffProfiles.Single(p => p.UserId == firstUser.Id);
            var secondProfile = ctx.StaffProfiles.Single(p => p.UserId == secondUser.Id);

            // Each admin sees ONLY their own district — distinct DistrictIds.
            Assert.NotEqual(firstProfile.DistrictId, secondProfile.DistrictId);
        }
    }

    // ----------------------------------------------------------------- email collision

    [Fact]
    public async Task RegisterDistrict_EmailAlreadyRegistered_Fails_NoOrgRowsCreated()
    {
        using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Email = "taken@district.org", PasswordHash = "x",
                FirstName = "Existing", LastName = "User", Role = UserRole.Parent, IsActive = true
            });
            await ctx.SaveChangesAsync();
        }

        RegisterDistrictResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).RegisterDistrictAsync(Model("taken@district.org", "Should Not Exist"));

        Assert.False(result.Success);
        Assert.Contains("already registered", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.AuthResult);

        using (var ctx = CreateContext())
        {
            // No District / StaffProfile created; only the single pre-existing user remains.
            Assert.Empty(ctx.Districts);
            Assert.Empty(ctx.StaffProfiles);
            Assert.Single(ctx.Users.Where(u => u.Email == "taken@district.org"));
        }
    }

    // ----------------------------------------------------------------- transaction atomicity

    [Fact]
    public async Task RegisterDistrict_FailureAfterUserInsert_RollsBack_NoOrphanDistrict()
    {
        // Seed an INACTIVE user with the target email. GetByEmailAsync filters IsActive, so the
        // pre-check returns null and the flow proceeds — but the User insert then violates the unique
        // Email index INSIDE the transaction. This exercises the rollback path: no orphan District,
        // no StaffProfile, and the original (inactive) user is untouched.
        using (var ctx = CreateContext())
        {
            ctx.Users.Add(new User
            {
                Email = "ghost@district.org", PasswordHash = "x",
                FirstName = "Ghost", LastName = "User", Role = UserRole.Parent, IsActive = false
            });
            await ctx.SaveChangesAsync();
        }

        using (var ctx = CreateContext())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                CreateService(ctx).RegisterDistrictAsync(Model("ghost@district.org", "Orphan District")));
        }

        using (var ctx = CreateContext())
        {
            Assert.Empty(ctx.Districts);
            Assert.Empty(ctx.StaffProfiles);
            Assert.Single(ctx.Users.Where(u => u.Email == "ghost@district.org"));
        }
    }

    // ----------------------------------------------------------------- DTO validation (data annotations)

    private static IList<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void RegisterDistrictRequest_MissingDistrictName_IsInvalid()
    {
        var dto = new RegisterDistrictRequest
        {
            Email = "v@x.org", Password = "Password123!", FirstName = "A", LastName = "B",
            DistrictName = "", StateCode = "OH"
        };

        var results = Validate(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterDistrictRequest.DistrictName)));
    }

    [Fact]
    public void RegisterDistrictRequest_ThreeCharStateCode_IsInvalid()
    {
        var dto = new RegisterDistrictRequest
        {
            Email = "v2@x.org", Password = "Password123!", FirstName = "A", LastName = "B",
            DistrictName = "Valid District", StateCode = "OHX"
        };

        var results = Validate(dto);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RegisterDistrictRequest.StateCode)));
    }

    [Fact]
    public void RegisterDistrictRequest_NullStateCode_IsValid()
    {
        var dto = new RegisterDistrictRequest
        {
            Email = "v3@x.org", Password = "Password123!", FirstName = "A", LastName = "B",
            DistrictName = "Valid District", StateCode = null
        };

        Assert.Empty(Validate(dto));
    }

    // ----------------------------------------------------------------- validation (service-level state handling)

    [Fact]
    public async Task RegisterDistrict_BlankStateCode_StoresNull()
    {
        RegisterDistrictResult result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).RegisterDistrictAsync(Model("nostate@district.org", "Stateless District", state: null));

        Assert.True(result.Success);
        using (var ctx = CreateContext())
            Assert.Null(ctx.Districts.Single(d => d.Name == "Stateless District").StateCode);
    }

    public void Dispose() => _connection.Dispose();
}
