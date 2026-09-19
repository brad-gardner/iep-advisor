using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Virtual Advocate plan, Phase 1: the parent-private journal. Entries list newest OccurredOn first, are
/// markdown-at-rest (sanitized before the length check), must be dated today or earlier, may only link
/// records that belong to the same child, and are visible to every family member with Viewer+ while
/// only Collaborator+ may write. Unknown ids and missing access collapse to the same "not found".
/// </summary>
public sealed class JournalServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public JournalServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static JournalService CreateService(ApplicationDbContext ctx) => new(ctx, new AccessService(ctx));

    private sealed record Family(int OwnerId, int CoParentId, int ViewerId, int StrangerId, int ChildId, int OtherChildId);

    /// <summary>Owner + Collaborator co-parent + Viewer on one child; a stranger who owns a second child.</summary>
    private Family SeedFamily(string prefix)
    {
        using var ctx = CreateContext();
        var owner = new User { Email = $"{prefix}-owner@example.com", PasswordHash = "x", FirstName = "Dana", LastName = "Parent", Role = UserRole.Parent };
        var coParent = new User { Email = $"{prefix}-co@example.com", PasswordHash = "x", FirstName = "Chris", LastName = "CoParent", Role = UserRole.Parent };
        var viewer = new User { Email = $"{prefix}-viewer@example.com", PasswordHash = "x", FirstName = "Vic", LastName = "Viewer", Role = UserRole.Parent };
        var stranger = new User { Email = $"{prefix}-stranger@example.com", PasswordHash = "x", FirstName = "Sam", LastName = "Stranger", Role = UserRole.Parent };
        ctx.Users.AddRange(owner, coParent, viewer, stranger);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = owner.Id, FirstName = "Jordan" };
        var otherChild = new ChildProfile { UserId = stranger.Id, FirstName = "Riley" };
        ctx.ChildProfiles.AddRange(child, otherChild);
        ctx.SaveChanges();

        ctx.ChildAccesses.AddRange(
            new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow },
            new ChildAccess { ChildProfileId = child.Id, UserId = coParent.Id, Role = AccessRole.Collaborator, IsActive = true, AcceptedAt = DateTime.UtcNow },
            new ChildAccess { ChildProfileId = child.Id, UserId = viewer.Id, Role = AccessRole.Viewer, IsActive = true, AcceptedAt = DateTime.UtcNow },
            new ChildAccess { ChildProfileId = otherChild.Id, UserId = stranger.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.SaveChanges();

        return new Family(owner.Id, coParent.Id, viewer.Id, stranger.Id, child.Id, otherChild.Id);
    }

    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

    private static SaveJournalEntryModel Entry(string content = "Sent home early after a **rough** morning.", JournalTag tag = JournalTag.Incident, DateOnly? occurredOn = null) => new()
    {
        ContentMarkdown = content,
        Tag = tag,
        OccurredOn = occurredOn ?? Yesterday
    };

    private async Task<int> CreateEntryAsync(int childId, int userId, SaveJournalEntryModel model)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(childId, userId, model);
        Assert.True(result.Success, result.Message);
        return result.Data!.Id;
    }

    // ------------------------------------------------------------------ create + list

    [Fact]
    public async Task Create_ThenList_ReturnsEntriesNewestOccurredOnFirst()
    {
        var f = SeedFamily("order");
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("middle", occurredOn: Yesterday.AddDays(-5)));
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("oldest", occurredOn: Yesterday.AddDays(-30)));
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("newest", occurredOn: Yesterday));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetForChildAsync(f.ChildId, f.OwnerId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { "newest", "middle", "oldest" }, result.Data!.Select(e => e.ContentMarkdown).ToArray());
        var newest = result.Data[0];
        Assert.Equal(f.ChildId, newest.ChildProfileId);
        Assert.Equal(Yesterday, newest.OccurredOn);
        Assert.Equal(JournalTag.Incident, newest.Tag);
        Assert.Equal(f.OwnerId, newest.CreatedById);
    }

    [Fact]
    public async Task Create_SameDay_ListsMostRecentlyCreatedFirst()
    {
        var f = SeedFamily("sameday");
        int first, second;
        using (var ctx = CreateContext())
        {
            var svc = CreateService(ctx);
            first = (await svc.CreateAsync(f.ChildId, f.OwnerId, Entry("first written"))).Data!.Id;
            second = (await svc.CreateAsync(f.ChildId, f.OwnerId, Entry("second written"))).Data!.Id;
        }
        using (var ctx = CreateContext())
        {
            // Pin distinct timestamps so the order does not depend on clock resolution.
            var a = await ctx.JournalEntries.SingleAsync(j => j.Id == first);
            var b = await ctx.JournalEntries.SingleAsync(j => j.Id == second);
            a.CreatedAt = new DateTime(2026, 9, 18, 10, 0, 0, DateTimeKind.Utc);
            b.CreatedAt = new DateTime(2026, 9, 18, 11, 0, 0, DateTimeKind.Utc);
            await ctx.SaveChangesAsync();
        }

        using var read = CreateContext();
        var result = await CreateService(read).GetForChildAsync(f.ChildId, f.OwnerId);

        Assert.Equal(new[] { "second written", "first written" }, result.Data!.Select(e => e.ContentMarkdown).ToArray());
    }

    [Fact]
    public async Task GetForChild_WithTag_ReturnsOnlyThatTag()
    {
        var f = SeedFamily("tag");
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("nurse called", JournalTag.Medical));
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("email to teacher", JournalTag.Communication));
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("doctor visit", JournalTag.Medical, Yesterday.AddDays(-3)));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetForChildAsync(f.ChildId, f.OwnerId, tag: JournalTag.Medical);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { "nurse called", "doctor visit" }, result.Data!.Select(e => e.ContentMarkdown).ToArray());
        Assert.All(result.Data!, e => Assert.Equal(JournalTag.Medical, e.Tag));
    }

    [Fact]
    public async Task GetForChild_WithTake_CapsTheListToTheNewest()
    {
        var f = SeedFamily("take");
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("d-3", occurredOn: Yesterday.AddDays(-3)));
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("d-1", occurredOn: Yesterday.AddDays(-1)));
        await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("d-2", occurredOn: Yesterday.AddDays(-2)));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetForChildAsync(f.ChildId, f.OwnerId, take: 2);

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { "d-1", "d-2" }, result.Data!.Select(e => e.ContentMarkdown).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task GetForChild_WithTakeOutOfRange_Fails(int take)
    {
        var f = SeedFamily($"takerange{take}");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetForChildAsync(f.ChildId, f.OwnerId, take: take);

        Assert.False(result.Success);
        Assert.Equal("take must be between 1 and 200.", result.Message);
    }

    // ------------------------------------------------------------------ access

    [Fact]
    public async Task Viewer_CanList_ButCannotCreateUpdateOrDelete()
    {
        var f = SeedFamily("viewer");
        var id = await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("owner wrote this"));

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var list = await svc.GetForChildAsync(f.ChildId, f.ViewerId);
        var create = await svc.CreateAsync(f.ChildId, f.ViewerId, Entry("viewer tries"));
        var update = await svc.UpdateAsync(id, f.ViewerId, Entry("viewer edits"));
        var delete = await svc.DeleteAsync(id, f.ViewerId);

        Assert.True(list.Success, list.Message);
        Assert.Equal("owner wrote this", Assert.Single(list.Data!).ContentMarkdown);
        Assert.False(create.Success);
        Assert.Equal("Child profile not found.", create.Message);
        Assert.False(update.Success);
        Assert.Equal("Journal entry not found.", update.Message);
        Assert.False(delete.Success);
        Assert.Equal("Journal entry not found.", delete.Message);
        Assert.Equal("owner wrote this", (await ctx.JournalEntries.SingleAsync(j => j.Id == id)).ContentMarkdown);
    }

    [Fact]
    public async Task UnrelatedUser_GetsNotFound_OnListUpdateAndDelete_AndNothingLeaksOrChanges()
    {
        var f = SeedFamily("stranger");
        var id = await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("private"));

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var list = await svc.GetForChildAsync(f.ChildId, f.StrangerId);
        var update = await svc.UpdateAsync(id, f.StrangerId, Entry("hijack"));
        var delete = await svc.DeleteAsync(id, f.StrangerId);
        var missing = await svc.UpdateAsync(999_999, f.StrangerId, Entry("ghost"));

        Assert.False(list.Success);
        Assert.Equal("Child profile not found.", list.Message);
        Assert.Null(list.Data);
        Assert.False(update.Success);
        Assert.False(delete.Success);
        // An entry the user cannot see and an entry that does not exist read identically.
        Assert.Equal(missing.Message, update.Message);
        Assert.Equal(missing.Message, delete.Message);
        Assert.Equal("private", (await ctx.JournalEntries.SingleAsync(j => j.Id == id)).ContentMarkdown);
    }

    [Fact]
    public async Task CoParentCollaborator_CanCreate_AndOwnerSeesItWithAuthor()
    {
        var f = SeedFamily("coparent");

        var id = await CreateEntryAsync(f.ChildId, f.CoParentId, Entry("Co-parent note", JournalTag.Progress));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetForChildAsync(f.ChildId, f.OwnerId);
        Assert.True(result.Success, result.Message);
        var entry = Assert.Single(result.Data!);
        Assert.Equal(id, entry.Id);
        Assert.Equal("Co-parent note", entry.ContentMarkdown);
        Assert.Equal(f.CoParentId, entry.CreatedById);
    }

    // ------------------------------------------------------------------ validation

    [Fact]
    public async Task Create_WithJavascriptLink_PersistsTheLinkTextOnly()
    {
        var f = SeedFamily("sanitize");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(f.ChildId, f.OwnerId, Entry("Click [x](javascript:alert(1)) and **bold** stays"));

        Assert.True(result.Success, result.Message);
        Assert.Equal("Click x and **bold** stays", result.Data!.ContentMarkdown);
        Assert.Equal("Click x and **bold** stays", (await ctx.JournalEntries.SingleAsync(j => j.Id == result.Data.Id)).ContentMarkdown);
    }

    [Fact]
    public async Task Create_WithContentOver4000Chars_IsRejected()
    {
        var f = SeedFamily("toolong");

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var tooLong = await svc.CreateAsync(f.ChildId, f.OwnerId, Entry(new string('a', 4001)));
        var atLimit = await svc.CreateAsync(f.ChildId, f.OwnerId, Entry(new string('a', 4000)));

        Assert.False(tooLong.Success);
        Assert.Equal("Content must be 4000 characters or fewer.", tooLong.Message);
        Assert.True(atLimit.Success, atLimit.Message);
        Assert.Equal(1, await ctx.JournalEntries.CountAsync());
    }

    [Fact]
    public async Task Create_MeasuresLengthAfterSanitizing()
    {
        var f = SeedFamily("sanitizelen");
        // 4000 real characters plus a script block the sanitizer removes entirely: within limit once sanitized.
        var content = new string('b', 4000) + "<script>alert(1)</script>";

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(f.ChildId, f.OwnerId, Entry(content));

        Assert.True(result.Success, result.Message);
        Assert.Equal(new string('b', 4000), result.Data!.ContentMarkdown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>alert(1)</script>")]
    public async Task Create_WithNoRealContent_IsRejected(string content)
    {
        var f = SeedFamily("empty" + content.Length);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(f.ChildId, f.OwnerId, Entry(content));

        Assert.False(result.Success);
        Assert.Equal("Content is required.", result.Message);
    }

    [Fact]
    public async Task Create_WithFutureOccurredOn_IsRejected_ButTodayIsAccepted()
    {
        var f = SeedFamily("future");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var ctx = CreateContext();
        var svc = CreateService(ctx);
        var future = await svc.CreateAsync(f.ChildId, f.OwnerId, Entry("tomorrow", occurredOn: today.AddDays(2)));
        var present = await svc.CreateAsync(f.ChildId, f.OwnerId, Entry("today", occurredOn: today));

        Assert.False(future.Success);
        Assert.Equal("Date cannot be in the future.", future.Message);
        Assert.True(present.Success, present.Message);
    }

    [Fact]
    public async Task Create_LinkingAnotherChildsIep_IsRejected_WhileOwnIepIsAccepted()
    {
        var f = SeedFamily("ieplink");
        int ownIepId, otherIepId;
        using (var ctx = CreateContext())
        {
            var ownDoc = new IepDocument { ChildProfileId = f.ChildId, FileName = "own.pdf" };
            var otherDoc = new IepDocument { ChildProfileId = f.OtherChildId, FileName = "other.pdf" };
            ctx.IepDocuments.AddRange(ownDoc, otherDoc);
            await ctx.SaveChangesAsync();
            ownIepId = ownDoc.Id;
            otherIepId = otherDoc.Id;
        }

        using var read = CreateContext();
        var svc = CreateService(read);
        var crossChild = await svc.CreateAsync(f.ChildId, f.OwnerId, new SaveJournalEntryModel { ContentMarkdown = "x", Tag = JournalTag.Other, OccurredOn = Yesterday, LinkedIepDocumentId = otherIepId });
        var nonexistent = await svc.CreateAsync(f.ChildId, f.OwnerId, new SaveJournalEntryModel { ContentMarkdown = "x", Tag = JournalTag.Other, OccurredOn = Yesterday, LinkedIepDocumentId = 999_999 });
        var own = await svc.CreateAsync(f.ChildId, f.OwnerId, new SaveJournalEntryModel { ContentMarkdown = "x", Tag = JournalTag.Other, OccurredOn = Yesterday, LinkedIepDocumentId = ownIepId });

        Assert.False(crossChild.Success);
        Assert.Equal("Linked IEP document is not available for this child.", crossChild.Message);
        Assert.Equal(crossChild.Message, nonexistent.Message);
        Assert.True(own.Success, own.Message);
        Assert.Equal(ownIepId, own.Data!.LinkedIepDocumentId);
    }

    [Fact]
    public async Task Create_LinkingMeeting_RequiresAcceptedChildLinkToTheMeetingsStudent()
    {
        var f = SeedFamily("meetinglink");
        int linkedMeetingId, unlinkedMeetingId;
        using (var ctx = CreateContext())
        {
            var district = new District { Name = "D" };
            ctx.Districts.Add(district);
            await ctx.SaveChangesAsync();
            var school = new School { DistrictId = district.Id, Name = "S" };
            ctx.Schools.Add(school);
            await ctx.SaveChangesAsync();
            var linkedStudent = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Jordan" };
            var unlinkedStudent = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Someone" };
            ctx.SchoolStudents.AddRange(linkedStudent, unlinkedStudent);
            await ctx.SaveChangesAsync();
            ctx.ChildLinks.Add(new ChildLink { ChildProfileId = f.ChildId, SchoolStudentId = linkedStudent.Id, IsActive = true, AcceptedAt = DateTime.UtcNow, LinkedAt = DateTime.UtcNow });
            var linked = new Meeting { SchoolStudentId = linkedStudent.Id, Title = "Annual", StartsAtUtc = DateTime.UtcNow.AddDays(-1), CreatedByUserId = f.OwnerId };
            var unlinked = new Meeting { SchoolStudentId = unlinkedStudent.Id, Title = "Other", StartsAtUtc = DateTime.UtcNow.AddDays(-1), CreatedByUserId = f.OwnerId };
            ctx.Meetings.AddRange(linked, unlinked);
            await ctx.SaveChangesAsync();
            linkedMeetingId = linked.Id;
            unlinkedMeetingId = unlinked.Id;
        }

        using var read = CreateContext();
        var svc = CreateService(read);
        var foreign = await svc.CreateAsync(f.ChildId, f.OwnerId, new SaveJournalEntryModel { ContentMarkdown = "x", Tag = JournalTag.Communication, OccurredOn = Yesterday, LinkedMeetingId = unlinkedMeetingId });
        var own = await svc.CreateAsync(f.ChildId, f.OwnerId, new SaveJournalEntryModel { ContentMarkdown = "x", Tag = JournalTag.Communication, OccurredOn = Yesterday, LinkedMeetingId = linkedMeetingId });

        Assert.False(foreign.Success);
        Assert.Equal("Linked meeting is not available for this child.", foreign.Message);
        Assert.True(own.Success, own.Message);
        Assert.Equal(linkedMeetingId, own.Data!.LinkedMeetingId);
    }

    // ------------------------------------------------------------------ update / delete

    [Fact]
    public async Task Update_ChangesContentTagAndDate_AndStampsUpdatedBy()
    {
        var f = SeedFamily("update");
        var id = await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("original", JournalTag.Incident, Yesterday.AddDays(-10)));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).UpdateAsync(id, f.CoParentId, Entry("**revised**", JournalTag.Progress, Yesterday));

        Assert.True(result.Success, result.Message);
        Assert.Equal("**revised**", result.Data!.ContentMarkdown);
        Assert.Equal(JournalTag.Progress, result.Data.Tag);
        Assert.Equal(Yesterday, result.Data.OccurredOn);
        Assert.Equal(f.OwnerId, result.Data.CreatedById);
        using var verify = CreateContext();
        var stored = await verify.JournalEntries.SingleAsync(j => j.Id == id);
        Assert.Equal("**revised**", stored.ContentMarkdown);
        Assert.Equal(JournalTag.Progress, stored.Tag);
        Assert.Equal(Yesterday, stored.OccurredOn);
        Assert.Equal(f.OwnerId, stored.CreatedById);
        Assert.Equal(f.CoParentId, stored.UpdatedById);
    }

    [Fact]
    public async Task Update_WithInvalidContent_LeavesTheEntryUntouched()
    {
        var f = SeedFamily("updateinvalid");
        var id = await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("keep me"));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).UpdateAsync(id, f.OwnerId, Entry(new string('z', 4001)));

        Assert.False(result.Success);
        Assert.Equal("Content must be 4000 characters or fewer.", result.Message);
        using var verify = CreateContext();
        Assert.Equal("keep me", (await verify.JournalEntries.SingleAsync(j => j.Id == id)).ContentMarkdown);
    }

    [Fact]
    public async Task Delete_ByCollaborator_RemovesTheEntry()
    {
        var f = SeedFamily("delete");
        var id = await CreateEntryAsync(f.ChildId, f.OwnerId, Entry("to delete"));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeleteAsync(id, f.CoParentId);

        Assert.True(result.Success, result.Message);
        Assert.False(await ctx.JournalEntries.AnyAsync(j => j.Id == id));
    }

    public void Dispose() => _connection.Dispose();
}
