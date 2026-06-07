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
/// P7a coverage for the student role + invite + consent flow: the consent gate, the parent/educator
/// happy paths, dual-invite convergence onto a single StudentProfile, the one-pair guard, email binding,
/// idempotent invites, and SchoolId-bound educator permission. Uses a real SQLite in-memory engine
/// (same pattern as <see cref="ChildLinkServiceTests"/>).
/// </summary>
public sealed class StudentInviteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public StudentInviteServiceTests()
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

    private StudentInviteService CreateService(ApplicationDbContext ctx, CapturingEmailService email)
        => new(ctx, new AccessService(ctx), new OrgAccessService(ctx), email, NullLogger<StudentInviteService>.Instance);

    private static EducatorService CreateEducator(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), NullLogger<EducatorService>.Instance);

    // ----------------------------------------------------------------- seed helpers

    private int SeedUser(string email, UserRole role = UserRole.Parent)
    {
        using var ctx = CreateContext();
        var user = new User { Email = email, PasswordHash = "x", FirstName = "First", LastName = "Last", Role = role };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
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

    private async Task<(int educatorId, int studentId)> SeedEducatorWithStudent(
        string educatorEmail, string district, string school, string studentFirst = "Sam", string studentLast = "Student")
    {
        var educatorId = SeedUser(educatorEmail);
        int studentId;
        using (var ctx = CreateContext())
            await CreateEducator(ctx).OnboardAsync(educatorId, new OnboardEducatorModel
            {
                DistrictName = district, SchoolName = school, StateCode = "OH"
            });
        using (var ctx = CreateContext())
        {
            var created = await CreateEducator(ctx).CreateStudentAsync(educatorId, new CreateSchoolStudentModel
            {
                FirstName = studentFirst, LastName = studentLast, GradeLevel = "9", DisabilityCategory = "SLD"
            });
            studentId = created.Data!.Id;
        }
        return (educatorId, studentId);
    }

    // ----------------------------------------------------------------- consent gate

    [Fact]
    public async Task Accept_WithConsentFalse_DoesNotActivate()
    {
        var parentId = SeedUser("p@x.com");
        var childId = await SeedOwnedChild(parentId, "Kid");
        var studentId = SeedUser("student@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childId, "student@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(studentId, email.LastRawToken!, consentAccepted: false);
            Assert.False(result.Success);
            Assert.Contains("Consent is required", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            // Role NOT flipped; no profile / no consent recorded; invite still pending (not consumed).
            Assert.Equal(UserRole.Parent, ctx.Users.Single(u => u.Id == studentId).Role);
            Assert.Empty(ctx.StudentProfiles);
            Assert.NotNull(ctx.StudentInvites.Single().InviteToken);
            Assert.Null(ctx.StudentInvites.Single().AcceptedAt);
        }
    }

    // ----------------------------------------------------------------- happy path (parent)

    [Fact]
    public async Task Accept_ParentInvite_WithConsent_ActivatesAndLinks()
    {
        var parentId = SeedUser("p2@x.com");
        var childId = await SeedOwnedChild(parentId, "Kid2");
        var studentId = SeedUser("student2@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).InviteFromParentAsync(parentId, childId, "student2@x.com")).Success);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(studentId, email.LastRawToken!, consentAccepted: true);
            Assert.True(result.Success);
            Assert.Equal(childId, result.Data!.ChildProfileId);
        }

        using (var ctx = CreateContext())
        {
            Assert.Equal(UserRole.Student, ctx.Users.Single(u => u.Id == studentId).Role);
            var profile = ctx.StudentProfiles.Single(p => p.UserId == studentId);
            Assert.Equal(childId, profile.ChildProfileId);
            Assert.Null(profile.SchoolStudentId);
            Assert.NotNull(profile.ConsentAcceptedAt);
            // single-use token cleared
            Assert.Null(ctx.StudentInvites.Single().InviteToken);
            Assert.NotNull(ctx.StudentInvites.Single().AcceptedAt);
        }
    }

    // ----------------------------------------------------------------- dual-invite convergence

    [Fact]
    public async Task DualInvite_SameStudent_ConvergesOnOneProfile_WithBothLinks()
    {
        // Parent side
        var parentId = SeedUser("dpar@x.com");
        var childA = await SeedOwnedChild(parentId, "ChildA");
        // Educator side
        var (educatorId, schoolStudentB) = await SeedEducatorWithStudent("ded@x.com", "DistD", "SchoolD");
        // The student user (same email targeted by both invites)
        var studentId = SeedUser("dual@x.com");
        var email = new CapturingEmailService();

        // Parent invites + student accepts
        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childA, "dual@x.com");
        var parentToken = email.LastRawToken!;
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).AcceptInviteAsync(studentId, parentToken, true)).Success);

        // The two sides describe the same real student — pair them with an active ChildLink
        // (the same-person guard requires this before a workspace links both sides).
        using (var ctx = CreateContext())
        {
            ctx.ChildLinks.Add(new ChildLink
            {
                ChildProfileId = childA,
                SchoolStudentId = schoolStudentB,
                IsActive = true,
                AcceptedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        // Educator invites + student accepts
        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromEducatorAsync(educatorId, schoolStudentB, "dual@x.com");
        var educatorToken = email.LastRawToken!;
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).AcceptInviteAsync(studentId, educatorToken, true)).Success);

        using (var ctx = CreateContext())
        {
            // Exactly ONE StudentProfile, carrying BOTH sides.
            var profile = Assert.Single(ctx.StudentProfiles.Where(p => p.UserId == studentId));
            Assert.Equal(childA, profile.ChildProfileId);
            Assert.Equal(schoolStudentB, profile.SchoolStudentId);
            Assert.NotNull(profile.ConsentAcceptedAt);
        }
    }

    // ----------------------------------------------------------------- one-pair guard

    [Fact]
    public async Task SecondParentInvite_ForDifferentChild_IsRejected_ProfileUnchanged()
    {
        var parentId = SeedUser("opg@x.com");
        var childA = await SeedOwnedChild(parentId, "FirstChild");
        var childB = await SeedOwnedChild(parentId, "SecondChild");
        var studentId = SeedUser("opgstudent@x.com");
        var email = new CapturingEmailService();

        // Accept the first child invite.
        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childA, "opgstudent@x.com");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).AcceptInviteAsync(studentId, email.LastRawToken!, true)).Success);

        // A second invite for a DIFFERENT child, accepted by the same student → rejected.
        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childB, "opgstudent@x.com");
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(studentId, email.LastRawToken!, true);
            Assert.False(result.Success);
            Assert.Contains("already linked to a different child", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var profile = ctx.StudentProfiles.Single(p => p.UserId == studentId);
            Assert.Equal(childA, profile.ChildProfileId); // unchanged
        }
    }

    [Fact]
    public async Task SecondSide_ForUnrelatedStudent_IsRejected_WhenNotPaired()
    {
        // Student account already linked to childA (parent side). An educator at an unrelated
        // school invites for schoolStudentB — a DIFFERENT real student with no ChildLink to childA.
        // The same-person guard must reject, preventing two unrelated records fusing on one workspace.
        var parentId = SeedUser("usp@x.com");
        var childA = await SeedOwnedChild(parentId, "RealChild");
        var (educatorId, schoolStudentB) = await SeedEducatorWithStudent("used@x.com", "DistU", "SchoolU");
        var studentId = SeedUser("usstudent@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childA, "usstudent@x.com");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx, email).AcceptInviteAsync(studentId, email.LastRawToken!, true)).Success);

        // No ChildLink pairs childA and schoolStudentB → educator invite accept must be rejected.
        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromEducatorAsync(educatorId, schoolStudentB, "usstudent@x.com");
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(studentId, email.LastRawToken!, true);
            Assert.False(result.Success);
            Assert.Contains("different student", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var profile = ctx.StudentProfiles.Single(p => p.UserId == studentId);
            Assert.Null(profile.SchoolStudentId); // unrelated side not linked
        }
    }

    // ----------------------------------------------------------------- email mismatch

    [Fact]
    public async Task Accept_WithWrongEmailUser_IsRejected()
    {
        var parentId = SeedUser("emp@x.com");
        var childId = await SeedOwnedChild(parentId, "Kid");
        SeedUser("invited@x.com");
        var wrongUserId = SeedUser("wrong@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childId, "invited@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).AcceptInviteAsync(wrongUserId, email.LastRawToken!, true);
            Assert.False(result.Success);
            Assert.Contains("different email", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            Assert.Empty(ctx.StudentProfiles);
            Assert.Equal(UserRole.Parent, ctx.Users.Single(u => u.Id == wrongUserId).Role);
        }
    }

    // ----------------------------------------------------------------- idempotent invite

    [Fact]
    public async Task DuplicateParentInvite_SameEmailAndChild_ReturnsExisting_NoDuplicate()
    {
        var parentId = SeedUser("idp@x.com");
        var childId = await SeedOwnedChild(parentId, "Kid");
        var email = new CapturingEmailService();

        int firstId;
        using (var ctx = CreateContext())
        {
            var first = await CreateService(ctx, email).InviteFromParentAsync(parentId, childId, "dup@x.com");
            Assert.True(first.Success);
            firstId = first.Data!.Id;
        }

        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx, email).InviteFromParentAsync(parentId, childId, "dup@x.com");
            Assert.True(second.Success);
            Assert.Equal(firstId, second.Data!.Id); // same invite returned
        }

        using (var ctx = CreateContext())
            Assert.Single(ctx.StudentInvites);
    }

    // ----------------------------------------------------------------- permission

    [Fact]
    public async Task ParentInvite_ForChildNotOwned_IsRejected()
    {
        var parentId = SeedUser("perm1@x.com");
        var otherParentId = SeedUser("other1@x.com");
        var notMineChild = await SeedOwnedChild(otherParentId, "NotMine");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteFromParentAsync(parentId, notMineChild, "s@x.com");
            Assert.False(result.Success);
            Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Empty(ctx.StudentInvites);
    }

    [Fact]
    public async Task EducatorInvite_FromAnotherSchool_IsRejected()
    {
        var (_, studentInA) = await SeedEducatorWithStudent("edAa@x.com", "DistrictAa", "SchoolAa");
        var educatorB = SeedUser("edBb@x.com");
        using (var ctx = CreateContext())
            await CreateEducator(ctx).OnboardAsync(educatorB, new OnboardEducatorModel
            {
                DistrictName = "DistrictBb", SchoolName = "SchoolBb", StateCode = "OH"
            });
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).InviteFromEducatorAsync(educatorB, studentInA, "s@x.com");
            Assert.False(result.Success);
            Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Empty(ctx.StudentInvites);
    }

    // ----------------------------------------------------------------- preview

    [Fact]
    public async Task Preview_ParentInvite_ReturnsChildContext()
    {
        var parentId = SeedUser("prevp@x.com");
        var childId = await SeedOwnedChild(parentId, "Riley");
        var studentId = SeedUser("prevs@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromParentAsync(parentId, childId, "prevs@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).PreviewInviteAsync(studentId, email.LastRawToken!);
            Assert.True(result.Success);
            Assert.Equal("Parent", result.Data!.InviteSource);
            Assert.Equal("Riley", result.Data.LinkedToFirstName);
        }
    }

    [Fact]
    public async Task Preview_EducatorInvite_ReturnsSchoolContext()
    {
        var (educatorId, schoolStudentId) = await SeedEducatorWithStudent("preved@x.com", "DistP", "SchoolP", "Jordan");
        var studentId = SeedUser("prevse@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).InviteFromEducatorAsync(educatorId, schoolStudentId, "prevse@x.com");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx, email).PreviewInviteAsync(studentId, email.LastRawToken!);
            Assert.True(result.Success);
            Assert.Equal("Educator", result.Data!.InviteSource);
            Assert.Equal("Jordan", result.Data.LinkedToFirstName);
            Assert.Equal("SchoolP", result.Data.SchoolName);
        }
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Captures the raw token passed to the student invite email so tests can exercise accept.</summary>
    private sealed class CapturingEmailService : IEmailService
    {
        public string? LastRawToken { get; private set; }

        public Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default)
        {
            LastRawToken = inviteToken;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default) => Task.CompletedTask;
    }
}
