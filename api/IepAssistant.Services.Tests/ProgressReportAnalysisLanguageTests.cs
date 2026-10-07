using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: <see cref="ProgressReportAnalysisService.AnalyzeAsync"/> runs
/// entirely in <c>ProgressReportAnalysisWorker</c>, outside any request, and its enqueue call site
/// (<c>ProgressReportsController</c>) belongs to a different, concurrently active work item — so unlike
/// <c>AnalysisRunService</c>/<c>MeetingPrepService</c>, this service cannot capture an ephemeral request
/// culture at create time. Instead it resolves the language from the owning child's parent's own saved
/// <see cref="User.PreferredLanguage"/>, scoped to exactly the child the report belongs to.
/// </summary>
public sealed class ProgressReportAnalysisLanguageTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ProgressReportAnalysisLanguageTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class FakeBlobStorage : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 }));

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null) => Task.FromResult($"https://fake.blob/{blobPath}");
    }

    private sealed class CapturingClaudeClient : IClaudeClient
    {
        public ClaudeCompletionRequest? LastRequest { get; private set; }

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult<string?>("""{"summary": "ok", "goalProgressFindings": [], "redFlags": []}""");
        }
    }

    private ProgressReportAnalysisService BuildService(ApplicationDbContext ctx, IClaudeClient claude) => new(
        new ProgressReportRepository(ctx),
        new ProgressReportAnalysisRepository(ctx),
        null!,
        new ParentAdvocacyGoalRepository(ctx),
        new AccessService(ctx),
        new FakeBlobStorage(),
        ctx,
        claude,
        TestSupport.TestLocalizers.Ai(),
        NullLogger<ProgressReportAnalysisService>.Instance);

    private int SeedProgressReport(string? ownerPreferredLanguage)
    {
        using var ctx = CreateContext();
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "x", FirstName = "P", LastName = "Parent", Role = UserRole.Parent, PreferredLanguage = ownerPreferredLanguage };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = user.Id, FirstName = "Kid" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();

        var iep = new IepDocument { ChildProfileId = child.Id, Status = "parsed" };
        ctx.IepDocuments.Add(iep);
        ctx.SaveChanges();

        var report = new ProgressReport { IepDocumentId = iep.Id, ChildProfileId = child.Id, BlobUri = "reports/1.pdf", Status = "uploaded" };
        ctx.ProgressReports.Add(report);
        ctx.SaveChanges();
        return report.Id;
    }

    [Fact]
    public async Task AnalyzeAsync_OwnerPreferredLanguageSpanish_AppendsResponseLanguageLine_AndPersistsSpanish()
    {
        var reportId = SeedProgressReport("es");
        var claude = new CapturingClaudeClient();

        using var ctx = CreateContext();
        await BuildService(ctx, claude).AnalyzeAsync(reportId, CancellationToken.None);

        Assert.NotNull(claude.LastRequest);
        Assert.Contains("Spanish", claude.LastRequest!.SystemPrompt);

        var analysis = ctx.Set<ProgressReportAnalysis>().Single(a => a.ProgressReportId == reportId);
        Assert.Equal("completed", analysis.Status);
        Assert.Equal("es", analysis.Language);
    }

    [Fact]
    public async Task AnalyzeAsync_OwnerPreferredLanguageNullOrEnglish_NeverAppendsResponseLanguageLine()
    {
        var reportId = SeedProgressReport(null);
        var claude = new CapturingClaudeClient();

        using var ctx = CreateContext();
        await BuildService(ctx, claude).AnalyzeAsync(reportId, CancellationToken.None);

        Assert.NotNull(claude.LastRequest);
        Assert.DoesNotContain("RESPONSE LANGUAGE", claude.LastRequest!.SystemPrompt, StringComparison.OrdinalIgnoreCase);

        var analysis = ctx.Set<ProgressReportAnalysis>().Single(a => a.ProgressReportId == reportId);
        Assert.Equal("en", analysis.Language);
    }

    public void Dispose() => _connection.Dispose();
}
