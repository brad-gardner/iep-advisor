using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 3: <see cref="IepDocumentService"/>'s <see cref="ServiceResult"/>
/// failure messages render in the active UI culture, and status comes from
/// <see cref="ServiceResult.ErrorKind"/> — never from matching (possibly Spanish) message text — via
/// <see cref="IepAssistant.Api.Extensions.ServiceFailureMapperExtensions.MapServiceFailure"/>.
/// </summary>
public sealed class IepDocumentServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public IepDocumentServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static IepDocumentService CreateService(ApplicationDbContext ctx) => new(
        new IepDocumentRepository(ctx),
        new ChildProfileRepository(ctx),
        new AccessService(ctx),
        new NeverCalledBlobStorage(),
        ctx,
        TestSupport.TestLocalizers.Messages());

    private sealed class NeverCalledBlobStorage : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null) => throw new NotImplementedException();
    }

    private int SeedOwnerWithDocument(out int ownerUserId)
    {
        using var ctx = CreateContext();
        var owner = new User { Email = "iepdoc-owner@example.com", PasswordHash = "x", FirstName = "Owen", LastName = "Owner", Role = UserRole.Parent };
        ctx.Users.Add(owner);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = owner.Id, FirstName = "Kid", IsActive = true };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();

        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.SaveChanges();

        var document = new IepDocument { ChildProfileId = child.Id, Status = "created", CreatedById = owner.Id, UpdatedById = owner.Id };
        ctx.IepDocuments.Add(document);
        ctx.SaveChanges();

        ownerUserId = owner.Id;
        return document.Id;
    }

    [Fact]
    public async Task AttachFile_ToUnknownDocument_UnderEnglishCulture_MessageIsEnglish()
    {
        using var _lang = CultureScope.For("en");
        using var ctx = CreateContext();
        using var stream = new MemoryStream();
        var result = await CreateService(ctx).AttachFileAsync(id: -1, userId: 1, "x.pdf", stream, 0);

        Assert.False(result.Success);
        Assert.Equal("Document not found.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task AttachFile_ToUnknownDocument_UnderSpanishCulture_MessageIsSpanish_AndMapsTo404ViaErrorKind()
    {
        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        using var stream = new MemoryStream();
        var result = await CreateService(ctx).AttachFileAsync(id: -1, userId: 1, "x.pdf", stream, 0);

        Assert.False(result.Success);
        Assert.Equal("Documento no encontrado.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task AttachFile_WhileProcessing_UnderSpanishCulture_MessageIsSpanish_AndMapsTo409ViaErrorKind()
    {
        var documentId = SeedOwnerWithDocument(out var ownerUserId);
        using (var ctx = CreateContext())
        {
            var doc = await ctx.IepDocuments.SingleAsync(d => d.Id == documentId);
            doc.Status = "processing";
            await ctx.SaveChangesAsync();
        }

        using var _lang = CultureScope.For("es");
        using var ctx2 = CreateContext();
        using var stream = new MemoryStream();
        var result = await CreateService(ctx2).AttachFileAsync(documentId, ownerUserId, "x.pdf", stream, 0);

        Assert.False(result.Success);
        Assert.Equal(
            "No se puede reemplazar el archivo mientras el documento se está procesando. Espere a que el procesamiento termine.",
            result.Message);
        Assert.Equal(ServiceErrorKind.Conflict, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<ConflictObjectResult>(action);
    }

    private sealed class TestController : ControllerBase
    {
    }

    public void Dispose() => _connection.Dispose();
}
