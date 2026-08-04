using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Phase 2 coverage for the State Document Template Engine authoring service: building a version with
/// every FieldType (incl. Table), publish gating (empty template / empty section / invalid config),
/// Draft-only editing, forking a new Draft from Published (verbatim key carry-forward, one-draft rule),
/// reorder keeping FieldKey stable, optimistic concurrency, and immutability of Published content.
/// Real SQLite in-memory engine WITH the <see cref="ImmutableVersionInterceptor"/> wired in so the
/// immutability tests actually exercise it (same pattern as IepVersionServiceTests).
/// </summary>
public sealed class TemplateAuthoringServiceTests : IDisposable
{
    private const int AdminUserId = 1;
    private const int IepTypeId = 1;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public TemplateAuthoringServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new ImmutableVersionInterceptor())
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);
    private TemplateAuthoringService CreateService(ApplicationDbContext ctx)
        => new(ctx, _audit, NullLogger<TemplateAuthoringService>.Instance);
    private DocumentTemplateService CreateTemplateService(ApplicationDbContext ctx)
        => new(ctx, _audit, NullLogger<DocumentTemplateService>.Instance);

    // ---------------------------------------------------------------- Helpers

    /// <summary>Creates a (OH, IEP) template with its initial empty Draft v1; returns (templateId, versionId).</summary>
    private async Task<(int TemplateId, int VersionId)> SeedDraftTemplateAsync()
    {
        using var ctx = CreateContext();
        var created = await CreateTemplateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP");
        Assert.True(created.Success, created.Message);
        return (created.Data!.Id, created.Data.LatestVersion!.Id);
    }

    private static string SelectConfig(params string[] values)
        => JsonSerializer.Serialize(
            new SelectFieldConfig { Options = values.Select(v => new SelectOption { Value = v, Label = v }).ToList() },
            TemplateFieldConfigValidator.JsonOptions);

    private static string TableConfig(int? minRows = null, int? maxRows = null, params (FieldType Type, string Label, string? Cfg)[] columns)
        => JsonSerializer.Serialize(
            new TableFieldConfig
            {
                MinRows = minRows,
                MaxRows = maxRows,
                Columns = columns.Select(c => new TableColumn
                {
                    ColumnKey = Guid.NewGuid(), Type = c.Type, Label = c.Label, ConfigJson = c.Cfg
                }).ToList()
            },
            TemplateFieldConfigValidator.JsonOptions);

    /// <summary>Adds a section and returns its id.</summary>
    private async Task<int> AddSectionAsync(int versionId, string title)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddSectionAsync(AdminUserId, versionId, title, null);
        Assert.True(result.Success, result.Message);
        return result.Data!.Sections.Single(s => s.Title == title).Id;
    }

    // ---------------------------------------------------------------- Build + publish

    [Fact]
    public async Task BuildVersionWithEveryFieldType_ThenPublishV1_Succeeds()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "Overview");

        using (var ctx = CreateContext())
        {
            var svc = CreateService(ctx);
            Assert.True((await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "Name", true, null, null)).Success);
            Assert.True((await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.RichText, "Narrative", false, null, null)).Success);
            Assert.True((await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Date, "DOB", false, null, null)).Success);
            Assert.True((await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Select, "Grade", true, SelectConfig("K", "1", "2"), null)).Success);
            Assert.True((await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Checkbox, "Consent", false, null, null)).Success);

            var tableCfg = TableConfig(minRows: 1, maxRows: 5,
                columns: new[]
                {
                    (FieldType.Text, "Goal", (string?)null),
                    (FieldType.Date, "By", (string?)null),
                    (FieldType.Select, "Status", (string?)SelectConfig("Open", "Met"))
                });
            Assert.True((await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "Goals", true, tableCfg, null)).Success);
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).PublishAsync(AdminUserId, templateId, null);
            Assert.True(result.Success, result.Message);
            Assert.Equal(TemplateVersionStatus.Published, result.Data!.Status);
            Assert.NotNull(result.Data.PublishedAt);
            Assert.Equal(1, result.Data.VersionNumber);
            Assert.Equal(6, result.Data.Sections.Single().Fields.Count);
        }
    }

    [Fact]
    public async Task Publish_WritesPublishAudit()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "Overview");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "Name", true, null, null)).Success);

        // Clear the seed/authoring audits so we assert only the publish event.
        _audit.Entries.Clear();
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Publish, entry.Action);
        Assert.Equal("DocumentTemplateVersion", entry.ResourceType);
        Assert.Equal(versionId, entry.ResourceId);
        Assert.Equal(AdminUserId, entry.ActorUserId);
    }

    [Fact]
    public async Task Publish_WhenRejected_WritesNoAudit()
    {
        // A template with an empty Draft (no sections) cannot be published.
        var (templateId, _) = await SeedDraftTemplateAsync();

        _audit.Entries.Clear();
        using (var ctx = CreateContext())
            Assert.False((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task CreateDraftFromPublished_WritesEditAudit()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "Overview");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "Name", true, null, null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        _audit.Entries.Clear();
        int draftId;
        using (var ctx = CreateContext())
        {
            var fork = await CreateService(ctx).CreateDraftFromPublishedAsync(AdminUserId, templateId);
            Assert.True(fork.Success, fork.Message);
            draftId = fork.Data!.Id;
        }

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Edit, entry.Action);
        Assert.Equal("DocumentTemplateVersion", entry.ResourceType);
        Assert.Equal(draftId, entry.ResourceId);
        Assert.Equal(AdminUserId, entry.ActorUserId);
    }

    // ---------------------------------------------------------------- Publish gating

    [Fact]
    public async Task Publish_NoSections_IsRejected()
    {
        var (templateId, _) = await SeedDraftTemplateAsync();

        using var ctx = CreateContext();
        var result = await CreateService(ctx).PublishAsync(AdminUserId, templateId, null);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("at least one section", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Publish_SectionWithNoFields_IsRejected()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        await AddSectionAsync(versionId, "Empty");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).PublishAsync(AdminUserId, templateId, null);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("at least one field", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Publish_WithCorruptedFieldConfig_IsRejected()
    {
        // A valid Select saved through the service, then corrupted directly on the draft (options removed),
        // exercises the publish-time config backstop independent of the on-save validation.
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "Overview");

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Select, "Grade", true, SelectConfig("K"), null)).Success);

        using (var ctx = CreateContext())
        {
            var field = ctx.TemplateFields.Single();
            field.ConfigJson = "{\"options\":[]}";
            ctx.SaveChanges(); // draft edit — allowed by the interceptor
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).PublishAsync(AdminUserId, templateId, null);
            Assert.False(result.Success);
            Assert.Contains(result.Errors, e => e.Contains("at least one option", StringComparison.OrdinalIgnoreCase));
        }
    }

    // ---------------------------------------------------------------- On-save config validation

    [Fact]
    public async Task AddField_SelectWithNoOptions_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Select, "Grade", true, "{\"options\":[]}", null);

        Assert.False(result.Success);
        Assert.Contains("at least one option", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_SelectWithDuplicateOptionValues_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Select, "Grade", true, SelectConfig("A", "A"), null);

        Assert.False(result.Success);
        Assert.Contains("unique", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_TableWithNoColumns_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "T", true, TableConfig(), null);

        Assert.False(result.Success);
        Assert.Contains("at least one column", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_TableMinRowsExceedMax_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        var cfg = TableConfig(minRows: 5, maxRows: 2, columns: new[] { (FieldType.Text, "C", (string?)null) });

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "T", true, cfg, null);

        Assert.False(result.Success);
        Assert.Contains("minimum rows cannot exceed", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_TableInsideTable_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        var innerTable = TableConfig(columns: new[] { (FieldType.Text, "Inner", (string?)null) });
        var cfg = TableConfig(columns: new[] { (FieldType.Table, "Nested", (string?)innerTable) });

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "T", true, cfg, null);

        Assert.False(result.Success);
        Assert.Contains("cannot itself be a table", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_TableColumnWithEmptyKey_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        // Column with the default (empty) ColumnKey — hand-built JSON to bypass the helper's key generation.
        var cfg = "{\"columns\":[{\"columnKey\":\"00000000-0000-0000-0000-000000000000\",\"type\":\"Text\",\"label\":\"C\",\"required\":false}]}";

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "T", true, cfg, null);

        Assert.False(result.Success);
        Assert.Contains("stable key", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_TableColumnsWithDuplicateKeys_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        var dupKey = Guid.NewGuid();
        var cfg = JsonSerializer.Serialize(
            new TableFieldConfig
            {
                Columns = new()
                {
                    new TableColumn { ColumnKey = dupKey, Type = FieldType.Text, Label = "A" },
                    new TableColumn { ColumnKey = dupKey, Type = FieldType.Text, Label = "B" }
                }
            },
            TemplateFieldConfigValidator.JsonOptions);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "T", true, cfg, null);

        Assert.False(result.Success);
        Assert.Contains("column keys must be unique", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddField_RichTextInsideTable_IsRejected()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        var cfg = TableConfig(columns: new[] { (FieldType.RichText, "Notes", (string?)null) });

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Table, "T", true, cfg, null);

        Assert.False(result.Success);
        Assert.Contains("cannot be rich text", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Draft-only editing

    [Fact]
    public async Task EditingPublishedVersion_IsRejected()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "F", false, null, null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        // The version is now Published — adding a section to it must be rejected.
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).AddSectionAsync(AdminUserId, versionId, "Late", null);
            Assert.False(result.Success);
            Assert.Contains("Draft", result.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- Fork

    [Fact]
    public async Task CreateDraftFromPublished_ForksV2_CopyingSectionsAndFieldsVerbatim()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "Overview");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Select, "Grade", true, SelectConfig("K", "1"), null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        // Capture the published keys.
        Guid publishedSectionKey, publishedFieldKey;
        using (var ctx = CreateContext())
        {
            var pubVersion = await CreateService(ctx).GetVersionAsync(versionId);
            publishedSectionKey = pubVersion.Data!.Sections.Single().SectionKey;
            publishedFieldKey = pubVersion.Data.Sections.Single().Fields.Single().FieldKey;
        }

        TemplateVersionDetailModel draft;
        using (var ctx = CreateContext())
        {
            var fork = await CreateService(ctx).CreateDraftFromPublishedAsync(AdminUserId, templateId);
            Assert.True(fork.Success, fork.Message);
            draft = fork.Data!;
        }

        Assert.Equal(TemplateVersionStatus.Draft, draft.Status);
        Assert.Equal(2, draft.VersionNumber);
        Assert.NotEqual(versionId, draft.Id);

        var draftSection = Assert.Single(draft.Sections);
        Assert.Equal(publishedSectionKey, draftSection.SectionKey); // carried verbatim
        var draftField = Assert.Single(draftSection.Fields);
        Assert.Equal(publishedFieldKey, draftField.FieldKey);       // carried verbatim
        Assert.Equal(FieldType.Select, draftField.FieldType);
    }

    [Fact]
    public async Task CreateDraftFromPublished_WhenDraftAlreadyExists_IsRejected()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "F", false, null, null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).CreateDraftFromPublishedAsync(AdminUserId, templateId)).Success); // v2 draft

        using (var ctx = CreateContext())
        {
            var second = await CreateService(ctx).CreateDraftFromPublishedAsync(AdminUserId, templateId);
            Assert.False(second.Success);
            Assert.Contains("already has a Draft", second.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- Reorder

    [Fact]
    public async Task ReorderFields_UpdatesDisplayOrder_WithoutChangingFieldKey()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");

        int f1, f2, f3;
        using (var ctx = CreateContext())
        {
            var svc = CreateService(ctx);
            await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "A", false, null, null);
            await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "B", false, null, null);
            var afterC = await svc.AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "C", false, null, null);
            var fields = afterC.Data!.Sections.Single().Fields;
            f1 = fields.Single(f => f.Label == "A").Id;
            f2 = fields.Single(f => f.Label == "B").Id;
            f3 = fields.Single(f => f.Label == "C").Id;
        }

        // Capture pre-reorder keys.
        Dictionary<int, Guid> keysBefore;
        using (var ctx = CreateContext())
            keysBefore = (await CreateService(ctx).GetVersionAsync(versionId)).Data!
                .Sections.Single().Fields.ToDictionary(f => f.Id, f => f.FieldKey);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).ReorderFieldsAsync(AdminUserId, sectionId, new[] { f3, f1, f2 }, null);
            Assert.True(result.Success, result.Message);
            var ordered = result.Data!.Sections.Single().Fields;
            Assert.Equal(new[] { "C", "A", "B" }, ordered.Select(f => f.Label).ToArray());
            Assert.Equal(new[] { 0, 1, 2 }, ordered.Select(f => f.DisplayOrder).ToArray());
            // FieldKeys unchanged.
            foreach (var f in ordered)
                Assert.Equal(keysBefore[f.Id], f.FieldKey);
        }
    }

    // ---------------------------------------------------------------- Optimistic concurrency

    [Fact]
    public async Task StaleRowVersion_ReturnsConcurrencyError()
    {
        var (_, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "Original");

        // First edit establishes a token.
        byte[] staleToken;
        using (var ctx = CreateContext())
        {
            var edit1 = await CreateService(ctx).UpdateSectionAsync(AdminUserId, sectionId, "Rename1", null);
            Assert.True(edit1.Success, edit1.Message);
            staleToken = edit1.Data!.RowVersion!;
        }

        // A concurrent editor advances the token.
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).UpdateSectionAsync(AdminUserId, sectionId, "Rename2", staleToken)).Success);

        // Re-using the now-stale token must fail with a friendly concurrency error, not a 500.
        using (var ctx = CreateContext())
        {
            var stale = await CreateService(ctx).UpdateSectionAsync(AdminUserId, sectionId, "Rename3", staleToken);
            Assert.False(stale.Success);
            Assert.Contains("changed by someone else", stale.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- Immutability (interceptor)

    [Fact]
    public async Task MutatingPublishedField_IsBlockedByInterceptor()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "F", false, null, null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        using (var ctx = CreateContext())
        {
            var field = ctx.TemplateFields.Single();
            field.Label = "Hacked";
            var ex = Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
            Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task MutatingPublishedVersionEnvelope_IsBlockedByInterceptor()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "F", false, null, null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        using (var ctx = CreateContext())
        {
            var version = ctx.DocumentTemplateVersions.Single(v => v.Id == versionId);
            version.VersionNumber = 99;
            var ex = Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
            Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DeletingPublishedSection_IsBlockedByInterceptor()
    {
        var (templateId, versionId) = await SeedDraftTemplateAsync();
        var sectionId = await AddSectionAsync(versionId, "S");
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).AddFieldAsync(AdminUserId, sectionId, FieldType.Text, "F", false, null, null)).Success);
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).PublishAsync(AdminUserId, templateId, null)).Success);

        using (var ctx = CreateContext())
        {
            var section = ctx.TemplateSections.Single();
            ctx.TemplateSections.Remove(section);
            Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        }
    }

    public void Dispose() => _connection.Dispose();
}
