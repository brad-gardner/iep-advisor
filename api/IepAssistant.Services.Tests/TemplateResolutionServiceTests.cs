using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Phase 3 coverage for template resolution: state-specific Published is preferred over the default;
/// an unknown/blank state falls back to the default; a template with only a Draft version is skipped
/// (never pinned); and no Published template anywhere blocks with a friendly message. Real SQLite
/// in-memory engine (same pattern as DocumentTemplateServiceTests).
/// </summary>
public sealed class TemplateResolutionServiceTests : IDisposable
{
    private const int IepTypeId = 1;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public TemplateResolutionServiceTests()
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

    private TemplateResolutionService CreateService(ApplicationDbContext ctx)
        => new(ctx, NullLogger<TemplateResolutionService>.Instance);

    /// <summary>Seeds a template for (state, docType) with the given versions; returns the created version ids keyed by version number.</summary>
    private Dictionary<int, int> SeedTemplate(string? state, int docTypeId, params (int Number, TemplateVersionStatus Status)[] versions)
    {
        using var ctx = CreateContext();
        var template = new DocumentTemplate
        {
            StateCode = state,
            DocumentTypeId = docTypeId,
            Name = $"{state ?? "Default"} {docTypeId}",
            Versions = versions.Select(v => new DocumentTemplateVersion
            {
                VersionNumber = v.Number,
                Status = v.Status,
                PublishedAt = v.Status == TemplateVersionStatus.Published ? DateTime.UtcNow : null
            }).ToList()
        };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();

        return template.Versions.ToDictionary(v => v.VersionNumber, v => v.Id);
    }

    // ---------------------------------------------------------------- State-specific preferred

    [Fact]
    public async Task Resolve_PrefersStateSpecificPublished_OverDefault()
    {
        var ohVersions = SeedTemplate("OH", IepTypeId, (1, TemplateVersionStatus.Published));
        SeedTemplate(null, IepTypeId, (1, TemplateVersionStatus.Published));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("OH", IepTypeId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ohVersions[1], result.Data!.DocumentTemplateVersionId);
        Assert.Equal("OH", result.Data.StateCode);
        Assert.False(result.Data.UsedDefault);
    }

    [Fact]
    public async Task Resolve_PicksHighestPublishedVersionNumber()
    {
        var versions = SeedTemplate("OH", IepTypeId,
            (1, TemplateVersionStatus.Published),
            (2, TemplateVersionStatus.Published),
            (3, TemplateVersionStatus.Draft)); // newer draft must be ignored

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("oh", IepTypeId); // lower-case normalizes to OH

        Assert.True(result.Success, result.Message);
        Assert.Equal(versions[2], result.Data!.DocumentTemplateVersionId);
        Assert.Equal(2, result.Data.VersionNumber);
    }

    // ---------------------------------------------------------------- Fallback to default

    [Fact]
    public async Task Resolve_UnknownState_FallsBackToDefault()
    {
        var defaultVersions = SeedTemplate(null, IepTypeId, (1, TemplateVersionStatus.Published));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("TX", IepTypeId); // no TX template

        Assert.True(result.Success, result.Message);
        Assert.Equal(defaultVersions[1], result.Data!.DocumentTemplateVersionId);
        Assert.Null(result.Data.StateCode);
        Assert.True(result.Data.UsedDefault);
    }

    [Fact]
    public async Task Resolve_BlankState_UsesDefault()
    {
        var defaultVersions = SeedTemplate(null, IepTypeId, (1, TemplateVersionStatus.Published));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("   ", IepTypeId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(defaultVersions[1], result.Data!.DocumentTemplateVersionId);
        Assert.True(result.Data.UsedDefault);
    }

    [Fact]
    public async Task Resolve_StateTemplateHasOnlyDraft_FallsBackToDefaultPublished()
    {
        SeedTemplate("OH", IepTypeId, (1, TemplateVersionStatus.Draft)); // unpublished only
        var defaultVersions = SeedTemplate(null, IepTypeId, (1, TemplateVersionStatus.Published));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("OH", IepTypeId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(defaultVersions[1], result.Data!.DocumentTemplateVersionId);
        Assert.True(result.Data.UsedDefault); // never pins the state Draft
    }

    // ---------------------------------------------------------------- Blocked

    [Fact]
    public async Task Resolve_NoTemplateAtAll_IsBlocked()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("OH", IepTypeId);

        Assert.False(result.Success);
        Assert.Contains("no document template", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_OnlyDraftsAnywhere_IsBlocked_NeverPinsDraft()
    {
        SeedTemplate("OH", IepTypeId, (1, TemplateVersionStatus.Draft));
        SeedTemplate(null, IepTypeId, (1, TemplateVersionStatus.Draft));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync("OH", IepTypeId);

        Assert.False(result.Success);
        Assert.Contains("no document template", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_DifferentDocumentType_DoesNotLeakAcrossTypes()
    {
        // A Published IEP default exists, but resolving an ETR must NOT borrow it.
        SeedTemplate(null, IepTypeId, (1, TemplateVersionStatus.Published));

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ResolveAsync(null, EtrTypeId);

        Assert.False(result.Success);
        Assert.Contains("no document template", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _connection.Dispose();
}
