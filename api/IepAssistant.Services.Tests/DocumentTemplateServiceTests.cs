using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Phase 1 coverage for the State Document Template Engine: the DocumentType lookup seed rows,
/// template creation (with an initial Draft v1), (StateCode, DocumentTypeId) uniqueness, state-code
/// normalization, and rejection of unknown/inactive document types. Real SQLite in-memory engine
/// (same pattern as IepDraftServiceTests); EnsureCreated applies the HasData seed.
/// </summary>
public sealed class DocumentTemplateServiceTests : IDisposable
{
    private const int AdminUserId = 1;

    // Seeded DocumentType ids (see DocumentTypeConfiguration).
    private const int IepTypeId = 1;
    private const int Section504TypeId = 2;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public DocumentTemplateServiceTests()
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

    private DocumentTemplateService CreateService(ApplicationDbContext ctx)
        => new(ctx, _audit, NullLogger<DocumentTemplateService>.Instance);

    // ---------------------------------------------------------------- Seed rows

    [Fact]
    public void DocumentTypeSeedRows_ArePresent()
    {
        using var ctx = CreateContext();
        var types = ctx.DocumentTypes.AsNoTracking().OrderBy(t => t.Id).ToList();

        Assert.Equal(3, types.Count);
        Assert.Equal("IEP", types[0].Key);
        Assert.Equal("Section504", types[1].Key);
        Assert.Equal("ETR", types[2].Key);
        Assert.All(types, t => Assert.True(t.IsActive));
    }

