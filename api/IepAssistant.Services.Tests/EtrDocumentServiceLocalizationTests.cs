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
/// Multilingual plan (2026-10-06) phase 3: <see cref="EtrDocumentService"/>'s <see cref="ServiceResult"/>
/// failure messages render in the active UI culture, and status comes from
/// <see cref="ServiceResult.ErrorKind"/> — never from matching (possibly Spanish) message text — via
/// <see cref="IepAssistant.Api.Extensions.ServiceFailureMapperExtensions.MapServiceFailure"/>.
/// </summary>
public sealed class EtrDocumentServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public EtrDocumentServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static EtrDocumentService CreateService(ApplicationDbContext ctx) => new(
        new EtrDocumentRepository(ctx),
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

    [Fact]
    public async Task UpdateMetadata_OnUnknownDocument_UnderEnglishCulture_MessageIsEnglish()
    {
        using var _lang = CultureScope.For("en");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).UpdateMetadataAsync(id: -1, userId: 1, new UpdateEtrMetadataModel());

        Assert.False(result.Success);
        Assert.Equal("Document not found.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task UpdateMetadata_OnUnknownDocument_UnderSpanishCulture_MessageIsSpanish_AndMapsTo404ViaErrorKind()
    {
        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).UpdateMetadataAsync(id: -1, userId: 1, new UpdateEtrMetadataModel());

        Assert.False(result.Success);
        Assert.Equal("Documento no encontrado.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task Create_WithInvalidEvaluationType_UnderSpanishCulture_MessageIsSpanish_AndMapsTo400ViaErrorKind()
    {
        using var ctxSeed = CreateContext();
        var owner = new User { Email = "etrdoc-owner@example.com", PasswordHash = "x", FirstName = "Owen", LastName = "Owner", Role = UserRole.Parent };
        ctxSeed.Users.Add(owner);
        ctxSeed.SaveChanges();
        var child = new ChildProfile { UserId = owner.Id, FirstName = "Kid", IsActive = true };
        ctxSeed.ChildProfiles.Add(child);
        ctxSeed.SaveChanges();
        ctxSeed.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = owner.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctxSeed.SaveChanges();

        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(child.Id, owner.Id, new CreateEtrDocumentModel
        {
            EvaluationDate = DateTime.UtcNow,
            EvaluationType = "not-a-real-type",
            DocumentState = "draft"
        });

        Assert.False(result.Success);
        Assert.Equal("Tipo de evaluación no válido. Debe ser: initial, reevaluation, transfer u other.", result.Message);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<BadRequestObjectResult>(action);
    }

    private sealed class TestController : ControllerBase
    {
    }

    public void Dispose() => _connection.Dispose();
}
