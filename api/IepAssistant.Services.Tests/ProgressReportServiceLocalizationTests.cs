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
/// Multilingual plan (2026-10-06) phase 3: <see cref="ProgressReportService"/>'s <see cref="ServiceResult"/>
/// failure messages render in the active UI culture, and status comes from
/// <see cref="ServiceResult.ErrorKind"/> — never from matching (possibly Spanish) message text — via
/// <see cref="IepAssistant.Api.Extensions.ServiceFailureMapperExtensions.MapServiceFailure"/>.
/// </summary>
public sealed class ProgressReportServiceLocalizationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public ProgressReportServiceLocalizationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private static ProgressReportService CreateService(ApplicationDbContext ctx) => new(
        new ProgressReportRepository(ctx),
        new IepDocumentRepository(ctx),
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
    public async Task Create_ForUnknownIep_UnderEnglishCulture_MessageIsEnglish()
    {
        using var _lang = CultureScope.For("en");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(iepDocumentId: -1, userId: 1, new CreateProgressReportModel());

        Assert.False(result.Success);
        Assert.Equal("IEP not found.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task Create_ForUnknownIep_UnderSpanishCulture_MessageIsSpanish_AndMapsTo404ViaErrorKind()
    {
        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(iepDocumentId: -1, userId: 1, new CreateProgressReportModel());

        Assert.False(result.Success);
        Assert.Equal("IEP no encontrado.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<NotFoundObjectResult>(action);
    }

    private sealed class TestController : ControllerBase
    {
    }

    public void Dispose() => _connection.Dispose();
}