    [Fact]
    public async Task ListDocumentTypes_ReturnsActiveRows()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).ListDocumentTypesAsync();

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Count);
        Assert.Contains(result.Data!, t => t.Key == "IEP");
        Assert.Contains(result.Data!, t => t.Key == "ETR");
    }

    // ---------------------------------------------------------------- Create

    [Fact]
    public async Task CreateTemplate_CreatesTemplateWithDraftV1()
    {
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP");

            Assert.True(result.Success, result.Message);
            var template = result.Data!;
            Assert.Equal("OH", template.StateCode);
            Assert.Equal(IepTypeId, template.DocumentTypeId);
            Assert.Equal("IEP", template.DocumentTypeKey);
            Assert.Equal("Ohio IEP", template.Name);
            Assert.NotNull(template.LatestVersion);
            Assert.Equal(1, template.LatestVersion!.VersionNumber);
            Assert.Equal(TemplateVersionStatus.Draft, template.LatestVersion.Status);
            Assert.Null(template.LatestVersion.PublishedAt);
        }

        // Persisted: exactly one template and one Draft version.
        using (var ctx = CreateContext())
        {
            var template = Assert.Single(ctx.DocumentTemplates.Include(t => t.Versions).ToList());
            Assert.Equal(AdminUserId, template.CreatedById);
            var version = Assert.Single(template.Versions);
            Assert.Equal(1, version.VersionNumber);
            Assert.Equal(TemplateVersionStatus.Draft, version.Status);
        }
    }

    [Fact]
    public async Task CreateTemplate_AllowsNullStateForDefaultTemplate()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, null, IepTypeId, "Default IEP");

        Assert.True(result.Success, result.Message);
        Assert.Null(result.Data!.StateCode);
    }

    [Fact]
    public async Task CreateTemplate_WritesEditAudit()
    {
        _audit.Entries.Clear();

        int templateId;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP");
            Assert.True(result.Success, result.Message);
            templateId = result.Data!.Id;
        }

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Edit, entry.Action);
        Assert.Equal("DocumentTemplate", entry.ResourceType);
        Assert.Equal(templateId, entry.ResourceId);
        Assert.Equal(AdminUserId, entry.ActorUserId);
    }

    [Fact]
    public async Task CreateTemplate_DuplicateRejected_WritesNoAudit()
    {
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP")).Success);

        _audit.Entries.Clear();

        using (var ctx = CreateContext())
        {
            var dup = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP again");
            Assert.False(dup.Success);
        }

        // A rejected create is not a governance event — nothing is recorded.
        Assert.Empty(_audit.Entries);
    }

    // ---------------------------------------------------------------- Uniqueness

    [Fact]
    public async Task CreateTemplate_RejectsDuplicateStateAndDocumentType()
    {
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP")).Success);

        using (var ctx = CreateContext())
        {
            var dup = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP again");
            Assert.False(dup.Success);
            Assert.Contains("already exists", dup.Message!, StringComparison.OrdinalIgnoreCase);
        }

        // Only the first template was persisted.
        using (var ctx = CreateContext())
            Assert.Single(ctx.DocumentTemplates.Where(t => t.StateCode == "OH" && t.DocumentTypeId == IepTypeId).ToList());
    }

    [Fact]
    public async Task CreateTemplate_DuplicateDetection_UsesNormalizedState()
    {
        // "OH" then "oh" must collide because the state code is normalized before the uniqueness check.
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP")).Success);

        using (var ctx = CreateContext())
        {
            var dup = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "oh", IepTypeId, "ohio lower");
            Assert.False(dup.Success);
            Assert.Contains("already exists", dup.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CreateTemplate_RejectsDuplicateDefaultTemplate_ForSameDocumentType()
    {
        // Only one default (null-state) template may exist per document type. The service pre-check
        // guards this regardless of provider NULL-uniqueness semantics.
        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).CreateTemplateAsync(AdminUserId, null, IepTypeId, "Default IEP")).Success);

        using (var ctx = CreateContext())
        {
            var dup = await CreateService(ctx).CreateTemplateAsync(AdminUserId, null, IepTypeId, "Default IEP again");
            Assert.False(dup.Success);
            Assert.Contains("already exists", dup.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Single(ctx.DocumentTemplates.Where(t => t.StateCode == null && t.DocumentTypeId == IepTypeId).ToList());
    }

    [Fact]
    public async Task CreateTemplate_SameStateDifferentDocumentType_IsAllowed()
    {
        using var ctx = CreateContext();
        var svc = CreateService(ctx);

        Assert.True((await svc.CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP")).Success);
        Assert.True((await svc.CreateTemplateAsync(AdminUserId, "OH", Section504TypeId, "Ohio 504")).Success);
    }

    // ---------------------------------------------------------------- State normalization

    [Fact]
    public async Task CreateTemplate_NormalizesStateCodeToUppercase()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "oh", IepTypeId, "Ohio IEP");

        Assert.True(result.Success, result.Message);
        Assert.Equal("OH", result.Data!.StateCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTemplate_BlankState_StaysNull(string? state)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, state, IepTypeId, "Default IEP");

        Assert.True(result.Success, result.Message);
        Assert.Null(result.Data!.StateCode);
    }

    [Theory]
    [InlineData("Ohio")]
    [InlineData("O")]
    [InlineData("O1")]
    public async Task CreateTemplate_InvalidStateCode_IsRejected(string state)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, state, IepTypeId, "Bad");

        Assert.False(result.Success);
        Assert.Contains("2-letter", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Document type validation

    [Fact]
    public async Task CreateTemplate_UnknownDocumentType_IsRejected()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", 999, "Nope");

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateTemplate_InactiveDocumentType_IsRejected()
    {
        using (var ctx = CreateContext())
        {
            var etr = ctx.DocumentTypes.Single(t => t.Id == EtrTypeId);
            etr.IsActive = false;
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", EtrTypeId, "Ohio ETR");
            Assert.False(result.Success);
            Assert.Contains("not active", result.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CreateTemplate_BlankName_IsRejected()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "   ");

        Assert.False(result.Success);
        Assert.Contains("name is required", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- List

    [Fact]
    public async Task ListTemplates_ReturnsTemplatesWithLatestVersionSummary()
    {
        using (var ctx = CreateContext())
        {
            var svc = CreateService(ctx);
            Assert.True((await svc.CreateTemplateAsync(AdminUserId, "OH", IepTypeId, "Ohio IEP")).Success);
            Assert.True((await svc.CreateTemplateAsync(AdminUserId, null, EtrTypeId, "Default ETR")).Success);
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).ListTemplatesAsync();
            Assert.True(result.Success);
            Assert.Equal(2, result.Data!.Count);
            Assert.All(result.Data!, t =>
            {
                Assert.NotNull(t.LatestVersion);
                Assert.Equal(1, t.LatestVersion!.VersionNumber);
                Assert.Equal(TemplateVersionStatus.Draft, t.LatestVersion.Status);
            });
        }
    }

    public void Dispose() => _connection.Dispose();
}
