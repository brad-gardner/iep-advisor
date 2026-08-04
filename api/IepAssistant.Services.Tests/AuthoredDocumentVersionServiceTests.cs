using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Api.BackgroundServices;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Implementations;
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
            new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance),
            blob ?? new SuccessBlobStorageFake(),
            _audit,
            NullLogger<AuthoredDocumentVersionService>.Instance);

    private AuthoredDocumentPdfService CreatePdfService(ApplicationDbContext ctx, IBlobStorageService blob)
        => new(ctx, new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance), blob, NullLogger<AuthoredDocumentPdfService>.Instance);

    // ---- Blob fakes ----
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

        var student = new SchoolStudent { SchoolId = school.Id, FirstName = "Sam", IsActive = true };
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
            Pdf = pdfStatus is PdfRenderStatus status
                ? new AuthoredDocumentPdf
                {
                    RenderStatus = status,
                    RenderedAt = status == PdfRenderStatus.Rendered ? new DateTime(2026, 7, 20, 1, 0, 0, DateTimeKind.Utc) : null,
                    ErrorMessage = status == PdfRenderStatus.Error ? "prior render failed" : null
                }
                : null
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
                Pdf = new AuthoredDocumentPdf { RenderStatus = PdfRenderStatus.Pending }
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
        await foreach (var id in queue.DequeueAllAsync(CancellationToken.None))
        {
            if (id == sentinel) break;
            drained.Add(id);
        }
        return drained;
    }

    public void Dispose() => _connection.Dispose();
}
