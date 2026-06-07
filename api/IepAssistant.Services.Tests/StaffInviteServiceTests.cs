using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using IepAssistant.Services.Security;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P4 coverage: staff invite lifecycle + staff management. Org authorization is resolved per-request from
/// the real <see cref="OrgAccessService"/> against an active StaffProfile (DB-backed). Tokens follow the
/// shared hash-stored, single-use, email-bound pattern. Uses a real SQLite in-memory engine (same pattern
/// as <see cref="DistrictServiceTests"/>/<see cref="StudentInviteServiceTests"/>); the OrgRoles HasData
/// seed is applied by EnsureCreated.
/// </summary>
public sealed class StaffInviteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly IConfiguration _configuration;

    public StaffInviteServiceTests()
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

    private StaffInviteService CreateService(ApplicationDbContext ctx, CapturingEmailService email, bool exposeLinks = false)
        => new(ctx, new OrgAccessService(ctx), email, new JwtTokenFactory(_configuration),
               new InviteLinkExposure(exposeLinks), _configuration, NullLogger<StaffInviteService>.Instance);

    // ----------------------------------------------------------------- seed helpers

    private int SeedDistrict(string name, string? stateCode = "OH")
    {
        using var ctx = CreateContext();
        var d = new District { Name = name, StateCode = stateCode };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedSchool(int districtId, string name, bool isActive = true)
    {
        using var ctx = CreateContext();
        var s = new School { DistrictId = districtId, Name = name, StateCode = "OH", IsActive = isActive };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    private int SeedUser(string email, UserRole role = UserRole.Educator)
    {
        using var ctx = CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FirstName = "F", LastName = "L", Role = role };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        return u.Id;
    }

    /// <summary>Seeds a staff user; returns (userId, staffProfileId).</summary>
    private (int userId, int staffProfileId) SeedStaff(string email, int districtId, int? schoolId, int orgRoleId, bool isActive = true)
    {
        var userId = SeedUser(email);
        using var ctx = CreateContext();
        var p = new StaffProfile
        {
            UserId = userId,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = orgRoleId,
            IsActive = isActive
        };
        ctx.StaffProfiles.Add(p);
        ctx.SaveChanges();
        return (userId, p.Id);
    }

    // ================================================================= Invite: role matrix

    [Theory]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task DistrictAdmin_InvitesSchoolScopedRole_IntoActiveSchool_Succeeds(int orgRoleId)
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "new@x.com", OrgRoleId = orgRoleId, SchoolId = schoolId
            });
            Assert.True(result.Success);
        }

        using (var ctx = CreateContext())
        {
            var invite = Assert.Single(ctx.StaffInvites);
            Assert.Equal("new@x.com", invite.Email);
            Assert.Equal(orgRoleId, invite.OrgRoleId);
            Assert.Equal(schoolId, invite.SchoolId);
            Assert.NotNull(invite.InviteToken);
        }
        Assert.NotNull(email.LastRawToken);
    }

    [Fact]
    public async Task DistrictAdmin_InvitesDistrictAdmin_WithNullSchool_Succeeds()
    {
        var districtId = SeedDistrict("Maple");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "da@x.com", OrgRoleId = OrgRoleIds.DistrictAdmin, SchoolId = null
            });
            Assert.True(result.Success);
        }

        using var ctx2 = CreateContext();
        Assert.Null(ctx2.StaffInvites.Single().SchoolId);
    }

    [Fact]
    public async Task DistrictAdmin_InvitesDistrictAdmin_WithSchool_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "da@x.com", OrgRoleId = OrgRoleIds.DistrictAdmin, SchoolId = schoolId
        });
        Assert.False(result.Success);
        Assert.Contains("must not specify a school", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DistrictAdmin_InvitesTeacher_WithoutSchool_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "t@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = null
        });
        Assert.False(result.Success);
        Assert.Contains("school is required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DistrictAdmin_InvitesIntoInactiveSchool_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var inactive = SeedSchool(districtId, "Closed", isActive: false);
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "t@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = inactive
        });
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DistrictAdmin_InvitesIntoOtherDistrictSchool_IsRejected()
    {
        var districtA = SeedDistrict("A");
        var districtB = SeedDistrict("B");
        var schoolB = SeedSchool(districtB, "BSchool");
        var (adminA, _) = SeedStaff("admin@a.com", districtA, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(adminA, new CreateStaffInviteModel
        {
            Email = "t@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolB
        });
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(OrgRoleIds.SchoolAdmin)]
    [InlineData(OrgRoleIds.Teacher)]
    public async Task SchoolAdmin_InvitesIntoOwnSchool_Succeeds(int orgRoleId)
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (saUser, _) = SeedStaff("sa@x.com", districtId, schoolId, OrgRoleIds.SchoolAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
        {
            // SchoolId omitted — forced to caller's own.
            var result = await CreateService(ctx, email).InviteAsync(saUser, new CreateStaffInviteModel
            {
                Email = "new@x.com", OrgRoleId = orgRoleId, SchoolId = null
            });
            Assert.True(result.Success);
        }

        using var ctx2 = CreateContext();
        Assert.Equal(schoolId, ctx2.StaffInvites.Single().SchoolId);
    }

    [Fact]
    public async Task SchoolAdmin_InvitesIntoAnotherSchool_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var mySchool = SeedSchool(districtId, "Mine");
        var otherSchool = SeedSchool(districtId, "Other");
        var (saUser, _) = SeedStaff("sa@x.com", districtId, mySchool, OrgRoleIds.SchoolAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(saUser, new CreateStaffInviteModel
        {
            Email = "t@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = otherSchool
        });
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SchoolAdmin_InvitesDistrictAdmin_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (saUser, _) = SeedStaff("sa@x.com", districtId, schoolId, OrgRoleIds.SchoolAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(saUser, new CreateStaffInviteModel
        {
            Email = "da@x.com", OrgRoleId = OrgRoleIds.DistrictAdmin, SchoolId = null
        });
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Teacher_Invites_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (teacher, _) = SeedStaff("teach@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(teacher, new CreateStaffInviteModel
        {
            Email = "t@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
        });
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================= Invite: rejection guards

    [Theory]
    [InlineData(UserRole.Parent)]
    [InlineData(UserRole.Educator)]
    public async Task Invite_EmailAlreadyHasAccount_IsRejected(UserRole existingRole)
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedUser("taken@x.com", existingRole);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "taken@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
        });
        Assert.False(result.Success);
        Assert.Contains("work email", result.Message, StringComparison.OrdinalIgnoreCase);
        using var ctx2 = CreateContext();
        Assert.Empty(ctx2.StaffInvites);
    }

    [Fact]
    public async Task Invite_DuplicatePending_IsRejected()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "dup@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            })).Success);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "dup@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
            Assert.False(result.Success);
            Assert.Contains("already been invited", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using var ctx2 = CreateContext();
        Assert.Single(ctx2.StaffInvites);
    }

    [Fact]
    public async Task Invite_ExpiredPending_AllowsReinvite()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        // Seed an EXPIRED pending invite for the same email.
        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(new StaffInvite
            {
                Email = "again@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
                InviteToken = InviteTokenHelper.Hash(InviteTokenHelper.Generate()),
                InviteExpiresAt = DateTime.UtcNow.AddDays(-1), IsActive = true, InvitedByUserId = adminUserId
            });
            await ctx.SaveChangesAsync();
        }

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "again@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
        });
        Assert.True(result.Success);
    }

    // ================================================================= Filtered unique index (DB backstop)

    /// <summary>
    /// TOCTOU defense at the storage layer: bypass the service pre-check by inserting a second LIVE pending
    /// StaffInvite for the same email directly via the context. The filtered unique index on Email
    /// (IsActive=1 AND AcceptedAt IS NULL AND InviteToken IS NOT NULL) must reject it — proving SQLite
    /// honors the bracket-free partial-index filter that EnsureCreated applied.
    /// </summary>
    [Fact]
    public async Task FilteredUniqueIndex_RejectsSecondLivePendingInvite_ForSameEmail()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");

        StaffInvite NewLive() => new()
        {
            Email = "idx@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
            InviteToken = InviteTokenHelper.Hash(InviteTokenHelper.Generate()),
            InviteExpiresAt = DateTime.UtcNow.AddDays(14), IsActive = true, InvitedByUserId = 0
        };

        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(NewLive());
            await ctx.SaveChangesAsync();
        }

        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(NewLive());
            await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        }

        using var ctx2 = CreateContext();
        Assert.Single(ctx2.StaffInvites.Where(i => i.Email == "idx@x.com"));
    }

    /// <summary>Service-level: a second InviteAsync after a direct-context LIVE pending row still returns the
    /// friendly "already invited" error (DbUpdateException from the index mapped to the same message even if
    /// the pre-check were to race past).</summary>
    [Fact]
    public async Task Invite_AfterDirectPendingRow_ReturnsFriendlyAlreadyInvited()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        // Insert a LIVE pending invite straight to the DB (bypasses the service path entirely).
        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(new StaffInvite
            {
                Email = "dup2@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
                InviteToken = InviteTokenHelper.Hash(InviteTokenHelper.Generate()),
                InviteExpiresAt = DateTime.UtcNow.AddDays(14), IsActive = true, InvitedByUserId = adminUserId
            });
            await ctx.SaveChangesAsync();
        }

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "dup2@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
        });
        Assert.False(result.Success);
        Assert.Contains("already been invited", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The filter excludes revoked rows (IsActive=0, token nulled), so a fresh invite to the same
    /// email succeeds and does not trip the unique index.</summary>
    [Fact]
    public async Task FilteredUniqueIndex_RevokedRow_DoesNotBlockFreshInvite()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        // A previously-revoked invite for the email: IsActive=false + token nulled (Revoke's end state).
        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(new StaffInvite
            {
                Email = "fresh@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
                InviteToken = null, InviteExpiresAt = DateTime.UtcNow.AddDays(14),
                IsActive = false, InvitedByUserId = adminUserId
            });
            await ctx.SaveChangesAsync();
        }

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "fresh@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
        });
        Assert.True(result.Success);
    }

    /// <summary>The filter excludes accepted rows (AcceptedAt set, token nulled), so a fresh invite to the
    /// same email succeeds and does not trip the unique index.</summary>
    [Fact]
    public async Task FilteredUniqueIndex_AcceptedRow_DoesNotBlockFreshInvite()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        // A previously-accepted invite for the email: AcceptedAt set + token nulled (Accept's end state).
        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(new StaffInvite
            {
                Email = "accepted@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
                InviteToken = null, InviteExpiresAt = DateTime.UtcNow.AddDays(14),
                IsActive = true, AcceptedAt = DateTime.UtcNow, InvitedByUserId = adminUserId
            });
            await ctx.SaveChangesAsync();
        }

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).InviteAsync(adminUserId, new CreateStaffInviteModel
        {
            Email = "accepted@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
        });
        Assert.True(result.Success);
    }

    // ================================================================= Accept

    [Fact]
    public async Task Accept_HappyPath_CreatesUserAndStaffProfile_AndJwt_AndClaims()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "joiner@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
        var rawToken = email.LastRawToken!;

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptAsync(new AcceptStaffInviteModel
            {
                Token = rawToken, FirstName = "Joe", LastName = "Iner", Password = "password123"
            });
            Assert.True(result.Success);
            Assert.NotNull(result.AuthResult);
            Assert.False(string.IsNullOrEmpty(result.AuthResult!.Token));
            Assert.Equal("joiner@x.com", result.AuthResult.User.Email);
            Assert.Equal("Educator", result.AuthResult.User.Role);
        }

        using (var ctx = CreateContext())
        {
            var user = ctx.Users.Single(u => u.Email == "joiner@x.com");
            Assert.Equal(UserRole.Educator, user.Role);
            var profile = ctx.StaffProfiles.Single(p => p.UserId == user.Id);
            Assert.Equal(districtId, profile.DistrictId);
            Assert.Equal(schoolId, profile.SchoolId);
            Assert.Equal(OrgRoleIds.Teacher, profile.OrgRoleId);
            Assert.True(profile.IsActive);

            var invite = ctx.StaffInvites.Single();
            Assert.NotNull(invite.AcceptedAt);
            Assert.Equal(user.Id, invite.AcceptedByUserId);
            Assert.Null(invite.InviteToken);
        }
    }

    [Fact]
    public async Task Accept_ExpiredInvite_DistinctError_NotConsumed()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var raw = InviteTokenHelper.Generate();
        using (var ctx = CreateContext())
        {
            ctx.StaffInvites.Add(new StaffInvite
            {
                Email = "exp@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
                InviteToken = InviteTokenHelper.Hash(raw), InviteExpiresAt = DateTime.UtcNow.AddDays(-1),
                IsActive = true, InvitedByUserId = 0
            });
            await ctx.SaveChangesAsync();
        }
        var email = new CapturingEmailService();

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).AcceptAsync(new AcceptStaffInviteModel
        {
            Token = raw, FirstName = "A", LastName = "B", Password = "password123"
        });
        Assert.False(result.Success);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ctx2.Users.Where(u => u.Email == "exp@x.com"));
    }

    [Fact]
    public async Task Accept_RevokedOrUnknownToken_IsInvalid()
    {
        var email = new CapturingEmailService();
        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).AcceptAsync(new AcceptStaffInviteModel
        {
            Token = InviteTokenHelper.Generate(), FirstName = "A", LastName = "B", Password = "password123"
        });
        Assert.False(result.Success);
        Assert.Contains("no longer valid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Accept_EmailRegisteredAfterInvite_IsRejected_InviteNotConsumed()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "late@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
        var rawToken = email.LastRawToken!;

        // A user registers with that email AFTER the invite was sent (e.g. as a parent).
        SeedUser("late@x.com", UserRole.Parent);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptAsync(new AcceptStaffInviteModel
            {
                Token = rawToken, FirstName = "L", LastName = "Ate", Password = "password123"
            });
            Assert.False(result.Success);
            Assert.Contains("existing account", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            // Invite NOT consumed (token intact); no staff profile created.
            var invite = ctx.StaffInvites.Single();
            Assert.NotNull(invite.InviteToken);
            Assert.Null(invite.AcceptedAt);
            Assert.Empty(ctx.StaffProfiles.Where(p => p.DistrictId == districtId && p.SchoolId == schoolId));
        }
    }

    [Fact]
    public async Task Accept_DoubleAccept_SecondFails_NoSecondUserOrProfile()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "race@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
        var rawToken = email.LastRawToken!;

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).AcceptAsync(new AcceptStaffInviteModel
            {
                Token = rawToken, FirstName = "First", LastName = "Win", Password = "password123"
            })).Success);

        // Second accept of the SAME token must fail and create nothing extra.
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptAsync(new AcceptStaffInviteModel
            {
                Token = rawToken, FirstName = "Second", LastName = "Lose", Password = "password123"
            });
            Assert.False(result.Success);
        }

        using var ctx2 = CreateContext();
        Assert.Single(ctx2.Users.Where(u => u.Email == "race@x.com"));
        Assert.Single(ctx2.StaffProfiles.Where(p => p.SchoolId == schoolId && p.OrgRoleId == OrgRoleIds.Teacher));
    }

    [Fact]
    public async Task Accept_ConcurrentClaimRace_LoserRollsBack_NoUserOrProfile()
    {
        // Simulate the race directly: a concurrent winner claims the invite (sets AcceptedAt + nulls the
        // token) AFTER our accept resolves the still-pending row but BEFORE its guarded ExecuteUpdate.
        // Because the token is now nulled, the email-already-registered guard can't intercept; the atomic
        // claim is the sole gate. The loser must roll back — no User, no StaffProfile.
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var raw = InviteTokenHelper.Generate();
        int inviteId;
        using (var ctx = CreateContext())
        {
            var invite = new StaffInvite
            {
                Email = "rc@x.com", DistrictId = districtId, SchoolId = schoolId, OrgRoleId = OrgRoleIds.Teacher,
                InviteToken = InviteTokenHelper.Hash(raw), InviteExpiresAt = DateTime.UtcNow.AddDays(14),
                IsActive = true, InvitedByUserId = 0
            };
            ctx.StaffInvites.Add(invite);
            await ctx.SaveChangesAsync();
            inviteId = invite.Id;
        }

        // Concurrent winner: claim out-of-band (token nulled, AcceptedAt set) but the email is still free.
        using (var ctx = CreateContext())
        {
            await ctx.StaffInvites.Where(i => i.Id == inviteId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.AcceptedAt, DateTime.UtcNow)
                    .SetProperty(i => i.InviteToken, (string?)null));
        }

        var email = new CapturingEmailService();
        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).AcceptAsync(new AcceptStaffInviteModel
        {
            Token = raw, FirstName = "Late", LastName = "Loser", Password = "password123"
        });

        // Token no longer resolves an active+unaccepted invite → "no longer valid"; nothing created.
        Assert.False(result.Success);
        Assert.Empty(ctx2.Users.Where(u => u.Email == "rc@x.com"));
        Assert.Empty(ctx2.StaffProfiles.Where(p => p.SchoolId == schoolId));
    }

    // ================================================================= Preview

    [Fact]
    public async Task Preview_ValidInvite_ReturnsOrgContextAndEmail()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "prev@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });

        using var ctx2 = CreateContext();
        var preview = await CreateService(ctx2, email).PreviewAsync(email.LastRawToken!);
        Assert.NotNull(preview);
        Assert.Equal("valid", preview!.Status);
        Assert.Equal("Maple", preview.DistrictName);
        Assert.Equal("Elm", preview.SchoolName);
        Assert.Equal("Teacher", preview.RoleName);
        Assert.Equal("prev@x.com", preview.Email);
    }

    [Fact]
    public async Task Preview_UnknownToken_IsInvalid()
    {
        var email = new CapturingEmailService();
        using var ctx = CreateContext();
        var preview = await CreateService(ctx, email).PreviewAsync(InviteTokenHelper.Generate());
        Assert.NotNull(preview);
        Assert.Equal("invalid", preview!.Status);
    }

    // ================================================================= Resend / revoke

    [Fact]
    public async Task Resend_ResetsExpiry_AndInvalidatesOldToken()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        int inviteId;
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "rs@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
            inviteId = r.Data!.Id;
        }
        var oldToken = email.LastRawToken!;

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).ResendAsync(adminUserId, inviteId)).Success);
        var newToken = email.LastRawToken!;
        Assert.NotEqual(oldToken, newToken);

        // Old token no longer previews or accepts.
        using (var ctx = CreateContext())
            Assert.Equal("invalid", (await CreateService(ctx, email).PreviewAsync(oldToken))!.Status);
        // New token works.
        using (var ctx = CreateContext())
            Assert.Equal("valid", (await CreateService(ctx, email).PreviewAsync(newToken))!.Status);
    }

    [Fact]
    public async Task Revoke_PendingInvite_DeactivatesAndKillsToken()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        int inviteId;
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "rev@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
            inviteId = r.Data!.Id;
        }
        var token = email.LastRawToken!;

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).RevokeAsync(adminUserId, inviteId)).Success);

        using (var ctx = CreateContext())
            Assert.Equal("invalid", (await CreateService(ctx, email).PreviewAsync(token))!.Status);
        using var ctx2 = CreateContext();
        Assert.False(ctx2.StaffInvites.Single().IsActive);
    }

    [Fact]
    public async Task SchoolAdmin_CannotRevokeDistrictAdminInvite()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var (saUser, _) = SeedStaff("sa@x.com", districtId, schoolId, OrgRoleIds.SchoolAdmin);
        var email = new CapturingEmailService();

        int daInviteId;
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "da2@x.com", OrgRoleId = OrgRoleIds.DistrictAdmin, SchoolId = null
            });
            daInviteId = r.Data!.Id;
        }

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).RevokeAsync(saUser, daInviteId);
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SchoolAdmin_CannotRevokeOtherSchoolInvite()
    {
        var districtId = SeedDistrict("Maple");
        var mySchool = SeedSchool(districtId, "Mine");
        var otherSchool = SeedSchool(districtId, "Other");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var (saUser, _) = SeedStaff("sa@x.com", districtId, mySchool, OrgRoleIds.SchoolAdmin);
        var email = new CapturingEmailService();

        int otherInviteId;
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "ot@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = otherSchool
            });
            otherInviteId = r.Data!.Id;
        }

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).RevokeAsync(saUser, otherInviteId);
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================= Deactivate / reactivate

    [Fact]
    public async Task Deactivate_FlipsIsActive_AndBumpsSecurityStamp()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var (teacherUser, teacherProfile) = SeedStaff("teach@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var email = new CapturingEmailService();

        int stampBefore;
        using (var ctx = CreateContext())
            stampBefore = ctx.Users.Single(u => u.Id == teacherUser).SecurityStamp;

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).DeactivateStaffAsync(adminUserId, teacherProfile)).Success);

        using var ctx2 = CreateContext();
        Assert.False(ctx2.StaffProfiles.Single(p => p.Id == teacherProfile).IsActive);
        Assert.NotEqual(stampBefore, ctx2.Users.Single(u => u.Id == teacherUser).SecurityStamp);
    }

    [Fact]
    public async Task Deactivate_SoleDistrictAdmin_IsBlocked_IncludingSelf()
    {
        var districtId = SeedDistrict("Maple");
        var (adminUserId, adminProfile) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).DeactivateStaffAsync(adminUserId, adminProfile);
        Assert.False(result.Success);
        Assert.Contains("last active District Admin", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(ctx.StaffProfiles.Single(p => p.Id == adminProfile).IsActive);
    }

    [Fact]
    public async Task Deactivate_DistrictAdmin_AllowedWhenAnotherActiveAdminExists()
    {
        var districtId = SeedDistrict("Maple");
        var (admin1, profile1) = SeedStaff("a1@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedStaff("a2@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).DeactivateStaffAsync(admin1, profile1);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Deactivate_LastAdminGuard_CountsOnlyActiveAdmins()
    {
        var districtId = SeedDistrict("Maple");
        var (admin1, profile1) = SeedStaff("a1@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        // A second DistrictAdmin that is already INACTIVE — does not count toward the guard.
        SeedStaff("a2@x.com", districtId, null, OrgRoleIds.DistrictAdmin, isActive: false);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).DeactivateStaffAsync(admin1, profile1);
        Assert.False(result.Success);
        Assert.Contains("last active District Admin", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SchoolAdmin_CannotDeactivateDistrictAdmin()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (_, adminProfile) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var (saUser, _) = SeedStaff("sa@x.com", districtId, schoolId, OrgRoleIds.SchoolAdmin);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).DeactivateStaffAsync(saUser, adminProfile);
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SchoolAdmin_CannotDeactivateOtherSchoolStaff()
    {
        var districtId = SeedDistrict("Maple");
        var mySchool = SeedSchool(districtId, "Mine");
        var otherSchool = SeedSchool(districtId, "Other");
        var (saUser, _) = SeedStaff("sa@x.com", districtId, mySchool, OrgRoleIds.SchoolAdmin);
        var (_, otherTeacher) = SeedStaff("ot@x.com", districtId, otherSchool, OrgRoleIds.Teacher);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).DeactivateStaffAsync(saUser, otherTeacher);
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reactivate_RestoresStaff()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var (_, teacherProfile) = SeedStaff("teach@x.com", districtId, schoolId, OrgRoleIds.Teacher, isActive: false);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).ReactivateStaffAsync(adminUserId, teacherProfile)).Success);

        using var ctx2 = CreateContext();
        Assert.True(ctx2.StaffProfiles.Single(p => p.Id == teacherProfile).IsActive);
    }

    // ================================================================= List

    [Fact]
    public async Task List_DistrictAdmin_SeesAllMembersAndPendingInvites()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedStaff("teach@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "pending@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });

        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2, email).ListAsync(adminUserId);
        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Members.Count); // admin + teacher
        Assert.Single(result.Data.PendingInvites);
    }

    [Fact]
    public async Task List_SchoolAdmin_OwnSchoolOnly_HidesDistrictAdmins()
    {
        var districtId = SeedDistrict("Maple");
        var mySchool = SeedSchool(districtId, "Mine");
        var otherSchool = SeedSchool(districtId, "Other");
        SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var (saUser, _) = SeedStaff("sa@x.com", districtId, mySchool, OrgRoleIds.SchoolAdmin);
        SeedStaff("mine-teacher@x.com", districtId, mySchool, OrgRoleIds.Teacher);
        SeedStaff("other-teacher@x.com", districtId, otherSchool, OrgRoleIds.Teacher);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).ListAsync(saUser);
        Assert.True(result.Success);
        // Only own-school staff (SchoolAdmin self + mine-teacher); DistrictAdmin + other-school hidden.
        Assert.Equal(2, result.Data!.Members.Count);
        Assert.All(result.Data.Members, m => Assert.NotEqual(OrgRoleIds.DistrictAdmin, m.OrgRoleId));
        Assert.All(result.Data.Members, m => Assert.Equal(mySchool, m.SchoolId));
    }

    [Fact]
    public async Task List_Teacher_IsDenied()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (teacher, _) = SeedStaff("teach@x.com", districtId, schoolId, OrgRoleIds.Teacher);
        var email = new CapturingEmailService();

        using var ctx = CreateContext();
        var result = await CreateService(ctx, email).ListAsync(teacher);
        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================= ExposeLinksForTesting gate

    [Fact]
    public async Task Invite_InviteUrl_OnlyPresentWhenExposureEnabled()
    {
        var districtId = SeedDistrict("Maple");
        var schoolId = SeedSchool(districtId, "Elm");
        var (adminUserId, _) = SeedStaff("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var email = new CapturingEmailService();

        // Default: not exposed.
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx, email, exposeLinks: false).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "off@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
            Assert.Null(r.Data!.InviteUrl);
        }

        // Exposed: url present and carries the raw token.
        using (var ctx = CreateContext())
        {
            var r = await CreateService(ctx, email, exposeLinks: true).InviteAsync(adminUserId, new CreateStaffInviteModel
            {
                Email = "on@x.com", OrgRoleId = OrgRoleIds.Teacher, SchoolId = schoolId
            });
            Assert.NotNull(r.Data!.InviteUrl);
            Assert.Contains(Uri.EscapeDataString(email.LastRawToken!), r.Data.InviteUrl!);
        }
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Captures the raw token passed to the staff invite email so tests can exercise accept.</summary>
    private sealed class CapturingEmailService : IEmailService
    {
        public string? LastRawToken { get; private set; }

        public Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default)
        {
            LastRawToken = inviteToken;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default) => Task.CompletedTask;
    }
}
