using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// P3c coverage for the parent<->school invite/link flow: hashed single-use tokens, idempotent
/// invites, match-or-create accept (link existing vs create new), email binding, permission checks,
/// and forward-only revoke. Uses a real SQLite in-memory engine (same pattern as
/// <see cref="EducatorServiceTests"/>).
/// </summary>
public sealed class ChildLinkServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ChildLinkServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static ChildLinkService CreateService(ApplicationDbContext ctx, CapturingEmailService email)
        => new(ctx, new AccessService(ctx), email, new CapturingAuditLogger(), NullLogger<ChildLinkService>.Instance);

    private static EducatorService CreateEducator(ApplicationDbContext ctx)
        => new(ctx, NullLogger<EducatorService>.Instance);

    // ----------------------------------------------------------------- seed helpers

    private int SeedUser(string email, UserRole role = UserRole.Parent)
    {
        using var ctx = CreateContext();
        var user = new User { Email = email, PasswordHash = "x", FirstName = "First", LastName = "Last", Role = role };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    /// <summary>Onboards an educator and creates a student under their school; returns (educatorUserId, studentId).</summary>
    private async Task<(int educatorId, int studentId)> SeedEducatorWithStudent(
        string educatorEmail, string district, string school, string studentFirst = "Sam", string studentLast = "Student")
    {
        var educatorId = SeedUser(educatorEmail);
        int studentId;
        using (var ctx = CreateContext())
        {
            await CreateEducator(ctx).OnboardAsync(educatorId, new OnboardEducatorModel
            {
                DistrictName = district, SchoolName = school, StateCode = "OH"
            });
        }
        using (var ctx = CreateContext())
        {
            var created = await CreateEducator(ctx).CreateStudentAsync(educatorId, new CreateSchoolStudentModel
            {
                FirstName = studentFirst, LastName = studentLast, GradeLevel = "5", DisabilityCategory = "SLD"
            });
            studentId = created.Data!.Id;
        }
        return (educatorId, studentId);
    }

    private async Task<int> SeedOwnedChild(int parentUserId, string firstName)
    {
        using var ctx = CreateContext();
        var child = new ChildProfile { UserId = parentUserId, FirstName = firstName, IsActive = true };
        ctx.ChildProfiles.Add(child);
        await ctx.SaveChangesAsync();
        ctx.ChildAccesses.Add(new ChildAccess
        {
            ChildProfileId = child.Id, UserId = parentUserId, Role = AccessRole.Owner,
            AcceptedAt = DateTime.UtcNow, IsActive = true
        });
        await ctx.SaveChangesAsync();
        return child.Id;
    }

    /// <summary>Inserts an extra pending ChildLink invite directly (hashed token) and returns the raw token.</summary>
    private string SeedExtraPendingInvite(int studentId, string parentEmail)
    {
        var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        using var ctx = CreateContext();
        ctx.ChildLinks.Add(new ChildLink
        {
            SchoolStudentId = studentId,
            InviteEmail = parentEmail,
            InviteToken = hash,
            InviteExpiresAt = DateTime.UtcNow.AddDays(14),
            IsActive = true
        });
        ctx.SaveChanges();
        return rawToken;
    }

    // ----------------------------------------------------------------- tests

    [Fact]
    public async Task Invite_CreatesPendingLink_WithHashedToken()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed1@x.com", "D1", "S1");
        SeedUser("parent1@x.com");
        var email = new CapturingEmailService();

        ChildLinkModel? model;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "parent1@x.com");
            Assert.True(result.Success);
            model = result.Data;
        }

        Assert.NotNull(model);
        Assert.False(model!.IsAccepted);
        Assert.NotNull(email.LastRawToken);

        using (var ctx = CreateContext())
        {
            var link = ctx.ChildLinks.Single(l => l.SchoolStudentId == studentId);
            Assert.Null(link.ChildProfileId);
            Assert.Null(link.AcceptedAt);
            Assert.True(link.IsActive);
            // Stored token is the HASH, never the raw token.
            Assert.NotNull(link.InviteToken);
            Assert.NotEqual(email.LastRawToken, link.InviteToken);
        }
    }

    [Fact]
    public async Task Invite_Duplicate_ForSameStudentAndEmail_IsRejected()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed2@x.com", "D2", "S2");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "dup@x.com")).Success);

        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "dup@x.com");
            Assert.False(second.Success);
            Assert.Contains("pending invite", second.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Single(ctx.ChildLinks);
    }

    [Fact]
    public async Task Accept_WithLinkToExistingOwnedChild_LinksAndCreatesNoNewChild()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed3@x.com", "D3", "S3");
        var parentId = SeedUser("p3@x.com");
        var existingChildId = await SeedOwnedChild(parentId, "Existing");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p3@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(parentId, email.LastRawToken!, existingChildId);
            Assert.True(result.Success);
            Assert.Equal(existingChildId, result.Data!.ChildProfileId);
        }

        using (var ctx = CreateContext())
        {
            // Exactly one child for this parent (no new ChildProfile created).
            Assert.Single(ctx.ChildProfiles.Where(c => c.UserId == parentId));
            var link = ctx.ChildLinks.Single();
            Assert.Equal(existingChildId, link.ChildProfileId);
            Assert.NotNull(link.AcceptedAt);
            Assert.NotNull(link.LinkedAt);
            Assert.Null(link.InviteToken); // single-use, cleared
        }
    }

    [Fact]
    public async Task Accept_WithoutLinkTarget_CreatesNewChildAndOwnerAccess_CopiedFromStudent()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed4@x.com", "D4", "S4", "Copy", "Kid");
        var parentId = SeedUser("p4@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p4@x.com");

        int newChildId;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(parentId, email.LastRawToken!, null);
            Assert.True(result.Success);
            newChildId = result.Data!.ChildProfileId!.Value;
        }

        using (var ctx = CreateContext())
        {
            var child = ctx.ChildProfiles.Single(c => c.Id == newChildId);
            Assert.Equal(parentId, child.UserId);
            Assert.Equal("Copy", child.FirstName);
            Assert.Equal("Kid", child.LastName);
            Assert.Equal("SLD", child.DisabilityCategory);

            var access = ctx.ChildAccesses.Single(ca => ca.ChildProfileId == newChildId);
            Assert.Equal(parentId, access.UserId);
            Assert.Equal(AccessRole.Owner, access.Role);
            Assert.NotNull(access.AcceptedAt);

            var link = ctx.ChildLinks.Single();
            Assert.Equal(newChildId, link.ChildProfileId);
            Assert.Null(link.InviteToken);
        }
    }

    [Fact]
    public async Task Accept_WithWrongEmailUser_IsRejected()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed5@x.com", "D5", "S5");
        SeedUser("invited@x.com");
        var wrongUserId = SeedUser("wrong@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "invited@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(wrongUserId, email.LastRawToken!, null);
            Assert.False(result.Success);
            Assert.Contains("different email", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Empty(ctx.ChildProfiles); // nothing created
    }

    [Fact]
    public async Task Accept_Twice_IsIdempotent_OneLink_NoDuplicateChild()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed6@x.com", "D6", "S6");
        var parentId = SeedUser("p6@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p6@x.com");

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).AcceptInviteAsync(parentId, email.LastRawToken!, null)).Success);

        // Idempotency contract: "accept resolves to exactly one link". Simulate a SECOND pending invite
        // for the same (student, parent) — e.g. a duplicate invite that slipped through — and accept it.
        // The accept-side idempotency must return success WITHOUT creating a second link or child.
        var secondToken = SeedExtraPendingInvite(studentId, "p6@x.com");
        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx, email).AcceptInviteAsync(parentId, secondToken, null);
            Assert.True(second.Success);
            Assert.Contains("Already linked", second.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            Assert.Single(ctx.ChildProfiles.Where(c => c.UserId == parentId));
            Assert.Single(ctx.ChildLinks.Where(l => l.AcceptedAt != null && l.ChildProfileId != null));
        }
    }

    [Fact]
    public async Task Invite_WhenAlreadyActivelyLinkedToParent_IsRejected()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed6b@x.com", "D6b", "S6b");
        var parentId = SeedUser("p6b@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p6b@x.com");
        using (var ctx = CreateContext())
            await CreateService(ctx, email).AcceptInviteAsync(parentId, email.LastRawToken!, null);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p6b@x.com");
            Assert.False(result.Success);
            Assert.Contains("already linked", result.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Accept_LinkToChildParentDoesNotOwn_IsRejected()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed7@x.com", "D7", "S7");
        var parentId = SeedUser("p7@x.com");
        var otherParentId = SeedUser("other7@x.com");
        var notMineChildId = await SeedOwnedChild(otherParentId, "NotMine");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p7@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(parentId, email.LastRawToken!, notMineChildId);
            Assert.False(result.Success);
            Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var link = ctx.ChildLinks.Single();
            Assert.Null(link.AcceptedAt); // not accepted
        }
    }

    [Fact]
    public async Task Revoke_SetsInactive_AndRevokedLinkCannotBeAccepted()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed8@x.com", "D8", "S8");
        var parentId = SeedUser("p8@x.com");
        var email = new CapturingEmailService();

        int linkId;
        using (var ctx = CreateContext())
        {
            var invite = await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p8@x.com");
            linkId = invite.Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).RevokeLinkAsync(educatorId, studentId, linkId);
            Assert.True(result.Success);
        }

        using (var ctx = CreateContext())
            Assert.False(ctx.ChildLinks.Single(l => l.Id == linkId).IsActive);

        // A revoked (inactive) invite cannot be accepted.
        using (var ctx = CreateContext())
        {
            var accept = await CreateService(ctx, email).AcceptInviteAsync(parentId, email.LastRawToken!, null);
            Assert.False(accept.Success);
            Assert.Contains("Invalid or expired", accept.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Invite_FromEducatorInAnotherSchool_IsRejected()
    {
        var (educatorA, studentInA) = await SeedEducatorWithStudent("edA@x.com", "DistrictA", "SchoolA");
        var educatorB = SeedUser("edB@x.com");
        using (var ctx = CreateContext())
            await CreateEducator(ctx).OnboardAsync(educatorB, new OnboardEducatorModel
            {
                DistrictName = "DistrictB", SchoolName = "SchoolB", StateCode = "OH"
            });
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteParentAsync(educatorB, studentInA, "parent@x.com");
            Assert.False(result.Success);
            Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Empty(ctx.ChildLinks);
    }

    [Fact]
    public async Task Revoke_FromEducatorInAnotherSchool_IsRejected()
    {
        var (educatorA, studentInA) = await SeedEducatorWithStudent("edA2@x.com", "DistrictA2", "SchoolA2");
        var educatorB = SeedUser("edB2@x.com");
        using (var ctx = CreateContext())
            await CreateEducator(ctx).OnboardAsync(educatorB, new OnboardEducatorModel
            {
                DistrictName = "DistrictB2", SchoolName = "SchoolB2", StateCode = "OH"
            });
        var email = new CapturingEmailService();

        int linkId;
        using (var ctx = CreateContext())
            linkId = (await CreateService(ctx, email).InviteParentAsync(educatorA, studentInA, "parent2@x.com")).Data!.Id;

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).RevokeLinkAsync(educatorB, studentInA, linkId);
            Assert.False(result.Success);
            Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.True(ctx.ChildLinks.Single(l => l.Id == linkId).IsActive); // untouched
    }

    [Fact]
    public async Task Revoke_WithMismatchedStudentId_IsRejected()
    {
        // Two students in the SAME school; revoking link of student A via student B's route id must fail.
        var (educatorId, studentA) = await SeedEducatorWithStudent("ed8b@x.com", "D8b", "S8b");
        var email = new CapturingEmailService();
        int studentB, linkId;
        using (var ctx = CreateContext())
        {
            var ed = CreateEducator(ctx);
            studentB = (await ed.CreateStudentAsync(educatorId,
                new CreateSchoolStudentModel { FirstName = "Bee" })).Data!.Id;
            linkId = (await CreateService(ctx, email).InviteParentAsync(educatorId, studentA, "p8b@x.com")).Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            // linkId belongs to studentA but the route says studentB.
            var result = await CreateService(ctx, email).RevokeLinkAsync(educatorId, studentB, linkId);
            Assert.False(result.Success);
            Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.True(ctx.ChildLinks.Single(l => l.Id == linkId).IsActive); // untouched
    }

    [Fact]
    public async Task Preview_ReturnsStudentInfo_AndParentsOwnedChildren()
    {
        var (educatorId, studentId) = await SeedEducatorWithStudent("ed9@x.com", "D9", "S9", "Prev", "Iew");
        var parentId = SeedUser("p9@x.com");
        await SeedOwnedChild(parentId, "Mine1");
        await SeedOwnedChild(parentId, "Mine2");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteParentAsync(educatorId, studentId, "p9@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).PreviewInviteAsync(parentId, email.LastRawToken!);
            Assert.True(result.Success);
            Assert.Equal("Prev", result.Data!.StudentFirstName);
            Assert.Equal("S9", result.Data.SchoolName);
            Assert.Equal(2, result.Data.ExistingChildren.Count);
        }
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Captures the raw token passed to the email so tests can exercise the accept path.</summary>
    private sealed class CapturingEmailService : IEmailService
    {
        public string? LastRawToken { get; private set; }

        public Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default)
        {
            LastRawToken = inviteToken;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default) => Task.CompletedTask;
    }
}
