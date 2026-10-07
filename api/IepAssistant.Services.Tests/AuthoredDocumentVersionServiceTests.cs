using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Api.Extensions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using IepAssistant.Services.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Phase 4 coverage for the dynamic-template finalize + PDF pipeline: finalize validation (complete
/// missing/invalid list with section + field + row index), immutable version creation, per-(student,
/// docType) VersionNumber independence, the immutability interceptor on AuthoredDocumentVersion, the
/// dynamic PDF composer (determinism, empty-field omission, multi-page table), and the render
/// service/worker path (Pending -> Rendered, failure -> retryable Error). Real SQLite in-memory engine
/// WITH the <see cref="ImmutableVersionInterceptor"/> wired in so immutability is actually exercised.
/// </summary>
public sealed class AuthoredDocumentVersionServiceTests : IDisposable
{
    private const int IepTypeId = 1;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    static AuthoredDocumentVersionServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public AuthoredDocumentVersionServiceTests()
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

    private AuthoredDocumentVersionService CreateService(ApplicationDbContext ctx, IBlobStorageService? blob = null)
        => new(
            ctx,
            new OrgAccessService(ctx),
            new AccessService(ctx),
            new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance, TestSupport.TestLocalizers.Messages()),
            blob ?? new SuccessBlobStorageFake(),
            _audit,
            new GoalRecordService(ctx, new OrgAccessService(ctx), new AccessService(ctx), NullLogger<GoalRecordService>.Instance, TestSupport.TestLocalizers.Messages()),
            NullLogger<AuthoredDocumentVersionService>.Instance,
            TestSupport.TestLocalizers.Messages());

    private AuthoredDocumentPdfService CreatePdfService(ApplicationDbContext ctx, IBlobStorageService blob)
        => new(ctx, new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance, TestSupport.TestLocalizers.Messages()), blob, NullLogger<AuthoredDocumentPdfService>.Instance, TestSupport.TestLocalizers.Pdf());

    // ---- Blob fakes ----
    /// <summary>The smallest byte sequence the upload guard accepts as a PDF.</summary>
    private static readonly byte[] PdfBytes = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 fake");

    private sealed class SuccessBlobStorageFake : IBlobStorageService
    {
        public string? LastBlobPath { get; private set; }
        public byte[]? LastBytes { get; private set; }

        public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            LastBlobPath = blobPath;
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            LastBytes = ms.ToArray();
            return $"https://fake.blob/{blobPath}";
        }

        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream(LastBytes ?? Array.Empty<byte>()));

        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
            => Task.FromResult($"https://fake.blob/{blobPath}?sas=token");
    }

    private sealed class FailingBlobStorageFake : IBlobStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("blob upload exploded");
        public Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("download exploded");
        public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan? expiry = null)
            => Task.FromResult($"https://fake.blob/{blobPath}");
    }

    // ---------------------------------------------------------------- Seed helpers

    private sealed record SchoolScenario(int SchoolId, int CollaboratorUserId, int StudentId);

    private SchoolScenario SeedSchoolWithStudent(string prefix, AccessRole role = AccessRole.Collaborator)
    {
        using var ctx = CreateContext();

        var user = new User { Email = $"{prefix}@example.com", PasswordHash = "x", FirstName = "Ed", LastName = "U", Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();

        var district = new District { Name = $"{prefix} District" };
        ctx.Districts.Add(district);
        ctx.SaveChanges();

        var school = new School { DistrictId = district.Id, Name = $"{prefix} School" };
        ctx.Schools.Add(school);
        ctx.SaveChanges();

        ctx.StaffProfiles.Add(new StaffProfile { UserId = user.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        ctx.SaveChanges();

        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Sam", IsActive = true };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();

        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id, UserId = user.Id, Role = role, IsActive = true
        });
        ctx.SaveChanges();

        return new SchoolScenario(school.Id, user.Id, student.Id);
    }

    private int SeedStranger(string prefix)
    {
        using var ctx = CreateContext();
        var user = new User { Email = $"{prefix}@example.com", PasswordHash = "x", FirstName = "S", LastName = "T", Role = UserRole.Educator };
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user.Id;
    }

    /// <summary>Stable keys of a seeded published template exercising required Text/Select + a required min/max Table.</summary>
    private sealed record TemplateKeys(int VersionId, Guid TextKey, Guid SelectKey, Guid TableKey, Guid Col1Key, Guid Col2Key);

    /// <summary>
    /// Seeds a Published template with: a required Text field, a required Select field (options Yes/No),
    /// and a required Table (minRows=2, maxRows=5) whose first column is a required Text and second an
    /// optional Date. Exercises every finalize-validation branch.
    /// </summary>
    private TemplateKeys SeedTemplate(int docTypeId)
    {
        var textKey = Guid.NewGuid();
        var selectKey = Guid.NewGuid();
        var tableKey = Guid.NewGuid();
        var col1 = Guid.NewGuid();
        var col2 = Guid.NewGuid();

        using var ctx = CreateContext();

        var version = new DocumentTemplateVersion
        {
            VersionNumber = 1,
            Status = TemplateVersionStatus.Published,
            PublishedAt = DateTime.UtcNow
        };
        var template = new DocumentTemplate
        {
            StateCode = null,
            DocumentTypeId = docTypeId,
            Name = "Default template",
            Versions = { version }
        };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();

        var selectConfig = JsonSerializer.Serialize(new
        {
            options = new object[] { new { value = "Yes" }, new { value = "No" } }
        });
        var tableConfig = JsonSerializer.Serialize(new
        {
            columns = new object[]
            {
                new { columnKey = col1, type = "Text", label = "Service", required = true },
                new { columnKey = col2, type = "Date", label = "Start", required = false }
            },
            minRows = 2,
            maxRows = 5
        });

        var section = new TemplateSection
        {
            DocumentTemplateVersionId = version.Id,
            SectionKey = Guid.NewGuid(),
            Title = "Eligibility",
            DisplayOrder = 0,
            Fields =
            {
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = textKey, FieldType = FieldType.Text, Label = "Student Name", Required = true, DisplayOrder = 0 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = selectKey, FieldType = FieldType.Select, Label = "Eligible", Required = true, ConfigJson = selectConfig, DisplayOrder = 1 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = tableKey, FieldType = FieldType.Table, Label = "Services", Required = true, ConfigJson = tableConfig, DisplayOrder = 2 }
            }
        };
        ctx.TemplateSections.Add(section);
        ctx.SaveChanges();

        return new TemplateKeys(version.Id, textKey, selectKey, tableKey, col1, col2);
    }

    private int SeedInstance(SchoolScenario s, TemplateKeys keys, int docTypeId, string valuesJson)
    {
        using var ctx = CreateContext();
        var instance = new DocumentInstance
        {
            SchoolStudentId = s.StudentId,
            DocumentTypeId = docTypeId,
            DocumentTemplateVersionId = keys.VersionId,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = valuesJson,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();
        return instance.Id;
    }

    private string ValidValues(TemplateKeys keys) => $$"""
    {
      "{{keys.TextKey}}": "Alice",
      "{{keys.SelectKey}}": "Yes",
      "{{keys.TableKey}}": [
        { "{{keys.Col1Key}}": "Speech", "{{keys.Col2Key}}": "2026-02-01" },
        { "{{keys.Col1Key}}": "OT" }
      ]
    }
    """;

    /// <summary>
    /// Inserts a finalized <see cref="AuthoredDocumentVersion"/> directly (bypassing finalize validation)
    /// with an optional <see cref="AuthoredDocumentPdf"/> in a chosen render state, so the read/PDF paths
    /// can be exercised without driving a full finalize. The interceptor permits inserts.
    /// </summary>
    private int SeedFinalizedVersion(SchoolScenario s, TemplateKeys keys, int docTypeId, PdfRenderStatus? pdfStatus, int versionNumber = 1)
    {
        using var ctx = CreateContext();
        var version = new AuthoredDocumentVersion
        {
            SchoolStudentId = s.StudentId,
            DocumentTypeId = docTypeId,
            DocumentTemplateVersionId = keys.VersionId,
            VersionNumber = versionNumber,
            ValuesJson = ValidValues(keys),
            FinalizedByUserId = s.CollaboratorUserId,
            FinalizedAt = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
            Pdfs = pdfStatus is PdfRenderStatus status
                ? new List<AuthoredDocumentPdf>
                {
                    new()
                    {
                        RenderStatus = status,
                        RenderedAt = status == PdfRenderStatus.Rendered ? new DateTime(2026, 7, 20, 1, 0, 0, DateTimeKind.Utc) : null,
                        ErrorMessage = status == PdfRenderStatus.Error ? "prior render failed" : null
                    }
                }
                : new List<AuthoredDocumentPdf>()
        };
        ctx.AuthoredDocumentVersions.Add(version);
        ctx.SaveChanges();
        return version.Id;
    }

    private sealed record ParentScenario(int ParentUserId, int ChildProfileId);

    /// <summary>
    /// Seeds a parent User with a ChildProfile they own (Owner ChildAccess) and a ChildLink to
    /// <paramref name="schoolStudentId"/>. The link's active/accepted flags are parameterized so authz
    /// denial paths can be exercised.
    /// </summary>
    private ParentScenario SeedLinkedParent(string prefix, int schoolStudentId, bool linkActive = true, bool linkAccepted = true)
    {
        using var ctx = CreateContext();

        var parent = new User { Email = $"{prefix}-parent@example.com", PasswordHash = "x", FirstName = "P", LastName = "A", Role = UserRole.Parent };
        ctx.Users.Add(parent);
        ctx.SaveChanges();

        var child = new ChildProfile { UserId = parent.Id, FirstName = "Kid", IsActive = true };
        ctx.ChildProfiles.Add(child);
        ctx.SaveChanges();

        ctx.ChildAccesses.Add(new ChildAccess
        {
            ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner,
            IsActive = true, AcceptedAt = DateTime.UtcNow
        });
        ctx.ChildLinks.Add(new ChildLink
        {
            ChildProfileId = child.Id, SchoolStudentId = schoolStudentId,
            IsActive = linkActive, AcceptedAt = linkAccepted ? DateTime.UtcNow : null, LinkedAt = DateTime.UtcNow
        });
        ctx.SaveChanges();

        return new ParentScenario(parent.Id, child.Id);
    }

    // ---------------------------------------------------------------- Finalize validation

    [Fact]
    public async Task Finalize_MissingRequired_ReturnsCompleteErrorList_WithFieldAndRow()
    {
        var s = SeedSchoolWithStudent("val");
        var keys = SeedTemplate(IepTypeId);
        // Text missing (required); Select "Maybe" (non-member); Table has 1 row (< minRows 2) with the
        // required first column empty.
        var values = $$"""
        {
          "{{keys.SelectKey}}": "Maybe",
          "{{keys.TableKey}}": [ { } ]
        }
        """;
        var instanceId = SeedInstance(s, keys, IepTypeId, values);

        ServiceResult<AuthoredDocumentVersionSummaryModel> result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);

        // Required scalar (Text) identified by field label.
        Assert.Contains(result.Errors, e => e.Contains("Student Name") && e.Contains("required", StringComparison.OrdinalIgnoreCase));
        // Required Select with a non-member value.
        Assert.Contains(result.Errors, e => e.Contains("Eligible") && e.Contains("Maybe") && e.Contains("not a valid option", StringComparison.OrdinalIgnoreCase));
        // Table row-bounds (minRows).
        Assert.Contains(result.Errors, e => e.Contains("Services") && e.Contains("At least 2"));
        // Required table column empty, identified with the 1-based row index.
        Assert.Contains(result.Errors, e => e.Contains("Services") && e.Contains("row 1") && e.Contains("Service") && e.Contains("required", StringComparison.OrdinalIgnoreCase));

        // No version created; instance remains an editable Draft.
        using (var ctx = CreateContext())
        {
            Assert.Empty(ctx.AuthoredDocumentVersions.ToList());
            Assert.Equal(DocumentInstanceStatus.Draft, ctx.DocumentInstances.Single(i => i.Id == instanceId).Status);
        }
    }

    [Fact]
    public async Task Finalize_MaxRowsExceeded_IsRejected()
    {
        var s = SeedSchoolWithStudent("maxrows");
        var keys = SeedTemplate(IepTypeId);
        var rows = string.Join(",", Enumerable.Range(0, 6).Select(_ => $"{{ \"{keys.Col1Key}\": \"X\" }}"));
        var values = $$"""
        { "{{keys.TextKey}}": "A", "{{keys.SelectKey}}": "Yes", "{{keys.TableKey}}": [ {{rows}} ] }
        """;
        var instanceId = SeedInstance(s, keys, IepTypeId, values);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("No more than 5"));
    }

    // ---------------------------------------------------------------- Finalize success + numbering

    [Fact]
    public async Task Finalize_Success_CreatesImmutableVersion_AndReturnsToDraft()
    {
        var s = SeedSchoolWithStudent("ok");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        AuthoredDocumentVersionSummaryModel summary;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            summary = result.Data!;
        }

        Assert.Equal(1, summary.VersionNumber);
        Assert.Equal(PdfRenderStatus.Pending, summary.PdfRenderStatus);

        using (var ctx = CreateContext())
        {
            var version = ctx.AuthoredDocumentVersions.Single();
            Assert.Equal(s.StudentId, version.SchoolStudentId);
            Assert.Equal(IepTypeId, version.DocumentTypeId);
            Assert.Equal(keys.VersionId, version.DocumentTemplateVersionId);
            Assert.Contains("Alice", version.ValuesJson); // frozen snapshot
            // A Pending PDF row was created.
            Assert.Equal(PdfRenderStatus.Pending, ctx.AuthoredDocumentPdfs.Single().RenderStatus);
            // Instance returned to Draft (re-finalizable).
            Assert.Equal(DocumentInstanceStatus.Draft, ctx.DocumentInstances.Single(i => i.Id == instanceId).Status);
        }
    }

    [Fact]
    public async Task Finalize_ReFinalize_IncrementsVersionNumber()
    {
        var s = SeedSchoolWithStudent("renum");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        using (var ctx = CreateContext())
            Assert.Equal(1, (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.VersionNumber);
        using (var ctx = CreateContext())
            Assert.Equal(2, (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.VersionNumber);
        using (var ctx = CreateContext())
            Assert.Equal(3, (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.VersionNumber);
    }

    [Fact]
    public async Task Finalize_IepAndEtr_NumberIndependentlyForSameStudent()
    {
        var s = SeedSchoolWithStudent("indep");
        var iepKeys = SeedTemplate(IepTypeId);
        var etrKeys = SeedTemplate(EtrTypeId);
        var iepInstance = SeedInstance(s, iepKeys, IepTypeId, ValidValues(iepKeys));
        var etrInstance = SeedInstance(s, etrKeys, EtrTypeId, ValidValues(etrKeys));

        // IEP v1, IEP v2, ETR v1 — the ETR numbering is independent of the IEP's.
        using (var ctx = CreateContext())
            Assert.Equal(1, (await CreateService(ctx).FinalizeAsync(iepInstance, s.CollaboratorUserId)).Data!.VersionNumber);
        using (var ctx = CreateContext())
            Assert.Equal(2, (await CreateService(ctx).FinalizeAsync(iepInstance, s.CollaboratorUserId)).Data!.VersionNumber);
        using (var ctx = CreateContext())
            Assert.Equal(1, (await CreateService(ctx).FinalizeAsync(etrInstance, s.CollaboratorUserId)).Data!.VersionNumber);
    }

    [Fact]
    public async Task Finalize_NonCollaborator_IsDenied()
    {
        var s = SeedSchoolWithStudent("authz");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));
        var stranger = SeedStranger("authz-stranger");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).FinalizeAsync(instanceId, stranger);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ----------------------------------------------------------------- multilingual plan (2026-10-06)
    // phase 3: FinalizeAsync's Permission/AlreadyFinalizing failures now carry ServiceErrorKind
    // explicitly, so Spanish wording never changes the controller's status routing — see
    // IepAssistant.Api.Extensions.ServiceFailureMapperExtensions.MapServiceFailure.

    [Fact]
    public async Task Finalize_NonCollaborator_UnderSpanishCulture_MessageIsSpanish_AndMapsTo403ViaErrorKind()
    {
        var s = SeedSchoolWithStudent("authzes");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));
        var stranger = SeedStranger("authzes-stranger");

        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateService(ctx).FinalizeAsync(instanceId, stranger);

        Assert.False(result.Success);
        Assert.Equal("No tiene permiso para acceder a este documento.", result.Message);
        Assert.Equal(ServiceErrorKind.Forbidden, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        var objectResult = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Finalize_AlreadyFinalizing_UnderSpanishCulture_MessageIsSpanish_AndMapsTo409ViaErrorKind()
    {
        var s = SeedSchoolWithStudent("racees");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        using (var ctx = CreateContext())
        {
            var instance = await ctx.DocumentInstances.SingleAsync(i => i.Id == instanceId);
            instance.Status = DocumentInstanceStatus.Finalizing;
            await ctx.SaveChangesAsync();
        }

        using var _lang = CultureScope.For("es");
        using var ctx2 = CreateContext();
        var result = await CreateService(ctx2).FinalizeAsync(instanceId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Equal("Este documento ya se está finalizando.", result.Message);
        Assert.Equal(ServiceErrorKind.Conflict, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<ConflictObjectResult>(action);
    }

    private sealed class TestController : ControllerBase
    {
    }

    [Fact]
    public async Task Finalize_RecordsAuditEntry()
    {
        var s = SeedSchoolWithStudent("audit");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        _audit.Entries.Clear();
        int versionId;
        using (var ctx = CreateContext())
            versionId = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.Id;

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Finalize, entry.Action);
        Assert.Equal("AuthoredDocumentVersion", entry.ResourceType);
        Assert.Equal(versionId, entry.ResourceId);
    }

    /// <summary>
    /// Review pass 2 fix (plan 2026-10-02-002, P3): previously only the Goals table was re-validated
    /// against active team membership at finalize (GoalRecordService.ProjectOnFinalizeAsync) — a Services/
    /// Accommodations/Transition row's stale `_ownerUserId` froze verbatim into the immutable version.
    /// AuthoredDocumentVersionService.FinalizeAsync now strips it from EVERY owner-eligible table's
    /// snapshot (OwnerEligibleRowSanitizer) before the version is created. Uses a Services-semantic table
    /// (not Goals) specifically to prove the fix covers tables GoalRecordService never touches.
    /// </summary>
    [Fact]
    public async Task Finalize_StripsInactiveOwner_FromNonGoalsOwnerEligibleTable_KeepsActiveOwner()
    {
        var s = SeedSchoolWithStudent("finalize-owner-strip");
        var keys = SeedServicesTemplate();

        int departedUserId;
        using (var ctx = CreateContext())
        {
            var departed = new User { Email = "departed-owner@example.com", PasswordHash = "x", FirstName = "Dee", LastName = "Parted", Role = UserRole.Educator };
            ctx.Users.Add(departed);
            ctx.SaveChanges();
            departedUserId = departed.Id;

            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = s.StudentId, UserId = s.CollaboratorUserId, TeamRole = TeamRole.CaseManager, IsActive = true });
            // Inactive — e.g. the provider left the team after this row was originally saved.
            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = s.StudentId, UserId = departedUserId, TeamRole = TeamRole.SpeechLanguagePathologist, IsActive = false });
            ctx.SaveChanges();
        }

        var activeRowId = Guid.NewGuid();
        var inactiveRowId = Guid.NewGuid();
        var valuesJson = $$"""
        {
          "{{keys.ServicesFieldKey}}": [
            { "_rowId": "{{activeRowId}}", "{{keys.ServiceTypeCol}}": "Speech therapy", "_ownerUserId": {{s.CollaboratorUserId}} },
            { "_rowId": "{{inactiveRowId}}", "{{keys.ServiceTypeCol}}": "Occupational therapy", "_ownerUserId": {{departedUserId}} }
          ]
        }
        """;

        int instanceId;
        using (var ctx = CreateContext())
        {
            var instance = new DocumentInstance
            {
                SchoolStudentId = s.StudentId,
                DocumentTypeId = IepTypeId,
                DocumentTemplateVersionId = keys.VersionId,
                Status = DocumentInstanceStatus.Draft,
                ValuesJson = valuesJson,
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            ctx.DocumentInstances.Add(instance);
            ctx.SaveChanges();
            instanceId = instance.Id;
        }

        ServiceResult<AuthoredDocumentVersionSummaryModel> result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId);
        Assert.True(result.Success, result.Message);

        using (var ctx = CreateContext())
        {
            var version = ctx.AuthoredDocumentVersions.Single(v => v.Id == result.Data!.Id);
            var rows = JsonNode.Parse(version.ValuesJson)!.AsObject()[keys.ServicesFieldKey.ToString()]!.AsArray();
            var activeRow = rows.OfType<JsonObject>().Single(r => r["_rowId"]!.ToString() == activeRowId.ToString());
            var inactiveRow = rows.OfType<JsonObject>().Single(r => r["_rowId"]!.ToString() == inactiveRowId.ToString());

            Assert.Equal(s.CollaboratorUserId, activeRow[RowMetaKeys.OwnerUserId]!.GetValue<int>());
            Assert.Null(inactiveRow[RowMetaKeys.OwnerUserId]);
        }
    }

    // ---------------------------------------------------------------- Numbering backstop + immutability

    [Fact]
    public async Task VersionNumber_DuplicatePerStudentDocType_IsRejectedByUniqueIndex()
    {
        var s = SeedSchoolWithStudent("uniq");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        int versionNumber;
        using (var ctx = CreateContext())
            versionNumber = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.VersionNumber;

        using var ctx2 = CreateContext();
        ctx2.AuthoredDocumentVersions.Add(new AuthoredDocumentVersion
        {
            SchoolStudentId = s.StudentId,
            DocumentTypeId = IepTypeId,
            DocumentTemplateVersionId = keys.VersionId,
            VersionNumber = versionNumber, // duplicate (student, docType, versionNumber)
            ValuesJson = "{}",
            FinalizedByUserId = s.CollaboratorUserId,
            FinalizedAt = DateTime.UtcNow
        });
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => ctx2.SaveChangesAsync());
    }

    [Fact]
    public async Task Version_Update_ThrowsImmutable()
    {
        var s = SeedSchoolWithStudent("immut");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        int versionId;
        using (var ctx = CreateContext())
            versionId = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.Id;

        using var ctx2 = CreateContext();
        var version = ctx2.AuthoredDocumentVersions.Single(v => v.Id == versionId);
        version.ValuesJson = "{\"tampered\":true}";
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx2.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VersionPdf_Update_IsAllowed()
    {
        var s = SeedSchoolWithStudent("pdfmut");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        int versionId;
        using (var ctx = CreateContext())
            versionId = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.Id;

        using var ctx2 = CreateContext();
        var pdf = ctx2.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId);
        pdf.RenderStatus = PdfRenderStatus.Rendered;
        pdf.BlobUri = "https://blob/x.pdf";
        await ctx2.SaveChangesAsync(); // no throw — the Pdf row is deliberately mutable
        Assert.Equal(PdfRenderStatus.Rendered, ctx2.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId).RenderStatus);
    }

    // ---------------------------------------------------------------- PDF render (worker path)

    [Fact]
    public async Task RenderAsync_Success_MarksRenderedWithChecksumAndDeterministicPath()
    {
        var s = SeedSchoolWithStudent("render");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        AuthoredDocumentVersionSummaryModel v;
        using (var ctx = CreateContext())
            v = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!;

        var blob = new SuccessBlobStorageFake();
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, blob).RenderAsync(v.Id);

        Assert.NotNull(blob.LastBytes);
        Assert.NotEmpty(blob.LastBytes!);
        Assert.Equal($"authored-docs/{v.Id}/doc-v{v.VersionNumber}.pdf", blob.LastBlobPath);

        using (var ctx = CreateContext())
        {
            var pdf = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == v.Id);
            Assert.Equal(PdfRenderStatus.Rendered, pdf.RenderStatus);
            Assert.False(string.IsNullOrWhiteSpace(pdf.Checksum));
            Assert.NotNull(pdf.RenderedAt);
            Assert.Null(pdf.ErrorMessage);
        }
    }

    [Fact]
    public async Task RenderAsync_Twice_YieldsIdenticalChecksum()
    {
        var s = SeedSchoolWithStudent("determ");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        int versionId;
        using (var ctx = CreateContext())
            versionId = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.Id;

        string checksum1, checksum2;
        using (var ctx = CreateContext())
        {
            await CreatePdfService(ctx, new SuccessBlobStorageFake()).RenderAsync(versionId);
            checksum1 = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId).Checksum!;
        }
        using (var ctx = CreateContext())
        {
            await CreatePdfService(ctx, new SuccessBlobStorageFake()).RenderAsync(versionId);
            checksum2 = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId).Checksum!;
        }

        Assert.Equal(checksum1, checksum2);
    }

    [Fact]
    public async Task RenderAsync_Failure_MarksErrorWithoutCrashing_AndLeavesVersionValid()
    {
        var s = SeedSchoolWithStudent("renderfail");
        var keys = SeedTemplate(IepTypeId);
        var instanceId = SeedInstance(s, keys, IepTypeId, ValidValues(keys));

        int versionId;
        using (var ctx = CreateContext())
            versionId = (await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId)).Data!.Id;

        // Does not throw (swallowed into a retryable Error state).
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new FailingBlobStorageFake()).RenderAsync(versionId);

        using (var ctx = CreateContext())
        {
            var pdf = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId);
            Assert.Equal(PdfRenderStatus.Error, pdf.RenderStatus);
            Assert.False(string.IsNullOrWhiteSpace(pdf.ErrorMessage));
            Assert.Null(pdf.RenderedAt);
            // The frozen version is untouched.
            Assert.NotNull(ctx.AuthoredDocumentVersions.Single(x => x.Id == versionId));
        }
    }

    [Fact]
    public async Task RenderAsync_LargeTable_ProducesMultiPageDocument()
    {
        var s = SeedSchoolWithStudent("bigtable");
        var keys = SeedTemplate(IepTypeId);
        // 200 rows forces the table across multiple pages; header-repeat is structural (.Header()).
        var rows = string.Join(",", Enumerable.Range(0, 200).Select(i => $"{{ \"{keys.Col1Key}\": \"Service line {i}\", \"{keys.Col2Key}\": \"2026-01-01\" }}"));
        var values = $$"""
        { "{{keys.TextKey}}": "Alice", "{{keys.SelectKey}}": "Yes", "{{keys.TableKey}}": [ {{rows}} ] }
        """;
        // maxRows on the seeded table is 5; use a permissive instance by pinning a fresh template with no bounds.
        var instanceId = SeedInstance(s, keys, IepTypeId, values);

        int versionId;
        using (var ctx = CreateContext())
        {
            // Skip finalize validation (maxRows would reject 200): insert the version directly, then render.
            var version = new AuthoredDocumentVersion
            {
                SchoolStudentId = s.StudentId,
                DocumentTypeId = IepTypeId,
                DocumentTemplateVersionId = keys.VersionId,
                VersionNumber = 1,
                ValuesJson = values,
                FinalizedByUserId = s.CollaboratorUserId,
                FinalizedAt = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
                Pdfs = new List<AuthoredDocumentPdf> { new() { RenderStatus = PdfRenderStatus.Pending } }
            };
            ctx.AuthoredDocumentVersions.Add(version);
            ctx.SaveChanges();
            versionId = version.Id;
        }

        var blob = new SuccessBlobStorageFake();
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, blob).RenderAsync(versionId);

        Assert.NotNull(blob.LastBytes);
        Assert.NotEmpty(blob.LastBytes!);
        using (var ctx = CreateContext())
            Assert.Equal(PdfRenderStatus.Rendered, ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId).RenderStatus);
    }

    // ---------------------------------------------------------------- Composer: empty-field omission

    [Fact]
    public void Composer_OmitsEmptyOptionalFieldsAndSections()
    {
        // Build a tree: Section A has an optional Text (empty) only -> omitted; Section B has a Text with
        // a value -> rendered. The composer must not throw and must produce bytes.
        var emptyKey = Guid.NewGuid();
        var filledKey = Guid.NewGuid();
        var tree = new TemplateVersionDetailModel
        {
            Id = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Empty Section", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = emptyKey, FieldType = FieldType.Text, Label = "Nothing", Required = false, DisplayOrder = 0 }
                    }
                },
                new()
                {
                    Id = 2, Title = "Filled Section", DisplayOrder = 1,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 2, FieldKey = filledKey, FieldType = FieldType.Text, Label = "Something", Required = false, DisplayOrder = 0 }
                    }
                }
            }
        };
        var values = $$"""{ "{{filledKey}}": "Present" }""";

        var doc = new AuthoredDocumentPdfDocument("IEP", 1, new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc), tree, values);
        var bytes = doc.GeneratePdf();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void Composer_ValidateAgainstSchema_ReportsSectionAndFieldForEmptyDoc()
    {
        var textKey = Guid.NewGuid();
        var tree = new TemplateVersionDetailModel
        {
            Id = 1,
            Sections = new List<TemplateSectionModel>
            {
                new()
                {
                    Id = 1, Title = "Demographics", DisplayOrder = 0,
                    Fields = new List<TemplateFieldModel>
                    {
                        new() { Id = 1, FieldKey = textKey, FieldType = FieldType.Text, Label = "Legal Name", Required = true, DisplayOrder = 0 }
                    }
                }
            }
        };

        var errors = AuthoredDocumentVersionService.ValidateAgainstSchema(tree, "{}");
        var error = Assert.Single(errors);
        Assert.Contains("Demographics", error);
        Assert.Contains("Legal Name", error);
    }

    // ---------------------------------------------------------------- PDF status (side-effect-free poll)

    [Fact]
    public async Task GetPdfStatus_ReturnsStatus_WithoutAudit()
    {
        var s = SeedSchoolWithStudent("status");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        _audit.Entries.Clear();
        ServiceResult<AuthoredDocumentPdfStatusModel> result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).GetPdfStatusAsync(versionId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(versionId, result.Data!.VersionId);
        Assert.Equal(PdfRenderStatus.Rendered, result.Data.RenderStatus);
        Assert.NotNull(result.Data.RenderedAt);
        // A status poll is not an export: no SAS is minted and NO audit is written (both live in the
        // download call). AuthoredDocumentPdfStatusModel carries no Url — enforced at compile time.
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task GetPdfStatus_NoPdfRow_DefaultsToPending()
    {
        var s = SeedSchoolWithStudent("status-none");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, pdfStatus: null);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetPdfStatusAsync(versionId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(PdfRenderStatus.Pending, result.Data!.RenderStatus);
        Assert.Null(result.Data.RenderedAt);
    }

    // ---------------------------------------------------------------- PDF download URL (audited export)

    [Fact]
    public async Task GetPdfDownloadUrl_Rendered_ReturnsUrl_AndWritesExactlyOneExportAudit()
    {
        var s = SeedSchoolWithStudent("dl-ok");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        _audit.Entries.Clear();
        ServiceResult<string> result;
        using (var ctx = CreateContext())
            result = await CreateService(ctx).GetPdfDownloadUrlAsync(versionId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.Data));
        // The SAS is minted from the deterministic blob path for this version.
        Assert.Contains($"authored-docs/{versionId}/doc-v1.pdf", result.Data!);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Export, entry.Action);
        Assert.Equal("AuthoredDocumentVersion", entry.ResourceType);
        Assert.Equal(versionId, entry.ResourceId);
        Assert.Equal(s.CollaboratorUserId, entry.ActorUserId);
    }

    [Theory]
    [InlineData(PdfRenderStatus.Pending)]
    [InlineData(PdfRenderStatus.Error)]
    public async Task GetPdfDownloadUrl_NotYetRendered_Fails_WithNoUrlOrAudit(PdfRenderStatus status)
    {
        var s = SeedSchoolWithStudent($"dl-{status}");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, status);

        _audit.Entries.Clear();
        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetPdfDownloadUrlAsync(versionId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("not available", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task GetPdfDownloadUrl_NonReader_IsDenied_WithNoAudit()
    {
        var s = SeedSchoolWithStudent("dl-authz");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);
        var stranger = SeedStranger("dl-stranger");

        _audit.Entries.Clear();
        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetPdfDownloadUrlAsync(versionId, stranger);

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_audit.Entries);
    }

    // ---------------------------------------------------------------- PDF retry

    [Fact]
    public async Task RequestPdfRetry_NoPdfRow_IsRejected()
    {
        var s = SeedSchoolWithStudent("retry-none");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, pdfStatus: null);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).RequestPdfRetryAsync(versionId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("no PDF record", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RequestPdfRetry_AlreadyRendered_IsRejected_AndLeavesStatusUnchanged()
    {
        var s = SeedSchoolWithStudent("retry-rendered");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).RequestPdfRetryAsync(versionId, s.CollaboratorUserId);
            Assert.False(result.Success);
            Assert.Contains("already rendered", result.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Equal(PdfRenderStatus.Rendered, ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId).RenderStatus);
    }

    [Fact]
    public async Task RequestPdfRetry_Error_ResetsToPending_AndClearsError()
    {
        var s = SeedSchoolWithStudent("retry-error");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Error);

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).RequestPdfRetryAsync(versionId, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            Assert.Equal(versionId, result.Data);
        }

        using (var ctx = CreateContext())
        {
            var pdf = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId);
            Assert.Equal(PdfRenderStatus.Pending, pdf.RenderStatus);
            Assert.Null(pdf.ErrorMessage);
        }
    }

    [Fact]
    public async Task RequestPdfRetry_NonCollaborator_IsDenied_AndLeavesStatusUnchanged()
    {
        var s = SeedSchoolWithStudent("retry-authz");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Error);
        var stranger = SeedStranger("retry-stranger");

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).RequestPdfRetryAsync(versionId, stranger);
            Assert.False(result.Success);
            Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
            Assert.Equal(PdfRenderStatus.Error, ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId).RenderStatus);
    }

    // ---------------------------------------------------------------- Multilingual plan phase 7: per-language PDFs

    [Fact]
    public async Task RenderAsync_EnglishThenSpanish_CoexistAsSeparateRows_WithDistinctBlobPathsAndBytes()
    {
        var s = SeedSchoolWithStudent("pdf-i18n");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Pending);

        var englishBlob = new SuccessBlobStorageFake();
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, englishBlob).RenderAsync(versionId); // language omitted -> English, unchanged

        // A Spanish render needs its own tracking row first — GetPdfStatusAsync (covered separately below)
        // is what creates it in production; here we create it directly to isolate RenderAsync's own behavior.
        using (var ctx = CreateContext())
        {
            ctx.AuthoredDocumentPdfs.Add(new AuthoredDocumentPdf { AuthoredDocumentVersionId = versionId, Language = "es", RenderStatus = PdfRenderStatus.Pending });
            ctx.SaveChanges();
        }

        var spanishBlob = new SuccessBlobStorageFake();
        using (var ctx = CreateContext())
            await CreatePdfService(ctx, spanishBlob).RenderAsync(versionId, "es");

        // Distinct blob paths — the English key is the pre-phase-7 literal, unchanged.
        Assert.Equal($"authored-docs/{versionId}/doc-v1.pdf", englishBlob.LastBlobPath);
        Assert.Equal($"authored-docs/{versionId}/doc-v1.es.pdf", spanishBlob.LastBlobPath);
        Assert.NotEqual(englishBlob.LastBlobPath, spanishBlob.LastBlobPath);

        // Distinct rendered bytes — the Spanish PDF's labels differ from the English ones, so the two
        // renders can never be byte-identical.
        Assert.NotEqual(englishBlob.LastBytes, spanishBlob.LastBytes);

        using (var ctx = CreateContext())
        {
            var rows = ctx.AuthoredDocumentPdfs.Where(p => p.AuthoredDocumentVersionId == versionId).ToList();
            Assert.Equal(2, rows.Count);

            // SeedFinalizedVersion's row predates this phase's explicit Language tagging (mirrors a real
            // pre-migration row) — null means English, same as everywhere else this column is read.
            var english = Assert.Single(rows, p => p.Language == null);
            Assert.Equal(PdfRenderStatus.Rendered, english.RenderStatus);

            var spanish = Assert.Single(rows, p => p.Language == "es");
            Assert.Equal(PdfRenderStatus.Rendered, spanish.RenderStatus);
            Assert.NotEqual(english.Checksum, spanish.Checksum);
        }
    }

    /// <summary>Seeds a Held meeting (plus one participant) for <paramref name="studentId"/>, returning the
    /// meeting id. Used by the header-freeze tests below to give <c>BuildHeaderContextAsync</c>'s "latest
    /// Held meeting" query something to pick up — and something that can change between renders.</summary>
    private int SeedHeldMeeting(int studentId, int createdByUserId, DateTime startsAtUtc, string participantName, TeamRole participantRole)
    {
        using var ctx = CreateContext();
        var meeting = new Meeting
        {
            SchoolStudentId = studentId,
            Type = MeetingType.AnnualReview,
            Title = "IEP Meeting",
            StartsAtUtc = startsAtUtc,
            Status = MeetingStatus.Held,
            CreatedByUserId = createdByUserId
        };
        ctx.Meetings.Add(meeting);
        ctx.SaveChanges();

        ctx.MeetingParticipants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            ExternalName = participantName,
            TeamRole = participantRole,
            RsvpToken = Guid.NewGuid().ToString("N")
        });
        ctx.SaveChanges();

        return meeting.Id;
    }

    [Fact]
    public async Task RenderAsync_SpanishAfterEnglish_ReusesFrozenHeaderSnapshot_EvenWhenLatestHeldMeetingChangesBetween()
    {
        var s = SeedSchoolWithStudent("pdf-header-freeze");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Pending);

        // The meeting/participant in place when English renders — this is what must stay frozen.
        SeedHeldMeeting(s.StudentId, s.CollaboratorUserId, new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), "Original Case Manager", TeamRole.CaseManager);

        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new SuccessBlobStorageFake()).RenderAsync(versionId);

        string frozenJsonAfterEnglish;
        using (var ctx = CreateContext())
        {
            var english = Assert.Single(ctx.AuthoredDocumentPdfs.Where(p => p.AuthoredDocumentVersionId == versionId && p.Language == null));
            Assert.False(string.IsNullOrEmpty(english.HeaderSnapshotJson));
            frozenJsonAfterEnglish = english.HeaderSnapshotJson!;

            var frozen = JsonSerializer.Deserialize<AuthoredDocumentPdfHeaderContext>(frozenJsonAfterEnglish)!;
            Assert.Equal(new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc), frozen.MeetingDate);
            Assert.Equal("Original Case Manager", Assert.Single(frozen.Participants).Name);
        }

        // Now the team changes: a LATER Held meeting with a different participant — if anything rebuilt
        // the header live for the Spanish render, this is what it would (wrongly) pick up instead.
        SeedHeldMeeting(s.StudentId, s.CollaboratorUserId, new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc), "Replacement Case Manager", TeamRole.CaseManager);

        using (var ctx = CreateContext())
        {
            ctx.AuthoredDocumentPdfs.Add(new AuthoredDocumentPdf { AuthoredDocumentVersionId = versionId, Language = "es", RenderStatus = PdfRenderStatus.Pending });
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new SuccessBlobStorageFake()).RenderAsync(versionId, "es");

        using (var ctx = CreateContext())
        {
            var rows = ctx.AuthoredDocumentPdfs.Where(p => p.AuthoredDocumentVersionId == versionId).ToList();

            var english = Assert.Single(rows, p => p.Language == null);
            Assert.Equal(PdfRenderStatus.Rendered, english.RenderStatus);
            // First resolution wins: the English row's snapshot is untouched by the later Spanish render,
            // byte-for-byte, and still reflects the ORIGINAL meeting/participant — not the replacement.
            Assert.Equal(frozenJsonAfterEnglish, english.HeaderSnapshotJson);

            var spanish = Assert.Single(rows, p => p.Language == "es");
            Assert.Equal(PdfRenderStatus.Rendered, spanish.RenderStatus);
            // The snapshot lives only on the English row — the Spanish row never gets its own copy.
            Assert.Null(spanish.HeaderSnapshotJson);
        }
    }

    [Fact]
    public async Task RenderAsync_RetryAfterFailure_ReusesTheSameFrozenHeaderSnapshot()
    {
        var s = SeedSchoolWithStudent("pdf-header-retry");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Pending);

        SeedHeldMeeting(s.StudentId, s.CollaboratorUserId, new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc), "Case Manager A", TeamRole.CaseManager);

        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new SuccessBlobStorageFake()).RenderAsync(versionId);

        string frozenJson;
        using (var ctx = CreateContext())
        {
            var english = Assert.Single(ctx.AuthoredDocumentPdfs.Where(p => p.AuthoredDocumentVersionId == versionId));
            frozenJson = english.HeaderSnapshotJson!;
            Assert.False(string.IsNullOrEmpty(frozenJson));
        }

        // Simulate a later failed render (e.g. a transient blob outage) needing a retry, after the team
        // changed again — the retry must still reuse the ORIGINAL frozen header, not rebuild live.
        SeedHeldMeeting(s.StudentId, s.CollaboratorUserId, new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc), "Case Manager B", TeamRole.CaseManager);
        using (var ctx = CreateContext())
        {
            var english = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId);
            english.RenderStatus = PdfRenderStatus.Error;
            english.ErrorMessage = "simulated transient failure";
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
            await CreatePdfService(ctx, new SuccessBlobStorageFake()).RenderAsync(versionId); // retry, English

        using (var ctx = CreateContext())
        {
            var english = ctx.AuthoredDocumentPdfs.Single(p => p.AuthoredDocumentVersionId == versionId);
            Assert.Equal(PdfRenderStatus.Rendered, english.RenderStatus);
            Assert.Equal(frozenJson, english.HeaderSnapshotJson);

            var header = JsonSerializer.Deserialize<AuthoredDocumentPdfHeaderContext>(english.HeaderSnapshotJson!)!;
            Assert.Equal(new DateTime(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc), header.MeetingDate);
            Assert.Equal("Case Manager A", Assert.Single(header.Participants).Name);
        }
    }

    [Fact]
    public async Task GetPdfStatus_FirstSpanishPoll_CreatesPendingRow_FlagsNeedsRender_LeavesEnglishRowUntouched()
    {
        var s = SeedSchoolWithStudent("pdf-status-es");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using (var ctx = CreateContext())
        using (CultureScope.For("es"))
        {
            var result = await CreateService(ctx).GetPdfStatusAsync(versionId, s.CollaboratorUserId);

            Assert.True(result.Success, result.Message);
            Assert.True(result.Data!.NeedsRender);
            Assert.Equal("es", result.Data.Language);
            Assert.Equal(PdfRenderStatus.Pending, result.Data.RenderStatus);
        }

        using (var ctx = CreateContext())
        {
            var rows = ctx.AuthoredDocumentPdfs.Where(p => p.AuthoredDocumentVersionId == versionId).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(PdfRenderStatus.Rendered, Assert.Single(rows, p => p.Language == null).RenderStatus);
            Assert.Equal(PdfRenderStatus.Pending, Assert.Single(rows, p => p.Language == "es").RenderStatus);
        }

        // A second poll, still Pending, must NOT flag NeedsRender again (it would duplicate the queued render).
        using (var ctx = CreateContext())
        using (CultureScope.For("es"))
        {
            var again = await CreateService(ctx).GetPdfStatusAsync(versionId, s.CollaboratorUserId);
            Assert.True(again.Success, again.Message);
            Assert.False(again.Data!.NeedsRender);
        }
    }

    /// <summary>
    /// Review fix (2026-10-07): GetPdfStatusAsync's Add+SaveChanges for a brand-new (version, language)
    /// row is now wrapped in try/catch DbUpdateException, detaching the loser and re-querying AsNoTracking
    /// so two concurrent first polls join the SAME render instead of one failing outright. Genuinely
    /// interleaving two overlapping GetPdfStatusAsync calls isn't reachable from a single-threaded test —
    /// the method's OWN existence check would simply see the other call's already-committed row and never
    /// reach the Add+Save path at all. So this instead proves the mechanism the catch block relies on
    /// directly: a concurrent insert for the exact same (version, language) genuinely collides on the
    /// unique index (<see cref="AuthoredDocumentPdfConfiguration"/>), and detaching the loser + re-querying
    /// AsNoTracking (exactly what the catch block does) recovers the winner's row.
    /// </summary>
    [Fact]
    public async Task GetPdfStatus_ConcurrentFirstPollInsertRace_ReQueryPathRecoversTheWinnerRow()
    {
        var s = SeedSchoolWithStudent("pdf-status-race");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using var winnerCtx = CreateContext();
        var winnerRow = new AuthoredDocumentPdf { AuthoredDocumentVersionId = versionId, Language = "es", RenderStatus = PdfRenderStatus.Pending };
        winnerCtx.AuthoredDocumentPdfs.Add(winnerRow);
        winnerCtx.SaveChanges();

        using var loserCtx = CreateContext();
        var loserRow = new AuthoredDocumentPdf { AuthoredDocumentVersionId = versionId, Language = "es", RenderStatus = PdfRenderStatus.Pending };
        loserCtx.AuthoredDocumentPdfs.Add(loserRow);

        await Assert.ThrowsAsync<DbUpdateException>(() => loserCtx.SaveChangesAsync());

        loserCtx.Entry(loserRow).State = EntityState.Detached;
        var recovered = await loserCtx.AuthoredDocumentPdfs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.AuthoredDocumentVersionId == versionId && p.Language == "es");

        Assert.NotNull(recovered);
        Assert.Equal(winnerRow.Id, recovered!.Id);
        Assert.NotEqual(loserRow.Id, recovered.Id);
    }

    [Fact]
    public async Task GetPdfDownloadUrl_SpanishRequested_ButOnlyEnglishRendered_Fails()
    {
        var s = SeedSchoolWithStudent("dl-es-not-ready");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using var ctx = CreateContext();
        using var _lang = CultureScope.For("es");
        var result = await CreateService(ctx).GetPdfDownloadUrlAsync(versionId, s.CollaboratorUserId);

        // The English row is Rendered, but a Spanish-requesting caller must get the SPANISH row's status —
        // which doesn't exist yet — never silently served the English PDF. ErrorKind (not message text,
        // which is correctly Spanish here) is what a caller should ever branch on.
        Assert.False(result.Success);
        Assert.Equal(ServiceErrorKind.Validation, result.ErrorKind);
    }

    // ---------------------------------------------------------------- Parent / child read authorization

    [Fact]
    public async Task ParentLinked_CanListForChild_AndGetVersion()
    {
        var s = SeedSchoolWithStudent("parent-ok");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);
        var parent = SeedLinkedParent("parent-ok", s.StudentId);

        using (var ctx = CreateContext())
        {
            var list = await CreateService(ctx).ListForChildAsync(parent.ChildProfileId, parent.ParentUserId);
            Assert.True(list.Success, list.Message);
            var row = Assert.Single(list.Data!);
            Assert.Equal(versionId, row.Id);
        }

        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetVersionAsync(versionId, parent.ParentUserId);
            Assert.True(get.Success, get.Message);
            Assert.Equal(versionId, get.Data!.Id);
        }
    }

    [Theory]
    [InlineData(false, true)]  // inactive link
    [InlineData(true, false)]  // unaccepted link
    public async Task ParentWithInactiveOrUnacceptedLink_IsDenied(bool linkActive, bool linkAccepted)
    {
        var s = SeedSchoolWithStudent($"parent-badlink-{linkActive}-{linkAccepted}");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);
        var parent = SeedLinkedParent($"parent-badlink-{linkActive}-{linkAccepted}", s.StudentId, linkActive, linkAccepted);

        using (var ctx = CreateContext())
        {
            var list = await CreateService(ctx).ListForChildAsync(parent.ChildProfileId, parent.ParentUserId);
            Assert.True(list.Success);
            Assert.Empty(list.Data!); // the student is not in the parent's active+accepted linked set
        }

        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetVersionAsync(versionId, parent.ParentUserId);
            Assert.False(get.Success);
            Assert.Contains("permission", get.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ParentOfDifferentStudent_IsDenied()
    {
        var s = SeedSchoolWithStudent("parent-other-a");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        // Parent is validly linked, but to a DIFFERENT student with no versions of their own.
        var other = SeedSchoolWithStudent("parent-other-b");
        var parent = SeedLinkedParent("parent-other", other.StudentId);

        using (var ctx = CreateContext())
        {
            var get = await CreateService(ctx).GetVersionAsync(versionId, parent.ParentUserId);
            Assert.False(get.Success);
            Assert.Contains("permission", get.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            var list = await CreateService(ctx).ListForChildAsync(parent.ChildProfileId, parent.ParentUserId);
            Assert.True(list.Success);
            Assert.Empty(list.Data!); // student A's version never appears for a parent of student B
        }
    }

    private sealed record ServicesTemplateKeys(int VersionId, Guid ServicesFieldKey, Guid ServiceTypeCol);

    /// <summary>A Services-semantic table (one Text column, ServiceType-tagged) so a test can exercise the
    /// `_ownerUserId` family-facing redaction on <see cref="AuthoredDocumentVersionService.GetVersionAsync"/>
    /// (review fix, plan 2026-10-02-002, P2-2).</summary>
    private ServicesTemplateKeys SeedServicesTemplate(int docTypeId = IepTypeId)
    {
        var servicesFieldKey = Guid.NewGuid();
        var serviceTypeCol = Guid.NewGuid();

        using var ctx = CreateContext();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = docTypeId, Name = "Services template", Versions = { version } });
        ctx.SaveChanges();

        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id, SectionKey = Guid.NewGuid(), Title = "Services", DisplayOrder = 0,
            Fields =
            {
                new TemplateField
                {
                    DocumentTemplateVersionId = version.Id, FieldKey = servicesFieldKey, FieldType = FieldType.Table, Label = "Services", DisplayOrder = 0,
                    ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Services, (serviceTypeCol, FieldType.Text, "Service", ColumnSemantics.ServiceType))
                }
            }
        });
        ctx.SaveChanges();

        return new ServicesTemplateKeys(version.Id, servicesFieldKey, serviceTypeCol);
    }

    /// <summary>Inserts a finalized version with an explicit ValuesJson (bypassing finalize validation),
    /// mirroring <see cref="SeedFinalizedVersion"/> but for a <see cref="ServicesTemplateKeys"/> template
    /// whose ValuesJson a test builds directly.</summary>
    private int SeedFinalizedServicesVersion(SchoolScenario s, ServicesTemplateKeys keys, int docTypeId, string valuesJson, int versionNumber = 1)
    {
        using var ctx = CreateContext();
        var version = new AuthoredDocumentVersion
        {
            SchoolStudentId = s.StudentId,
            DocumentTypeId = docTypeId,
            DocumentTemplateVersionId = keys.VersionId,
            VersionNumber = versionNumber,
            ValuesJson = valuesJson,
            FinalizedByUserId = s.CollaboratorUserId,
            FinalizedAt = DateTime.UtcNow
        };
        ctx.AuthoredDocumentVersions.Add(version);
        ctx.SaveChanges();
        return version.Id;
    }

    [Fact]
    public async Task ParentLinked_GetVersion_RedactsOwnerToRoleOnly_StaffSeesRawOwner()
    {
        var s = SeedSchoolWithStudent("parent-owner-redact");
        var keys = SeedServicesTemplate();
        using (var ctx = CreateContext())
        {
            ctx.StudentTeamMembers.Add(new StudentTeamMember
            {
                SchoolStudentId = s.StudentId, UserId = s.CollaboratorUserId, TeamRole = TeamRole.InterventionSpecialist, IsActive = true
            });
            ctx.SaveChanges();
        }
        var valuesJson = $$"""
        { "{{keys.ServicesFieldKey}}": [ { "_rowId": "{{Guid.NewGuid()}}", "{{keys.ServiceTypeCol}}": "Speech therapy", "_ownerUserId": {{s.CollaboratorUserId}} } ] }
        """;
        var versionId = SeedFinalizedServicesVersion(s, keys, IepTypeId, valuesJson);
        var parent = SeedLinkedParent("parent-owner-redact", s.StudentId);

        // Staff still sees the raw owner id — nothing to redact for them.
        using (var ctx = CreateContext())
        {
            var staffGet = await CreateService(ctx).GetVersionAsync(versionId, s.CollaboratorUserId);
            Assert.True(staffGet.Success, staffGet.Message);
            Assert.Contains(RowMetaKeys.OwnerUserId, staffGet.Data!.ValuesJson);
            Assert.DoesNotContain(RowMetaKeys.OwnerRole, staffGet.Data.ValuesJson);
        }

        // The parent's read is redacted to role-only: never the raw id, never the person's name.
        using (var ctx = CreateContext())
        {
            var parentGet = await CreateService(ctx).GetVersionAsync(versionId, parent.ParentUserId);
            Assert.True(parentGet.Success, parentGet.Message);
            Assert.DoesNotContain(RowMetaKeys.OwnerUserId, parentGet.Data!.ValuesJson);
            Assert.Contains($"\"{RowMetaKeys.OwnerRole}\":\"Intervention Specialist\"", parentGet.Data.ValuesJson);
        }
    }

    // ---------------------------------------------------------------- PDF worker startup reconcile

    [Fact]
    public async Task ReconcilePendingRenders_EnqueuesExactlyThePendingVersionIds()
    {
        var s = SeedSchoolWithStudent("reconcile");
        var keys = SeedTemplate(IepTypeId);
        var pendingA = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Pending, versionNumber: 1);
        var pendingB = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Pending, versionNumber: 2);
        var rendered = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered, versionNumber: 3);

        var queue = new AuthoredDocumentPdfQueue();
        var worker = new AuthoredDocumentPdfWorker(queue, CreateScopeFactory(), NullLogger<AuthoredDocumentPdfWorker>.Instance);

        await InvokeReconcileAsync(worker);

        var drained = await DrainAsync(queue);
        Assert.Equal(new[] { pendingA, pendingB }.OrderBy(x => x).ToList(), drained.OrderBy(x => x).ToList());
        Assert.DoesNotContain(rendered, drained);
    }

    [Fact]
    public async Task ReconcilePendingRenders_NoPending_IsNoOp()
    {
        var s = SeedSchoolWithStudent("reconcile-empty");
        var keys = SeedTemplate(IepTypeId);
        SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered, versionNumber: 1);

        var queue = new AuthoredDocumentPdfQueue();
        var worker = new AuthoredDocumentPdfWorker(queue, CreateScopeFactory(), NullLogger<AuthoredDocumentPdfWorker>.Instance);

        await InvokeReconcileAsync(worker);

        var drained = await DrainAsync(queue);
        Assert.Empty(drained);
    }

    /// <summary>Real DI scope factory over the shared SQLite connection (the worker resolves ApplicationDbContext per scope).</summary>
    private IServiceScopeFactory CreateScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection));
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    /// <summary>Invokes the worker's private startup sweep directly (no BackgroundService hosting/consumer loop).</summary>
    private static async Task InvokeReconcileAsync(AuthoredDocumentPdfWorker worker)
    {
        var method = typeof(AuthoredDocumentPdfWorker)
            .GetMethod("ReconcilePendingRendersAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;
    }

    /// <summary>
    /// Deterministically drains everything the reconcile enqueued: a sentinel is appended after the
    /// (already-complete) reconcile, and the reader stops at it — no timing, no consumer loop.
    /// </summary>
    private static async Task<List<int>> DrainAsync(AuthoredDocumentPdfQueue queue)
    {
        const int sentinel = -1;
        await queue.EnqueueAsync(sentinel);

        var drained = new List<int>();
        await foreach (var (versionId, _) in queue.DequeueAllAsync(CancellationToken.None))
        {
            if (versionId == sentinel) break;
            drained.Add(versionId);
        }
        return drained;
    }

    // ---------------------------------------------------------------- Plan 7: amendments

    private SignedArtifactService CreateSignedArtifactService(ApplicationDbContext ctx, IBlobStorageService blob)
        => new(ctx, new OrgAccessService(ctx), new AccessService(ctx), blob, _audit, TestSupport.TestLocalizers.Messages());

    [Fact]
    public async Task Amend_Then_Finalize_CreatesAmendmentChain_PreservingRowIds()
    {
        var s = SeedSchoolWithStudent("amend");
        var keys = SeedTemplate(IepTypeId);
        var rowId1 = Guid.NewGuid();
        var rowId2 = Guid.NewGuid();
        var valuesWithRowIds = $$"""
        {
          "{{keys.TextKey}}": "Alice",
          "{{keys.SelectKey}}": "Yes",
          "{{keys.TableKey}}": [
            { "_rowId": "{{rowId1}}", "{{keys.Col1Key}}": "Speech", "{{keys.Col2Key}}": "2026-02-01" },
            { "_rowId": "{{rowId2}}", "{{keys.Col1Key}}": "OT" }
          ]
        }
        """;
        var instanceId = SeedInstance(s, keys, IepTypeId, valuesWithRowIds);

        int originalVersionId;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).FinalizeAsync(instanceId, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            originalVersionId = result.Data!.Id;
        }

        int newInstanceId;
        using (var ctx = CreateContext())
        {
            var amendResult = await CreateService(ctx).AmendAsync(originalVersionId, s.CollaboratorUserId, new AmendDocumentVersionModel
            {
                Reason = "Change in services",
                EffectiveDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            Assert.True(amendResult.Success, amendResult.Message);
            newInstanceId = amendResult.Data!.InstanceId;
        }

        using (var ctx = CreateContext())
        {
            var newInstance = ctx.DocumentInstances.Single(i => i.Id == newInstanceId);
            Assert.Equal(originalVersionId, newInstance.AmendsVersionId);
            Assert.Equal("Change in services", newInstance.AmendmentReason);
            Assert.Equal(DocumentInstanceStatus.Draft, newInstance.Status);
            Assert.NotNull(newInstance.RowVersion); // concurrency token live from the first read, like CreateAsync
            // Prefilled VERBATIM — every _rowId survives the copy.
            Assert.Contains(rowId1.ToString(), newInstance.ValuesJson);
            Assert.Contains(rowId2.ToString(), newInstance.ValuesJson);
        }

        int amendedVersionId;
        using (var ctx = CreateContext())
        {
            var finalizeResult = await CreateService(ctx).FinalizeAsync(newInstanceId, s.CollaboratorUserId);
            Assert.True(finalizeResult.Success, finalizeResult.Message);
            amendedVersionId = finalizeResult.Data!.Id;
            Assert.Equal(originalVersionId, finalizeResult.Data!.AmendsVersionId);
            Assert.Equal("Change in services", finalizeResult.Data!.AmendmentReason);
        }

        using (var ctx = CreateContext())
        {
            var amendedVersion = ctx.AuthoredDocumentVersions.Single(v => v.Id == amendedVersionId);
            Assert.Contains(rowId1.ToString(), amendedVersion.ValuesJson);
            Assert.Contains(rowId2.ToString(), amendedVersion.ValuesJson);

            var detail = await CreateService(ctx).GetVersionAsync(originalVersionId, s.CollaboratorUserId);
            Assert.True(detail.Success, detail.Message);
            Assert.Contains(amendedVersionId, detail.Data!.AmendedByVersionIds);
        }
    }

    [Fact]
    public async Task Amend_BlankReason_IsRejected()
    {
        var s = SeedSchoolWithStudent("amend-blank");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).AmendAsync(versionId, s.CollaboratorUserId, new AmendDocumentVersionModel { Reason = "  " });
        Assert.False(result.Success);
    }

    // ---------------------------------------------------------------- Plan 7: signed artifacts

    [Fact]
    public async Task SignedArtifactUpload_TransitionsSignatureStatus_AndIsListedAndDownloadable()
    {
        var s = SeedSchoolWithStudent("sig");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using (var ctx = CreateContext())
            Assert.Equal(SignatureStatus.Unsigned, ctx.AuthoredDocumentVersions.Single().SignatureStatus);

        var blob = new SuccessBlobStorageFake();
        int artifactId;
        using (var ctx = CreateContext())
        {
            var service = CreateSignedArtifactService(ctx, blob);
            var upload = await service.UploadAsync(s.CollaboratorUserId, versionId, new UploadSignedArtifactModel
            {
                FileStream = new MemoryStream(PdfBytes),
                FileName = "signed.pdf",
                ContentType = "application/pdf",
                SizeBytes = 3,
                SignerSummary = "Parent + case manager",
                SignatureStatus = SignatureStatus.PartiallySigned
            });
            Assert.True(upload.Success, upload.Message);
            artifactId = upload.Data!.Id;
        }

        using (var ctx = CreateContext())
            Assert.Equal(SignatureStatus.PartiallySigned, ctx.AuthoredDocumentVersions.Single().SignatureStatus);

        using (var ctx = CreateContext())
        {
            var service = CreateSignedArtifactService(ctx, blob);
            var list = await service.ListAsync(s.CollaboratorUserId, versionId);
            Assert.True(list.Success, list.Message);
            Assert.Single(list.Data!);
            Assert.Equal("signed.pdf", list.Data![0].FileName);

            var url = await service.GetDownloadUrlAsync(s.CollaboratorUserId, artifactId);
            Assert.True(url.Success, url.Message);
            Assert.Contains("sas=token", url.Data!);
        }

        // A second upload declaring fully Signed moves the version past PartiallySigned.
        using (var ctx = CreateContext())
        {
            var service = CreateSignedArtifactService(ctx, blob);
            var upload = await service.UploadAsync(s.CollaboratorUserId, versionId, new UploadSignedArtifactModel
            {
                FileStream = new MemoryStream(PdfBytes),
                FileName = "signed2.pdf",
                ContentType = "application/pdf",
                SizeBytes = 2,
                SignatureStatus = SignatureStatus.Signed
            });
            Assert.True(upload.Success, upload.Message);
        }

        using (var ctx = CreateContext())
        {
            Assert.Equal(SignatureStatus.Signed, ctx.AuthoredDocumentVersions.Single().SignatureStatus);
            Assert.Equal(2, ctx.SignedArtifacts.Count());
        }
    }

    [Fact]
    public async Task SignedArtifactUpload_UnsignedStatus_IsRejected()
    {
        var s = SeedSchoolWithStudent("sig-bad");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using var ctx = CreateContext();
        var service = CreateSignedArtifactService(ctx, new SuccessBlobStorageFake());
        var upload = await service.UploadAsync(s.CollaboratorUserId, versionId, new UploadSignedArtifactModel
        {
            FileStream = new MemoryStream(PdfBytes),
            FileName = "x.pdf",
            ContentType = "application/pdf",
            SizeBytes = 1,
            SignatureStatus = SignatureStatus.Unsigned
        });
        Assert.False(upload.Success);
    }

    [Fact]
    public async Task SignedArtifactUpload_ChecksTheBytesNotTheHeader_AndStoresABareFileName()
    {
        var s = SeedSchoolWithStudent("sig-guard");
        var keys = SeedTemplate(IepTypeId);
        var versionId = SeedFinalizedVersion(s, keys, IepTypeId, PdfRenderStatus.Rendered);

        using var ctx = CreateContext();
        var blob = new SuccessBlobStorageFake();
        var service = CreateSignedArtifactService(ctx, blob);

        // A declared application/pdf content type does not make an executable a PDF.
        var notPdf = await service.UploadAsync(s.CollaboratorUserId, versionId, new UploadSignedArtifactModel
        {
            FileStream = new MemoryStream(new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 }),
            FileName = "signed.pdf",
            ContentType = "application/pdf",
            SizeBytes = 5,
            SignatureStatus = SignatureStatus.Signed
        });
        Assert.False(notPdf.Success);
        Assert.Contains("PDF", notPdf.Message);
        Assert.Empty(ctx.SignedArtifacts);

        // A traversal-shaped client file name is reduced to a bare name before it is stored.
        var traversal = await service.UploadAsync(s.CollaboratorUserId, versionId, new UploadSignedArtifactModel
        {
            FileStream = new MemoryStream(PdfBytes),
            FileName = "../../etc/evil.pdf",
            ContentType = "application/pdf",
            SizeBytes = PdfBytes.Length,
            SignatureStatus = SignatureStatus.Signed
        });
        Assert.True(traversal.Success, traversal.Message);
        Assert.Equal("evil.pdf", traversal.Data!.FileName);
        Assert.DoesNotContain("..", ctx.SignedArtifacts.Single().FileName);
    }

    // ----------------------------------------------------------------- Multilingual plan phase 5

    [Fact]
    public async Task SignedArtifact_ListAsync_UnknownVersion_UnderSpanishCulture_MessageIsSpanish_AndMapsTo404ViaErrorKind()
    {
        var userId = 1;

        using var _lang = CultureScope.For("es");
        using var ctx = CreateContext();
        var result = await CreateSignedArtifactService(ctx, new SuccessBlobStorageFake()).ListAsync(userId, -1);

        Assert.False(result.Success);
        Assert.Equal("Versión del documento no encontrada.", result.Message);
        Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);

        var action = new TestController().MapServiceFailure(result);
        Assert.IsType<NotFoundObjectResult>(action);
    }

    public void Dispose() => _connection.Dispose();
}
