using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The parent's own meeting-prep questions: plain text at rest (markup stripped before the length check),
/// listed in display order, de-duplicated case-insensitively on add, reorderable only with the child's own
/// ids, readable by Viewer+ and writable by Collaborator+. Unknown ids and missing access collapse to the
/// same "not found".
/// </summary>
public sealed class ParentPrepQuestionServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ParentPrepQuestionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static ParentPrepQuestionService CreateService(ApplicationDbContext ctx) => new(ctx, new AccessService(ctx));

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

    private async Task<ParentPrepQuestionModel> AddAsync(int childId, int userId, string text, string? source = null)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddAsync(childId, userId, text, source);
        Assert.True(result.Success, result.Message);
        Assert.False(result.Data!.AlreadyExisted);
        return result.Data.Question;
    }

    private async Task<List<ParentPrepQuestionModel>> ListAsync(int childId, int userId)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetForChildAsync(childId, userId);
        Assert.True(result.Success, result.Message);
        return result.Data!;
    }

    // ------------------------------------------------------------------ add + list

    [Fact]
    public async Task Add_ThenList_ReturnsQuestionsInDisplayOrder_WithIncrementingOrder()
    {
        var f = SeedFamily("order");
        var first = await AddAsync(f.ChildId, f.OwnerId, "What baseline was used for the reading goal?");
        var second = await AddAsync(f.ChildId, f.CoParentId, "How often is progress measured?", "advocate");

        Assert.Equal(0, first.DisplayOrder);
        Assert.Equal(1, second.DisplayOrder);
        Assert.Equal("parent", first.Source);
        Assert.Equal("advocate", second.Source);
        Assert.False(first.IsChecked);
        Assert.Equal(f.OwnerId, first.CreatedById);

        var list = await ListAsync(f.ChildId, f.ViewerId);
        Assert.Equal(new[] { first.Id, second.Id }, list.Select(q => q.Id));
        Assert.Equal(f.ChildId, list[0].ChildProfileId);
    }

    [Fact]
    public async Task Add_StripsMarkup_AndCollapsesWhitespace_BeforeStoring()
    {
        var f = SeedFamily("markup");
        var q = await AddAsync(f.ChildId, f.OwnerId, "  <p>Why was <b>speech</b> <script>alert(1)</script>reduced?</p>\n\n<img src=x onerror=alert(1)>  Next   line ");
        Assert.Equal("Why was speech reduced? Next line", q.Text);
    }

    [Fact]
    public async Task Add_RejectsBlank_AndMarkupOnly_AndOverLongText()
    {
        var f = SeedFamily("validate");
        using var ctx = CreateContext();
        var service = CreateService(ctx);

        var blank = await service.AddAsync(f.ChildId, f.OwnerId, "   ", null);
        Assert.False(blank.Success);
        Assert.Equal("Question text is required.", blank.Message);

        var markupOnly = await service.AddAsync(f.ChildId, f.OwnerId, "<script>alert(1)</script><br/>", null);
        Assert.False(markupOnly.Success);
        Assert.Equal("Question text is required.", markupOnly.Message);

        var tooLong = await service.AddAsync(f.ChildId, f.OwnerId, new string('q', ParentPrepQuestionService.MaxTextLength + 1), null);
        Assert.False(tooLong.Success);
        Assert.Contains("500", tooLong.Message);

        // Markup does not count: 500 plain characters wrapped in tags is fine.
        var exact = await service.AddAsync(f.ChildId, f.OwnerId, "<b>" + new string('q', ParentPrepQuestionService.MaxTextLength) + "</b>", null);
        Assert.True(exact.Success, exact.Message);
        Assert.Equal(ParentPrepQuestionService.MaxTextLength, exact.Data!.Question.Text.Length);

        Assert.Single((await service.GetForChildAsync(f.ChildId, f.OwnerId)).Data!); // only the valid add landed
    }

    [Fact]
    public async Task Add_RejectsUnknownSource()
    {
        var f = SeedFamily("source");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddAsync(f.ChildId, f.OwnerId, "A question", "teacher");
        Assert.False(result.Success);
        Assert.Contains("Source", result.Message);
    }

    [Fact]
    public async Task Add_DuplicateTextCaseInsensitive_ReturnsExistingRow_WithAlreadyExisted()
    {
        var f = SeedFamily("dupe");
        var original = await AddAsync(f.ChildId, f.OwnerId, "What baseline was used?");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddAsync(f.ChildId, f.CoParentId, "  <em>WHAT baseline</em> was used?  ", "advocate");

        Assert.True(result.Success, result.Message);
        Assert.True(result.Data!.AlreadyExisted);
        Assert.Equal(original.Id, result.Data.Question.Id);
        Assert.Equal("parent", result.Data.Question.Source); // the existing row is untouched
        Assert.Single(await ListAsync(f.ChildId, f.OwnerId));
    }

    [Fact]
    public async Task Add_SameTextOnAnotherChild_IsNotADuplicate()
    {
        var f = SeedFamily("dupe-other-child");
        await AddAsync(f.ChildId, f.OwnerId, "What baseline was used?");
        var other = await AddAsync(f.OtherChildId, f.StrangerId, "What baseline was used?");
        Assert.Equal(f.OtherChildId, other.ChildProfileId);
    }

    // ------------------------------------------------------------------ access

    [Fact]
    public async Task Viewer_CanList_ButCannotAddUpdateReorderOrDelete()
    {
        var f = SeedFamily("viewer");
        var q = await AddAsync(f.ChildId, f.OwnerId, "Owner's question");

        using var ctx = CreateContext();
        var service = CreateService(ctx);
        Assert.True((await service.GetForChildAsync(f.ChildId, f.ViewerId)).Success);

        var add = await service.AddAsync(f.ChildId, f.ViewerId, "Viewer's question", null);
        Assert.False(add.Success);
        Assert.Equal("Child profile not found.", add.Message);

        var update = await service.UpdateAsync(q.Id, f.ViewerId, null, true);
        Assert.False(update.Success);
        Assert.Equal("Prep question not found.", update.Message);

        var reorder = await service.ReorderAsync(f.ChildId, f.ViewerId, new[] { q.Id });
        Assert.False(reorder.Success);
        Assert.Equal("Child profile not found.", reorder.Message);

        var delete = await service.DeleteAsync(q.Id, f.ViewerId);
        Assert.False(delete.Success);
        Assert.Equal("Prep question not found.", delete.Message);

        var after = Assert.Single(await ListAsync(f.ChildId, f.OwnerId));
        Assert.False(after.IsChecked);
    }

    [Fact]
    public async Task UnrelatedUser_GetsNotFound_ForEveryOperation_SameAsUnknownIds()
    {
        var f = SeedFamily("stranger");
        var q = await AddAsync(f.ChildId, f.OwnerId, "Owner's question");

        using var ctx = CreateContext();
        var service = CreateService(ctx);

        var list = await service.GetForChildAsync(f.ChildId, f.StrangerId);
        Assert.False(list.Success);
        Assert.Equal("Child profile not found.", list.Message);

        var update = await service.UpdateAsync(q.Id, f.StrangerId, "changed", null);
        Assert.False(update.Success);
        Assert.Equal("Prep question not found.", update.Message);

        var delete = await service.DeleteAsync(q.Id, f.StrangerId);
        Assert.False(delete.Success);
        Assert.Equal("Prep question not found.", delete.Message);

        var unknownUpdate = await service.UpdateAsync(999_999, f.OwnerId, "changed", null);
        Assert.Equal(update.Message, unknownUpdate.Message);
        var unknownDelete = await service.DeleteAsync(999_999, f.OwnerId);
        Assert.Equal(delete.Message, unknownDelete.Message);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_ChangesText_AndCheckedState_Independently()
    {
        var f = SeedFamily("update");
        var q = await AddAsync(f.ChildId, f.OwnerId, "Original");

        using var ctx = CreateContext();
        var service = CreateService(ctx);

        var checkedOnly = await service.UpdateAsync(q.Id, f.CoParentId, null, true);
        Assert.True(checkedOnly.Success, checkedOnly.Message);
        Assert.True(checkedOnly.Data!.IsChecked);
        Assert.Equal("Original", checkedOnly.Data.Text);

        var textOnly = await service.UpdateAsync(q.Id, f.CoParentId, "<i>Rewritten</i> question", null);
        Assert.True(textOnly.Success, textOnly.Message);
        Assert.Equal("Rewritten question", textOnly.Data!.Text);
        Assert.True(textOnly.Data.IsChecked); // unchanged

        var neither = await service.UpdateAsync(q.Id, f.CoParentId, null, null);
        Assert.False(neither.Success);
        Assert.Equal("Provide text or isChecked.", neither.Message);

        var blank = await service.UpdateAsync(q.Id, f.CoParentId, "  ", null);
        Assert.False(blank.Success);
        Assert.Equal("Question text is required.", blank.Message);

        var stored = Assert.Single(await ListAsync(f.ChildId, f.OwnerId));
        Assert.Equal("Rewritten question", stored.Text);
        Assert.True(stored.IsChecked);
    }

    // ------------------------------------------------------------------ reorder

    [Fact]
    public async Task Reorder_PutsListedIdsFirst_AndUnlistedKeepTheirRelativeOrderAfter()
    {
        var f = SeedFamily("reorder");
        var a = await AddAsync(f.ChildId, f.OwnerId, "A");
        var b = await AddAsync(f.ChildId, f.OwnerId, "B");
        var c = await AddAsync(f.ChildId, f.OwnerId, "C");
        var d = await AddAsync(f.ChildId, f.OwnerId, "D");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ReorderAsync(f.ChildId, f.CoParentId, new[] { c.Id, a.Id });

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { c.Id, a.Id, b.Id, d.Id }, result.Data!.Select(q => q.Id));
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.Data.Select(q => q.DisplayOrder));

        var list = await ListAsync(f.ChildId, f.OwnerId);
        Assert.Equal(new[] { "C", "A", "B", "D" }, list.Select(q => q.Text));
    }

    [Fact]
    public async Task Reorder_RejectsIdsFromAnotherChild_UnknownIds_Repeats_AndEmpty()
    {
        var f = SeedFamily("reorder-validate");
        var mine = await AddAsync(f.ChildId, f.OwnerId, "Mine");
        var theirs = await AddAsync(f.OtherChildId, f.StrangerId, "Theirs");

        using var ctx = CreateContext();
        var service = CreateService(ctx);

        var foreign = await service.ReorderAsync(f.ChildId, f.OwnerId, new[] { mine.Id, theirs.Id });
        Assert.False(foreign.Success);
        Assert.Equal("Every id must be one of this child's prep questions.", foreign.Message);
        Assert.DoesNotContain("not found", foreign.Message, StringComparison.OrdinalIgnoreCase);

        var unknown = await service.ReorderAsync(f.ChildId, f.OwnerId, new[] { 999_999 });
        Assert.Equal(foreign.Message, unknown.Message); // no existence hint

        var repeated = await service.ReorderAsync(f.ChildId, f.OwnerId, new[] { mine.Id, mine.Id });
        Assert.False(repeated.Success);
        Assert.Equal("ids must not repeat.", repeated.Message);

        var empty = await service.ReorderAsync(f.ChildId, f.OwnerId, Array.Empty<int>());
        Assert.False(empty.Success);
        Assert.Equal("ids is required.", empty.Message);

        // Nothing moved: the stranger's question is still theirs, in its own list.
        var theirList = Assert.Single(await ListAsync(f.OtherChildId, f.StrangerId));
        Assert.Equal(theirs.Id, theirList.Id);
        Assert.Equal(0, theirList.DisplayOrder);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_RemovesOnlyThatQuestion()
    {
        var f = SeedFamily("delete");
        var keep = await AddAsync(f.ChildId, f.OwnerId, "Keep");
        var drop = await AddAsync(f.ChildId, f.OwnerId, "Drop");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).DeleteAsync(drop.Id, f.CoParentId);
        Assert.True(result.Success, result.Message);

        var remaining = Assert.Single(await ListAsync(f.ChildId, f.OwnerId));
        Assert.Equal(keep.Id, remaining.Id);

        var again = await CreateService(ctx).DeleteAsync(drop.Id, f.OwnerId);
        Assert.False(again.Success);
        Assert.Equal("Prep question not found.", again.Message);
    }

    public void Dispose() => _connection.Dispose();
}
