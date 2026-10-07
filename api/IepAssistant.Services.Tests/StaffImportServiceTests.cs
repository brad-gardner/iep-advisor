using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using IepAssistant.Services.Security;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 6 review follow-up: <see cref="StaffImportService"/> now sets an
/// explicit <see cref="ServiceErrorKind"/> on every failure <see cref="Api.Controllers.DistrictImportsController"/>
/// maps to a status (mirrors <see cref="RosterImportService"/> — see <see cref="RosterImportServiceTests"/>
/// for the shared row-evaluation/commit coverage). This suite asserts one real failing call per kind the
/// service can return (Forbidden/NotFound/Validation) so a status regression shows up here, not only via
/// message-text matching.
/// </summary>
public sealed class StaffImportServiceTests : IDisposable
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly RosterTestDb _db = new();
    private readonly CapturingAuditLogger _audit = new();
    private readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-32-bytes-long-0123456789",
            ["Jwt:Issuer"] = "IepAssistant.Api",
            ["Jwt:Audience"] = "IepAssistant.Client",
            ["Jwt:ExpiryInDays"] = "7",
            ["App:FrontendUrl"] = "http://localhost:5173"
        }!)
        .Build();

    private StaffImportService CreateService(ApplicationDbContext ctx)
    {
        var org = new OrgAccessService(ctx);
        var invites = new StaffInviteService(ctx, org, new TestSupport.TestEmailServiceBase(), new JwtTokenFactory(_configuration),
            new InviteLinkExposure(false), _configuration, NullLogger<StaffInviteService>.Instance, TestSupport.TestLocalizers.Messages());
        return new StaffImportService(ctx, org, invites, _audit, NullLogger<StaffImportService>.Instance, TestSupport.TestLocalizers.Messages());
    }

    private (int District, int SchoolA, int Admin) Org()
    {
        var district = _db.District(stateCode: "OH");
        var schoolA = _db.School(district, "Maple Elementary", "OH");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        return (district, schoolA, admin);
    }

    [Fact]
    public async Task GenerateTemplate_NonAdmin_IsForbidden()
    {
        var o = Org();
        var (teacher, _) = _db.Staff("t@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var result = await CreateService(ctx).GenerateTemplateAsync(teacher);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task Commit_UnknownBatch_IsNotFound()
    {
        var o = Org();

        using var ctx = _db.Context();
        var result = await CreateService(ctx).CommitAsync(o.Admin, batchId: 999999, commitValid: false);

        Assert.False(result.Success);
        Assert.Equal("Import not found.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
    }

    [Fact]
    public async Task Preview_RejectsXlsm_IsValidation()
    {
        var o = Org();
        var upload = new ImportUploadModel { FileName = "staff.xlsm", ContentType = Xlsx, Length = 3, Content = new byte[] { 1, 2, 3 } };

        using var ctx = _db.Context();
        var result = await CreateService(ctx).PreviewAsync(o.Admin, upload);

        Assert.False(result.Success);
        Assert.Contains(".xlsm", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
        Assert.Empty(ctx.ImportBatches);
    }

    public void Dispose() => _db.Dispose();
}
