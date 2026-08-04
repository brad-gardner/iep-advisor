using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Phase 3 coverage for educator document-instance authoring: create pins the resolved Published
/// version (IEP and ETR both flow through the same code path), create is blocked when no template
/// resolves and denied for a non-collaborator, and SaveValues merges the patch, strips unknown field
/// keys, enforces per-FieldType type conformance, sanitizes RichText, blocks non-Draft edits, and
/// rejects a stale rowVersion. Real SQLite in-memory engine (same pattern as IepDraftServiceTests).
/// </summary>
public sealed class DocumentInstanceServiceTests : IDisposable
{
    private const int IepTypeId = 1;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public DocumentInstanceServiceTests()
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

    private DocumentInstanceService CreateService(ApplicationDbContext ctx)
        => new(
            ctx,
            new OrgAccessService(ctx),
            new TemplateResolutionService(ctx, NullLogger<TemplateResolutionService>.Instance),
            new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance),
            _audit,
            NullLogger<DocumentInstanceService>.Instance);

    // ---------------------------------------------------------------- Seed helpers

    private sealed record SchoolScenario(int SchoolId, int CollaboratorUserId, int StudentId);

    private SchoolScenario SeedSchoolWithStudent(string prefix, string? studentState = null, AccessRole role = AccessRole.Collaborator)
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

        var student = new SchoolStudent { SchoolId = school.Id, FirstName = "Sam", StateCode = studentState, IsActive = true };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();

        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess
        {
            SchoolStudentId = student.Id,
            UserId = user.Id,
            Role = role,
            IsActive = true
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

    /// <summary>Stable field keys of the seeded published template so tests can build value patches.</summary>
    private sealed record TemplateKeys(
        int VersionId, Guid TextKey, Guid CheckboxKey, Guid DateKey, Guid RichTextKey,
        Guid TableKey, Guid TableCol1Key, Guid TableCol2Key);

    /// <summary>
    /// Seeds a Published template for (state, docType) exercising every scalar type plus a Table with a
    /// Text column and a Date column. Returns the version id and the stable field/column keys.
    /// </summary>
    private TemplateKeys SeedPublishedTemplate(string? state, int docTypeId)
    {
        var textKey = Guid.NewGuid();
        var checkboxKey = Guid.NewGuid();
        var dateKey = Guid.NewGuid();
        var richKey = Guid.NewGuid();
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
            StateCode = state,
            DocumentTypeId = docTypeId,
            Name = $"{state ?? "Default"} template",
            Versions = { version }
        };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();

        var tableConfig = JsonSerializer.Serialize(new
        {
            columns = new object[]
            {
                new { columnKey = col1, type = "Text", label = "Note", required = false },
                new { columnKey = col2, type = "Date", label = "When", required = false }
            }
        });

        var section = new TemplateSection
        {
            DocumentTemplateVersionId = version.Id,
            SectionKey = Guid.NewGuid(),
            Title = "Section 1",
            DisplayOrder = 0,
            Fields =
            {
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = textKey, FieldType = FieldType.Text, Label = "Name", DisplayOrder = 0 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = checkboxKey, FieldType = FieldType.Checkbox, Label = "Eligible", DisplayOrder = 1 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = dateKey, FieldType = FieldType.Date, Label = "Meeting Date", DisplayOrder = 2 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = richKey, FieldType = FieldType.RichText, Label = "Narrative", DisplayOrder = 3 },
                new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = tableKey, FieldType = FieldType.Table, Label = "Services", ConfigJson = tableConfig, DisplayOrder = 4 }
            }
        };
        ctx.TemplateSections.Add(section);
        ctx.SaveChanges();

        return new TemplateKeys(version.Id, textKey, checkboxKey, dateKey, richKey, tableKey, col1, col2);
    }

    private static Dictionary<string, JsonElement> Patch(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private async Task<int> CreateInstanceAsync(SchoolScenario s, int docTypeId = IepTypeId)
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, docTypeId, s.CollaboratorUserId);
        Assert.True(result.Success, result.Message);
        return result.Data!.Id;
    }

    private static JsonElement ReadValues(ApplicationDbContext ctx, int instanceId)
    {
        var json = ctx.DocumentInstances.AsNoTracking().Single(i => i.Id == instanceId).ValuesJson;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ---------------------------------------------------------------- Create

    [Fact]
    public async Task Create_PinsResolvedVersion_AndReturnsTemplateTree()
    {
        var s = SeedSchoolWithStudent("create-iep");
        var keys = SeedPublishedTemplate(null, IepTypeId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, IepTypeId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(keys.VersionId, result.Data!.DocumentTemplateVersionId);
        Assert.Equal(DocumentInstanceStatus.Draft, result.Data.Status);
        Assert.Equal("{}", result.Data.ValuesJson);
        Assert.NotNull(result.Data.RowVersion);
        // The pinned template tree is included so the client can render the form.
        var section = Assert.Single(result.Data.TemplateVersion.Sections);
        Assert.Equal(5, section.Fields.Count);
    }

    [Fact]
    public async Task Create_ForEtr_UsesSameCodePath()
    {
        var s = SeedSchoolWithStudent("create-etr");
        var etrKeys = SeedPublishedTemplate(null, EtrTypeId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, EtrTypeId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(etrKeys.VersionId, result.Data!.DocumentTemplateVersionId);
        Assert.Equal(EtrTypeId, result.Data.DocumentTypeId);
        Assert.Equal("ETR", result.Data.DocumentTypeKey);
    }

    [Fact]
    public async Task Create_PrefersStudentStateTemplate()
    {
        var s = SeedSchoolWithStudent("create-oh", studentState: "OH");
        SeedPublishedTemplate(null, IepTypeId);
        var ohKeys = SeedPublishedTemplate("OH", IepTypeId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, IepTypeId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(ohKeys.VersionId, result.Data!.DocumentTemplateVersionId);
    }

    [Fact]
    public async Task Create_NoTemplate_IsBlocked()
    {
        var s = SeedSchoolWithStudent("create-blocked");
        // No template seeded at all.

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, IepTypeId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("no document template", result.Message!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ctx.DocumentInstances.ToList());
    }

    [Fact]
    public async Task Create_NonCollaborator_IsDenied()
    {
        var s = SeedSchoolWithStudent("create-authz");
        SeedPublishedTemplate(null, IepTypeId);
        var stranger = SeedStranger("create-stranger");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, IepTypeId, stranger);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AsViewer_IsDenied()
    {
        var s = SeedSchoolWithStudent("create-viewer", role: AccessRole.Viewer);
        SeedPublishedTemplate(null, IepTypeId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).CreateAsync(s.StudentId, IepTypeId, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_WritesAuditRecord()
    {
        var s = SeedSchoolWithStudent("create-audit");
        SeedPublishedTemplate(null, IepTypeId);

        _audit.Entries.Clear();
        int instanceId;
        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).CreateAsync(s.StudentId, IepTypeId, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            instanceId = result.Data!.Id;
        }

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AuditAction.Edit, entry.Action);
        Assert.Equal("DocumentInstance", entry.ResourceType);
        Assert.Equal(instanceId, entry.ResourceId);
        Assert.Equal(s.CollaboratorUserId, entry.ActorUserId);
    }

    // ---------------------------------------------------------------- SaveValues: merge + strip

    [Fact]
    public async Task SaveValues_MergesPatch_AndStripsUnknownKeys()
    {
        var s = SeedSchoolWithStudent("save-merge");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);
        var unknownKey = Guid.NewGuid();

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.TextKey}}": "Alice", "{{unknownKey}}": "ignored" }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        // Second patch merges (does not clobber the first key).
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.CheckboxKey}}": true }
            """);
            Assert.True((await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId)).Success);
        }

        using (var ctx = CreateContext())
        {
            var values = ReadValues(ctx, instanceId);
            Assert.Equal("Alice", values.GetProperty(keys.TextKey.ToString()).GetString());
            Assert.True(values.GetProperty(keys.CheckboxKey.ToString()).GetBoolean());
            Assert.False(values.TryGetProperty(unknownKey.ToString(), out _)); // unknown key stripped
        }
    }

    [Fact]
    public async Task SaveValues_Checkbox_RejectsNonBool()
    {
        var s = SeedSchoolWithStudent("save-checkbox");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using var ctx = CreateContext();
        var patch = Patch($$"""
        { "{{keys.CheckboxKey}}": "yes" }
        """);
        var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("checkbox", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveValues_Date_RejectsUnparseable()
    {
        var s = SeedSchoolWithStudent("save-date");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using var ctx = CreateContext();
        var patch = Patch($$"""
        { "{{keys.DateKey}}": "not-a-date" }
        """);
        var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("date", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveValues_Date_AcceptsParseableString()
    {
        var s = SeedSchoolWithStudent("save-date-ok");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.DateKey}}": "2026-01-15" }
            """);
            Assert.True((await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId)).Success);
        }

        using (var ctx = CreateContext())
            Assert.Equal("2026-01-15", ReadValues(ctx, instanceId).GetProperty(keys.DateKey.ToString()).GetString());
    }

    // ---------------------------------------------------------------- SaveValues: Table

    [Fact]
    public async Task SaveValues_Table_PersistsRows_AndStripsUnknownColumns()
    {
        var s = SeedSchoolWithStudent("save-table");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);
        var unknownCol = Guid.NewGuid();

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.TableKey}}": [
                { "{{keys.TableCol1Key}}": "Speech", "{{keys.TableCol2Key}}": "2026-02-01", "{{unknownCol}}": "drop me" },
                { "{{keys.TableCol1Key}}": "OT" }
            ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        using (var ctx = CreateContext())
        {
            var table = ReadValues(ctx, instanceId).GetProperty(keys.TableKey.ToString());
            Assert.Equal(2, table.GetArrayLength());
            var row0 = table[0];
            Assert.Equal("Speech", row0.GetProperty(keys.TableCol1Key.ToString()).GetString());
            Assert.Equal("2026-02-01", row0.GetProperty(keys.TableCol2Key.ToString()).GetString());
            Assert.False(row0.TryGetProperty(unknownCol.ToString(), out _)); // unknown column stripped
        }
    }

    [Fact]
    public async Task SaveValues_Table_RejectsBadCellType()
    {
        var s = SeedSchoolWithStudent("save-table-bad");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using var ctx = CreateContext();
        var patch = Patch($$"""
        { "{{keys.TableKey}}": [ { "{{keys.TableCol2Key}}": "not-a-date" } ] }
        """);
        var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("date", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveValues_Table_RejectsNonArray()
    {
        var s = SeedSchoolWithStudent("save-table-scalar");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using var ctx = CreateContext();
        var patch = Patch($$"""
        { "{{keys.TableKey}}": "oops" }
        """);
        var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("table", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- SaveValues: RichText sanitize

    [Fact]
    public async Task SaveValues_RichText_IsSanitized()
    {
        var s = SeedSchoolWithStudent("save-rich");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.RichTextKey}}": "<p>Hello</p><script>alert('x')</script><b onclick=\"evil()\">bold</b>" }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        using (var ctx = CreateContext())
        {
            var stored = ReadValues(ctx, instanceId).GetProperty(keys.RichTextKey.ToString()).GetString()!;
            Assert.DoesNotContain("<script", stored, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alert", stored, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onclick", stored, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<p>Hello</p>", stored); // safe formatting preserved
            Assert.Contains("bold", stored);
        }
    }

    // ---------------------------------------------------------------- SaveValues: size guard

    [Fact]
    public async Task SaveValues_OverSizeCap_IsRejected_AndLeavesPriorValuesUnchanged()
    {
        var s = SeedSchoolWithStudent("save-toobig");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        // First, persist a small legitimate value so we can prove the oversized save does not clobber it.
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.TextKey}}": "keep me" }
            """);
            Assert.True((await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId)).Success);
        }

        // A Text field whose value alone exceeds the 1 MB serialized cap.
        var huge = new string('a', DocumentInstanceService.MaxValuesJsonBytes + 1_000);
        using (var ctx = CreateContext())
        {
            var patch = new Dictionary<string, JsonElement>
            {
                [keys.TextKey.ToString()] = JsonSerializer.SerializeToElement(huge)
            };
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);

            Assert.False(result.Success);
            Assert.Contains("too large", result.Message!, StringComparison.OrdinalIgnoreCase);
        }

        // No partial write: the prior value is exactly what we stored, and the row was not touched.
        using (var ctx = CreateContext())
        {
            var values = ReadValues(ctx, instanceId);
            Assert.Equal("keep me", values.GetProperty(keys.TextKey.ToString()).GetString());
        }
    }

    // ---------------------------------------------------------------- SaveValues: status + concurrency + authz

    [Fact]
    public async Task SaveValues_BlockedWhenNotDraft()
    {
        var s = SeedSchoolWithStudent("save-finalizing");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            var instance = ctx.DocumentInstances.Single(i => i.Id == instanceId);
            instance.Status = DocumentInstanceStatus.Finalizing;
            ctx.SaveChanges();
        }

        using var ctx2 = CreateContext();
        var patch = Patch($$"""
        { "{{keys.TextKey}}": "late" }
        """);
        var result = await CreateService(ctx2).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);

        Assert.False(result.Success);
        Assert.Contains("no longer be edited", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveValues_StaleRowVersion_IsConcurrencyError()
    {
        var s = SeedSchoolWithStudent("save-stale");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        // Capture the current token, then save once (rotates the token).
        byte[] staleToken;
        using (var ctx = CreateContext())
            staleToken = ctx.DocumentInstances.AsNoTracking().Single(i => i.Id == instanceId).RowVersion!;

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.TextKey}}": "first" }
            """);
            Assert.True((await CreateService(ctx).SaveValuesAsync(instanceId, patch, staleToken, s.CollaboratorUserId)).Success);
        }

        // Reusing the now-stale token must fail with a friendly concurrency error.
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.TextKey}}": "second" }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, staleToken, s.CollaboratorUserId);
            Assert.False(result.Success);
            Assert.Contains("changed by someone else", result.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task SaveValues_RotatesRowVersion()
    {
        var s = SeedSchoolWithStudent("save-rotate");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        byte[] before;
        using (var ctx = CreateContext())
            before = ctx.DocumentInstances.AsNoTracking().Single(i => i.Id == instanceId).RowVersion!;

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.TextKey}}": "v" }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, before, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.Data!.RowVersion);
            Assert.False(before.AsSpan().SequenceEqual(result.Data.RowVersion));
        }
    }

    [Fact]
    public async Task SaveValues_NonCollaborator_IsDenied()
    {
        var s = SeedSchoolWithStudent("save-authz");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);
        var stranger = SeedStranger("save-stranger");

        using var ctx = CreateContext();
        var patch = Patch($$"""
        { "{{keys.TextKey}}": "x" }
        """);
        var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, stranger);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Get / List / Delete

    [Fact]
    public async Task Get_ReturnsInstanceWithTemplateTree()
    {
        var s = SeedSchoolWithStudent("get");
        SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetAsync(instanceId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(instanceId, result.Data!.Id);
        Assert.Single(result.Data.TemplateVersion.Sections);
    }

    [Fact]
    public async Task Get_NonCollaborator_IsDenied()
    {
        var s = SeedSchoolWithStudent("get-authz");
        SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);
        var stranger = SeedStranger("get-stranger");

        using var ctx = CreateContext();
        var result = await CreateService(ctx).GetAsync(instanceId, stranger);

        Assert.False(result.Success);
        Assert.Contains("permission", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_ReturnsStudentInstances()
    {
        var s = SeedSchoolWithStudent("list");
        SeedPublishedTemplate(null, IepTypeId);
        SeedPublishedTemplate(null, EtrTypeId);
        await CreateInstanceAsync(s, IepTypeId);
        await CreateInstanceAsync(s, EtrTypeId);

        using var ctx = CreateContext();
        var result = await CreateService(ctx).ListForStudentAsync(s.StudentId, s.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(2, result.Data!.Count);
        Assert.Contains(result.Data!, r => r.DocumentTypeKey == "IEP");
        Assert.Contains(result.Data!, r => r.DocumentTypeKey == "ETR");
    }

    [Fact]
    public async Task Delete_Draft_Succeeds()
    {
        var s = SeedSchoolWithStudent("delete");
        SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
            Assert.True((await CreateService(ctx).DeleteAsync(instanceId, s.CollaboratorUserId)).Success);

        using (var ctx = CreateContext())
            Assert.Empty(ctx.DocumentInstances.ToList());
    }

    [Fact]
    public async Task Delete_NonDraft_IsBlocked()
    {
        var s = SeedSchoolWithStudent("delete-final");
        SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            var instance = ctx.DocumentInstances.Single(i => i.Id == instanceId);
            instance.Status = DocumentInstanceStatus.Finalized;
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).DeleteAsync(instanceId, s.CollaboratorUserId);
            Assert.False(result.Success);
            Assert.Contains("draft", result.Message!, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void Dispose() => _connection.Dispose();
}
