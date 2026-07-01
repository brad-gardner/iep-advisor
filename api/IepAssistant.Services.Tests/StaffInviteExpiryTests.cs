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
/// Phase 3 coverage: the invite pre-expiry reminder logic in <see cref="StaffInviteExpiryService"/>. Exercised
/// with a deterministic <c>utcNow</c> (no timer, no hosted worker) against a real SQLite in-memory engine, the
/// same pattern as <see cref="StaffInviteServiceTests"/>. A <see cref="CapturingEmailService"/> records every
/// expiring-reminder send so recipient/idempotency can be asserted directly.
/// </summary>
public sealed class StaffInviteExpiryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public StaffInviteExpiryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private StaffInviteExpiryService CreateService(ApplicationDbContext ctx, IEmailService email)
        => new(ctx, email, NullLogger<StaffInviteExpiryService>.Instance);

    // ----------------------------------------------------------------- seed helpers

    private int SeedDistrict(string name = "Maple")
    {
        using var ctx = CreateContext();
        var d = new District { Name = name, StateCode = "OH" };
        ctx.Districts.Add(d);
        ctx.SaveChanges();
        return d.Id;
    }

    private int SeedSchool(int districtId, string name = "Elm", bool isActive = true)
    {
        using var ctx = CreateContext();
        var s = new School { DistrictId = districtId, Name = name, StateCode = "OH", IsActive = isActive };
        ctx.Schools.Add(s);
        ctx.SaveChanges();
        return s.Id;
    }

    /// <summary>Seeds a staff user (User + StaffProfile); returns the User id.</summary>
    private int SeedInviter(string email, int districtId, int? schoolId, int orgRoleId, bool isActive = true)
    {
        using var ctx = CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FirstName = "F", LastName = "L", Role = UserRole.Educator };
        ctx.Users.Add(u);
        ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile
        {
            UserId = u.Id,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = orgRoleId,
            IsActive = isActive
        });
        ctx.SaveChanges();
        return u.Id;
    }

    private int SeedInvite(
        int districtId,
        int? schoolId,
        int invitedByUserId,
        DateTime expiresAt,
        string email = "invitee@x.com",
        bool isActive = true,
        DateTime? acceptedAt = null,
        DateTime? reminderSentAt = null,
        string? token = "hash")
    {
        using var ctx = CreateContext();
        var invite = new StaffInvite
        {
            Email = email,
            DistrictId = districtId,
            SchoolId = schoolId,
            OrgRoleId = OrgRoleIds.Teacher,
            InviteToken = token,
            InviteExpiresAt = expiresAt,
            AcceptedAt = acceptedAt,
            IsActive = isActive,
            InvitedByUserId = invitedByUserId,
            ExpiryReminderSentAt = reminderSentAt
        };
        ctx.StaffInvites.Add(invite);
        ctx.SaveChanges();
        return invite.Id;
    }

    private DateTime? ReminderStamp(int inviteId)
    {
        using var ctx = CreateContext();
        return ctx.StaffInvites.AsNoTracking().Single(i => i.Id == inviteId).ExpiryReminderSentAt;
    }

    // ================================================================= Window boundaries

    [Fact]
    public async Task Invite_71HoursOut_Pending_SendsReminder_ToInviter_AndStamps()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(71), email: "invitee@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        var sent = Assert.Single(email.Sent);
        Assert.Equal("admin@x.com", sent.ToEmail);       // recipient is the INVITER
        Assert.Equal("invitee@x.com", sent.InviteeEmail); // the invite target is named in the body
        Assert.Equal("Maple", sent.DistrictName);
        Assert.Equal("Elm", sent.SchoolName);
        Assert.NotNull(ReminderStamp(inviteId));
    }

    [Fact]
    public async Task Invite_JustOver72Hours_NoReminder()
    {
        // Hard window rule is `InviteExpiresAt <= utcNow + 72h`; just past the edge must not fire.
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(72).AddMinutes(1));
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
        Assert.Null(ReminderStamp(inviteId));
    }

    [Fact]
    public async Task Invite_AlreadyExpired_NoReminder()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(-1));
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
        Assert.Null(ReminderStamp(inviteId));
    }

    [Fact]
    public async Task DistrictAdminInvite_NullSchool_SendsReminder_WithNoSchoolName()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedInvite(districtId, null, inviterId, now.AddHours(24), email: "da@x.com");
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        var sent = Assert.Single(email.Sent);
        Assert.Equal("admin@x.com", sent.ToEmail);
        Assert.Null(sent.SchoolName);
    }

    // ================================================================= Idempotency / re-arm

    [Fact]
    public async Task SecondScan_AfterSuccessfulSend_SendsNothing()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedInvite(districtId, schoolId, inviterId, now.AddHours(48));
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);
        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now.AddMinutes(5));

        Assert.Single(email.Sent); // exactly one, despite two scans
    }

    [Fact]
    public async Task Resend_NullsStamp_NextScanRemindsAgain()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(48));
        var email = new CapturingEmailService();

        // First scan: one reminder, stamp set.
        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);
        Assert.Single(email.Sent);

        // Simulate ResendAsync's effect: extend expiry back into the window AND null the stamp.
        var later = now.AddDays(11);
        using (var ctx = CreateContext())
        {
            var invite = ctx.StaffInvites.Single(i => i.Id == inviteId);
            invite.InviteExpiresAt = later.AddHours(48);
            invite.ExpiryReminderSentAt = null;
            await ctx.SaveChangesAsync();
        }

        // Next scan (as the extended invite re-approaches expiry): a fresh reminder fires.
        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(later);

        Assert.Equal(2, email.Sent.Count);
        Assert.NotNull(ReminderStamp(inviteId));
    }

    // ================================================================= Skips

    [Fact]
    public async Task Skip_WhenInvitingAdminDeactivated_NoEmail_NoStamp()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin, isActive: false);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(24));
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
        Assert.Null(ReminderStamp(inviteId));
    }

    [Fact]
    public async Task Skip_WhenInviteSchoolDeactivated_NoEmail_NoStamp()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId, isActive: false);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(24));
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
        Assert.Null(ReminderStamp(inviteId));
    }

    // ================================================================= Non-pending invites ignored

    [Fact]
    public async Task AcceptedInvite_InWindow_Ignored()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedInvite(districtId, schoolId, inviterId, now.AddHours(24), acceptedAt: now.AddHours(-2), token: null);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task RevokedInvite_InWindow_Ignored()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        SeedInvite(districtId, schoolId, inviterId, now.AddHours(24), isActive: false, token: null);
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
    }

    // ================================================================= Race guard

    [Fact]
    public async Task RaceGuard_ResendBeforeProcess_DoesNotClobberReArm()
    {
        // Candidate is scanned, but a concurrent resend extends expiry out of the window (and nulls the stamp)
        // before ProcessInviteAsync runs. The re-verify must skip the send, and the stamp must stay null so the
        // extended invite still earns its own future warning.
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(24));
        var email = new CapturingEmailService();

        // Scan happens now (candidate captured), then resend pushes expiry far out.
        List<int> candidates;
        using (var ctx = CreateContext())
            candidates = (await CreateService(ctx, email).FindExpiringInviteIdsAsync(now)).ToList();
        Assert.Contains(inviteId, candidates);

        using (var ctx = CreateContext())
        {
            var invite = ctx.StaffInvites.Single(i => i.Id == inviteId);
            invite.InviteExpiresAt = now.AddDays(14); // resend extended the window
            invite.ExpiryReminderSentAt = null;
            await ctx.SaveChangesAsync();
        }

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessInviteAsync(inviteId, now);

        Assert.Empty(email.Sent);
        Assert.Null(ReminderStamp(inviteId)); // re-arm intact
    }

    [Fact]
    public async Task RaceGuard_ResendDuringSend_EmailSent_ButStampSkipped()
    {
        // The re-verify passes (so the email IS sent), but a concurrent resend lands between the send and the
        // guarded stamp — extending expiry out of the window and nulling the stamp. The guarded UPDATE must
        // then match zero rows, leaving the re-arm intact even though an email already went out.
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);
        var inviterId = SeedInviter("admin@x.com", districtId, null, OrgRoleIds.DistrictAdmin);
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(24));

        var email = new MutatingEmailService(() =>
        {
            using var ctx = CreateContext();
            var invite = ctx.StaffInvites.Single(i => i.Id == inviteId);
            invite.InviteExpiresAt = now.AddDays(14); // resend extended the window mid-send
            invite.ExpiryReminderSentAt = null;
            ctx.SaveChanges();
        });

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessInviteAsync(inviteId, now);

        Assert.Equal(1, email.SendCount);        // email WAS sent
        Assert.Null(ReminderStamp(inviteId));    // but stamp skipped — re-arm preserved
    }

    [Fact]
    public async Task Skip_WhenInviterHasNoEmail_NoEmail_NoStamp()
    {
        var now = DateTime.UtcNow;
        var districtId = SeedDistrict();
        var schoolId = SeedSchool(districtId);

        // Active inviter whose User row has a blank email.
        int inviterId;
        using (var ctx = CreateContext())
        {
            var u = new User { Email = "", PasswordHash = "x", FirstName = "F", LastName = "L", Role = UserRole.Educator };
            ctx.Users.Add(u);
            ctx.SaveChanges();
            ctx.StaffProfiles.Add(new StaffProfile
            {
                UserId = u.Id, DistrictId = districtId, SchoolId = null, OrgRoleId = OrgRoleIds.DistrictAdmin, IsActive = true
            });
            ctx.SaveChanges();
            inviterId = u.Id;
        }
        var inviteId = SeedInvite(districtId, schoolId, inviterId, now.AddHours(24));
        var email = new CapturingEmailService();

        using (var ctx = CreateContext())
            await CreateService(ctx, email).ProcessExpiringInvitesAsync(now);

        Assert.Empty(email.Sent);
        Assert.Null(ReminderStamp(inviteId));
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Runs a callback when the expiry reminder is sent, to simulate a concurrent DB mutation
    /// landing between the send and the guarded stamp.</summary>
    private sealed class MutatingEmailService : IEmailService
    {
        private readonly Action _onSend;
        public int SendCount { get; private set; }

        public MutatingEmailService(Action onSend) => _onSend = onSend;

        public Task SendStaffInviteExpiringEmailAsync(string toEmail, string inviteeEmail, string districtName, string? schoolName, DateTime expiresAt, CancellationToken ct = default)
        {
            SendCount++;
            _onSend();
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>Records every expiry-reminder send so recipient + idempotency can be asserted.</summary>
    private sealed class CapturingEmailService : IEmailService
    {
        public sealed record Reminder(string ToEmail, string InviteeEmail, string DistrictName, string? SchoolName, DateTime ExpiresAt);

        public List<Reminder> Sent { get; } = new();

        public Task SendStaffInviteExpiringEmailAsync(string toEmail, string inviteeEmail, string districtName, string? schoolName, DateTime expiresAt, CancellationToken ct = default)
        {
            Sent.Add(new Reminder(toEmail, inviteeEmail, districtName, schoolName, expiresAt));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendShareInviteEmailAsync(string toEmail, string inviterName, string childName, string role, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendSchoolLinkInviteEmailAsync(string toEmail, string educatorName, string schoolName, string studentName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendStudentInviteEmailAsync(string toEmail, string inviterName, string context, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendBetaInviteEmailAsync(string toEmail, string inviteCode, CancellationToken ct = default) => Task.CompletedTask;
    }
}
