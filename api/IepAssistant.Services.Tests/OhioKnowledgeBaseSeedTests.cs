using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Data.Migrations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// The Ohio knowledge-base seed (<see cref="SeedOhioKnowledgeBase"/>): enough entries, every required field,
/// the allowed categories, Ohio citations, column limits, unique titles — and, applied to a real database,
/// <c>Up</c> inserts exactly the entries and <c>Down</c> removes exactly those rows and nothing else.
/// </summary>
public sealed class OhioKnowledgeBaseSeedTests : IDisposable
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.Ordinal) { "rights", "process", "provisions", "glossary", "tips" };

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public OhioKnowledgeBaseSeedTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    // ------------------------------------------------------------------ static checks over the seed

    [Fact]
    public void Seed_HasAtLeastThirtyEntries_AllOhio()
    {
        Assert.True(SeedOhioKnowledgeBase.Entries.Count >= 30, $"only {SeedOhioKnowledgeBase.Entries.Count} entries");
        Assert.Equal("OH", SeedOhioKnowledgeBase.StateCode);
    }

    [Fact]
    public void Seed_EveryEntry_HasRequiredFields_AllowedCategory_AndAnOhioCitation()
    {
        Assert.All(SeedOhioKnowledgeBase.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Title), "blank title");
            Assert.False(string.IsNullOrWhiteSpace(e.Content), $"{e.Title}: blank content");
            Assert.False(string.IsNullOrWhiteSpace(e.LegalReference), $"{e.Title}: blank legal reference");
            Assert.False(string.IsNullOrWhiteSpace(e.Tags), $"{e.Title}: blank tags");
            Assert.Contains(e.Category, AllowedCategories);
            Assert.True(e.LegalReference.StartsWith("OAC 3301-51", StringComparison.Ordinal) || e.LegalReference.StartsWith("ORC 3323", StringComparison.Ordinal),
                $"{e.Title}: legal reference '{e.LegalReference}' is not an Ohio citation");
        });
    }

    [Fact]
    public void Seed_EveryCategoryIsRepresented()
    {
        Assert.Equal(AllowedCategories, SeedOhioKnowledgeBase.Entries.Select(e => e.Category).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void Seed_FitsTheKnowledgeBaseEntryColumns()
    {
        // KnowledgeBaseEntryConfiguration: Title 200, Content 4000, Category 50, LegalReference 100, State 10, Tags 500.
        Assert.All(SeedOhioKnowledgeBase.Entries, e =>
        {
            Assert.True(e.Title.Length <= 200, $"{e.Title}: title {e.Title.Length} chars");
            Assert.True(e.Content.Length <= 4000, $"{e.Title}: content {e.Content.Length} chars");
            Assert.True(e.Category.Length <= 50);
            Assert.True(e.LegalReference.Length <= 100, $"{e.Title}: legal reference {e.LegalReference.Length} chars");
            Assert.True(e.Tags.Length <= 500, $"{e.Title}: tags {e.Tags.Length} chars");
        });
    }

    [Fact]
    public void Seed_ContentIsParentSized_NotAOneLinerAndNotAnEssay()
    {
        Assert.All(SeedOhioKnowledgeBase.Entries, e =>
        {
            var words = e.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            Assert.InRange(words, 120, 260);
        });
    }

    [Fact]
    public void Seed_TitlesAreUnique_AndTagsAreCommaSeparated()
    {
        var titles = SeedOhioKnowledgeBase.Entries.Select(e => e.Title).ToList();
        Assert.Equal(titles.Count, titles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(SeedOhioKnowledgeBase.Entries, e =>
            Assert.All(e.Tags.Split(','), tag => Assert.False(string.IsNullOrWhiteSpace(tag), $"{e.Title}: empty tag")));
    }

    [Fact]
    public void Seed_CoversTheOhioSpecificTimelines()
    {
        // The Phase 4 checkpoint: an Ohio parent asking about ETR timelines gets OAC 3301-51-06 cited.
        var etrTimeline = Assert.Single(SeedOhioKnowledgeBase.Entries, e => e.Tags.Contains("evaluation timeline", StringComparison.Ordinal));
        Assert.StartsWith("OAC 3301-51-06", etrTimeline.LegalReference);
        Assert.Contains("60 days", etrTimeline.Content);

        Assert.Contains(SeedOhioKnowledgeBase.Entries, e => e.LegalReference.StartsWith("OAC 3301-51-07(E)(2)") && e.Content.Contains("14"));
        Assert.Contains(SeedOhioKnowledgeBase.Entries, e => e.LegalReference.StartsWith("OAC 3301-51-06(B)(3)") && e.Content.Contains("30 calendar days"));
        Assert.Contains(SeedOhioKnowledgeBase.Entries, e => e.LegalReference.StartsWith("OAC 3301-51-09"));
        Assert.Contains(SeedOhioKnowledgeBase.Entries, e => e.LegalReference.StartsWith("OAC 3301-51-11"));
        Assert.Contains(SeedOhioKnowledgeBase.Entries, e => e.LegalReference.StartsWith("OAC 3301-51-05"));
    }

    // ------------------------------------------------------------------ operations

    [Fact]
    public void Up_IsOneInsertOfEveryEntry_AndDown_IsOneDeleteKeyedByStateAndTitle()
    {
        var migration = new SeedOhioKnowledgeBase();

        var insert = Assert.IsType<InsertDataOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("KnowledgeBaseEntries", insert.Table);
        Assert.Equal(SeedOhioKnowledgeBase.Entries.Count, insert.Values.GetLength(0));
        var columns = insert.Columns.ToList();
        var state = columns.IndexOf("State");
        var title = columns.IndexOf("Title");
        var order = columns.IndexOf("DisplayOrder");
        var active = columns.IndexOf("IsActive");
        Assert.DoesNotContain("Id", columns); // identity — never a fixed id that could collide with admin-created rows
        for (var i = 0; i < insert.Values.GetLength(0); i++)
        {
            Assert.Equal("OH", insert.Values[i, state]);
            Assert.Equal(SeedOhioKnowledgeBase.Entries[i].Title, insert.Values[i, title]);
            Assert.Equal(SeedOhioKnowledgeBase.FirstDisplayOrder + i, insert.Values[i, order]);
            Assert.Equal(true, insert.Values[i, active]);
        }

        var delete = Assert.IsType<DeleteDataOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal("KnowledgeBaseEntries", delete.Table);
        Assert.Equal(new[] { "State", "Title" }, delete.KeyColumns);
        var deleted = new HashSet<(string, string)>();
        for (var i = 0; i < delete.KeyValues.GetLength(0); i++)
            deleted.Add((Assert.IsType<string>(delete.KeyValues[i, 0]), Assert.IsType<string>(delete.KeyValues[i, 1])));
        Assert.Equal(SeedOhioKnowledgeBase.Entries.Select(e => ("OH", e.Title)).ToHashSet(), deleted);
    }

    [Fact]
    public void Applied_UpInsertsTheEntries_AndDownRemovesExactlyThoseRows()
    {
        using var ctx = CreateContext();
        // Rows Down must leave alone: a federal entry, an admin-created Ohio entry, and a same-title entry for another state.
        var seededTitle = SeedOhioKnowledgeBase.Entries[0].Title;
        ctx.KnowledgeBaseEntries.AddRange(
            new Domain.Entities.KnowledgeBaseEntry { Title = "Federal entry", Content = "c", Category = "rights", State = null },
            new Domain.Entities.KnowledgeBaseEntry { Title = "Admin-added Ohio entry", Content = "c", Category = "tips", State = "OH" },
            new Domain.Entities.KnowledgeBaseEntry { Title = seededTitle, Content = "c", Category = "rights", State = "PA" });
        ctx.SaveChanges();

        var migration = new SeedOhioKnowledgeBase();
        Apply(ctx, migration.UpOperations);

        var ohio = ctx.KnowledgeBaseEntries.AsNoTracking().Where(e => e.State == "OH").ToList();
        Assert.Equal(SeedOhioKnowledgeBase.Entries.Count + 1, ohio.Count);
        var seeded = ohio.Where(e => e.Title != "Admin-added Ohio entry").OrderBy(e => e.DisplayOrder).ToList();
        Assert.Equal(SeedOhioKnowledgeBase.Entries.Select(e => e.Title), seeded.Select(e => e.Title));
        Assert.All(seeded, e =>
        {
            Assert.True(e.IsActive);
            Assert.Equal(SeedOhioKnowledgeBase.SeededAt, e.CreatedAt);
            Assert.Equal(SeedOhioKnowledgeBase.SeededAt, e.UpdatedAt);
            Assert.True(e.Id > 0);
        });
        Assert.Equal(SeedOhioKnowledgeBase.FirstDisplayOrder, seeded[0].DisplayOrder);

        Apply(ctx, migration.DownOperations);

        var remaining = ctx.KnowledgeBaseEntries.AsNoTracking().OrderBy(e => e.Id).Select(e => new { e.Title, e.State }).AsEnumerable().Select(e => (e.Title, e.State)).ToList();
        Assert.Equal(new[] { ("Federal entry", (string?)null), ("Admin-added Ohio entry", "OH"), (seededTitle, "PA") }, remaining);
    }

    private static void Apply(ApplicationDbContext ctx, IReadOnlyList<MigrationOperation> operations)
    {
        var generator = ctx.GetService<IMigrationsSqlGenerator>();
        var connection = ctx.Database.GetDbConnection();
        foreach (var command in generator.Generate(operations, ctx.Model))
        {
            using var dbCommand = connection.CreateCommand();
            dbCommand.CommandText = command.CommandText;
            dbCommand.ExecuteNonQuery();
        }
    }

    public void Dispose() => _connection.Dispose();
}
