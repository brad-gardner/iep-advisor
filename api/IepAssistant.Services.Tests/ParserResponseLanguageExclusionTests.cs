using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: the document parsers
/// (<see cref="IepProcessingService"/>/<see cref="EtrProcessingService"/>) must reproduce the source
/// document's own text exactly, regardless of the requester's language — they are the one place
/// <see cref="ResponseLanguage.SystemLine"/> is deliberately never applied. These tests exercise the
/// REAL parsing flow (a scripted Claude client, a real SQLite-backed <c>ApplicationDbContext</c>) under
/// an active Spanish culture and assert the captured system prompt carries no response-language
/// instruction — so a future edit that accidentally wires one in fails loudly here, not in production.
/// </summary>
public sealed class ParserResponseLanguageExclusionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ParserResponseLanguageExclusionTests()
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
            => Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3 })); // non-empty; content is never actually parsed as a PDF here

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null) => Task.FromResult($"https://fake.blob/{blobPath}");
    }

    private sealed class CapturingClaudeClient : IClaudeClient
    {
        public ClaudeCompletionRequest? LastRequest { get; private set; }

        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult<string?>("""{"sections": []}""");
        }
    }

    private int SeedChild()
    {
        using var ctx = CreateContext();
        var user = new User { Email = "parser-parent@example.com", PasswordHash = "x", FirstName = "P", LastName = "Parent", Role = UserRole.Parent };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        var child = new ChildProfile { UserId = user.Id, FirstName = "Kid" };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();
        return child.Id;
    }

    [Fact]
    public async Task IepProcessingService_UnderSpanishCulture_SystemPromptCarriesNoResponseLanguageLine()
    {
        var childId = SeedChild();
        int documentId;
        using (var ctx = CreateContext())
        {
            var doc = new IepDocument { ChildProfileId = childId, BlobUri = "ieps/1.pdf", Status = "uploaded" };
            ctx.IepDocuments.Add(doc);
            ctx.SaveChanges();
            documentId = doc.Id;
        }

        var claude = new CapturingClaudeClient();
        using var _ = CultureScope.For("es");
        using var ctx2 = CreateContext();
        var service = new IepProcessingService(
            new IepDocumentRepository(ctx2),
            new ChildProfileRepository(ctx2),
            new AccessService(ctx2),
            new FakeBlobStorage(),
            ctx2,
            claude,
            NullLogger<IepProcessingService>.Instance);

        await service.ProcessDocumentAsync(documentId);

        Assert.NotNull(claude.LastRequest);
        var systemPrompt = claude.LastRequest!.SystemPrompt;
        Assert.DoesNotContain("RESPONSE LANGUAGE", systemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Spanish", systemPrompt, StringComparison.OrdinalIgnoreCase);
        // The exact fixed marker ResponseLanguage.SystemLine would append under "es" — asserted
        // directly against the real helper so this test fails if the marker text ever changes shape.
        Assert.DoesNotContain(ResponseLanguage.SystemLine(System.Globalization.CultureInfo.GetCultureInfo("es")), systemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EtrProcessingService_UnderSpanishCulture_SystemPromptCarriesNoResponseLanguageLine()
    {
        var childId = SeedChild();
        int documentId;
        using (var ctx = CreateContext())
        {
            var doc = new EtrDocument { ChildProfileId = childId, BlobUri = "etrs/1.pdf", Status = "uploaded" };
            ctx.EtrDocuments.Add(doc);
            ctx.SaveChanges();
            documentId = doc.Id;
        }

        var claude = new CapturingClaudeClient();
        using var _ = CultureScope.For("es");
        using var ctx2 = CreateContext();
        var service = new EtrProcessingService(
            new EtrDocumentRepository(ctx2),
            new EtrSectionRepository(ctx2),
            new AccessService(ctx2),
            new FakeBlobStorage(),
            ctx2,
            claude,
            NullLogger<EtrProcessingService>.Instance);

        await service.ProcessDocumentAsync(documentId);

        Assert.NotNull(claude.LastRequest);
        var systemPrompt = claude.LastRequest!.SystemPrompt;
        Assert.DoesNotContain("RESPONSE LANGUAGE", systemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Spanish", systemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ResponseLanguage.SystemLine(System.Globalization.CultureInfo.GetCultureInfo("es")), systemPrompt, StringComparison.Ordinal);
    }

    public void Dispose() => _connection.Dispose();
}
