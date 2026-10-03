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

        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Sam", StateCode = studentState, IsActive = true };
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

    /// <summary>
    /// Pilot-gates plan, phase 1: SharedDraftRevision -&gt; DocumentInstance is Restrict now, not
    /// Cascade (SQL Server disallows an immutability trigger on a table with any cascading FK
    /// touching it). A Draft-status instance can legitimately have been shared with the family before
    /// someone tries to delete it — that must surface as a friendly refusal, never a silent
    /// cascade-delete of the family's shared history nor an unhandled 500.
    /// </summary>
    [Fact]
    public async Task Delete_Draft_WithSharedRevision_IsBlocked()
    {
        var s = SeedSchoolWithStudent("delete-shared");
        var keys = SeedPublishedTemplate(null, IepTypeId);
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            ctx.SharedDraftRevisions.Add(new SharedDraftRevision
            {
                DocumentInstanceId = instanceId,
                RevisionNumber = 1,
                ValuesJson = "{}",
                DocumentTemplateVersionId = keys.VersionId,
                SharedByUserId = s.CollaboratorUserId,
                SharedAt = DateTime.UtcNow,
                Status = SharedDraftStatus.Active
            });
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var result = await CreateService(ctx).DeleteAsync(instanceId, s.CollaboratorUserId);
            Assert.False(result.Success);
            Assert.Contains("shared", result.Message!, StringComparison.OrdinalIgnoreCase);
        }

        using (var ctx = CreateContext())
        {
            // Neither the instance nor its shared revision was touched by the failed attempt.
            Assert.NotNull(ctx.DocumentInstances.Find(instanceId));
            Assert.Single(ctx.SharedDraftRevisions.Where(r => r.DocumentInstanceId == instanceId));
        }
    }

    // ---------------------------------------------------------------- Owners + objectives (plan 2026-10-02-002)

    private sealed record GoalsTemplateKeys(int VersionId, Guid GoalsFieldKey, Guid GoalTextCol, Guid PlainTableFieldKey, Guid PlainTableCol);

    /// <summary>A Goals-semantic table (one Text column, GoalText-tagged) alongside a semantic-less table
    /// field, so a test can exercise both "owner-eligible" and "not owner-eligible" tables side by side.</summary>
    private GoalsTemplateKeys SeedGoalsTemplate(int docTypeId = IepTypeId)
    {
        var goalsFieldKey = Guid.NewGuid();
        var goalTextCol = Guid.NewGuid();
        var plainTableFieldKey = Guid.NewGuid();
        var plainTableCol = Guid.NewGuid();

        using var ctx = CreateContext();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = docTypeId, Name = "Goals template", Versions = { version } });
        ctx.SaveChanges();

        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id, SectionKey = Guid.NewGuid(), Title = "Goals", DisplayOrder = 0,
            Fields =
            {
                new TemplateField
                {
                    DocumentTemplateVersionId = version.Id, FieldKey = goalsFieldKey, FieldType = FieldType.Table, Label = "Goals", DisplayOrder = 0,
                    ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals, (goalTextCol, FieldType.Text, "Goal", ColumnSemantics.GoalText))
                },
                new TemplateField
                {
                    DocumentTemplateVersionId = version.Id, FieldKey = plainTableFieldKey, FieldType = FieldType.Table, Label = "Notes", DisplayOrder = 1,
                    ConfigJson = TemplateGraphBuilder.TableConfig(null, (plainTableCol, FieldType.Text, "Note", null))
                }
            }
        });
        ctx.SaveChanges();

        return new GoalsTemplateKeys(version.Id, goalsFieldKey, goalTextCol, plainTableFieldKey, plainTableCol);
    }

    [Fact]
    public async Task SaveValues_KeepsOwner_WhenAnActiveTeamMember()
    {
        var s = SeedSchoolWithStudent("owner-kept");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        using (var ctx = CreateContext())
        {
            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = s.StudentId, UserId = s.CollaboratorUserId, TeamRole = TeamRole.CaseManager, IsActive = true });
            ctx.SaveChanges();
        }

        DocumentInstanceValuesModel saved;
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_ownerUserId": {{s.CollaboratorUserId}} } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            saved = result.Data!;
        }

        Assert.Empty(saved.Warnings);
        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        Assert.Equal(s.CollaboratorUserId, row.GetProperty(RowMetaKeys.OwnerUserId).GetInt32());
    }

    [Fact]
    public async Task SaveValues_DropsOwner_WithWarning_WhenNotAnActiveTeamMember()
    {
        var s = SeedSchoolWithStudent("owner-dropped");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        var strangerId = SeedStranger("owner-dropped-stranger");

        DocumentInstanceValuesModel saved;
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_ownerUserId": {{strangerId}} } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            saved = result.Data!;
        }

        var warning = Assert.Single(saved.Warnings);
        Assert.Equal("ownerNotTeamMember", warning.Code);
        Assert.Equal(keys.GoalsFieldKey.ToString(), warning.FieldKey);
        Assert.NotEmpty(warning.RowId);

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        Assert.False(row.TryGetProperty(RowMetaKeys.OwnerUserId, out _));
    }

    [Fact]
    public async Task SaveValues_DropsOwner_WithWarning_WhenAnInactiveTeamMemberOfTheSameStudent()
    {
        var s = SeedSchoolWithStudent("owner-inactive");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        var formerMemberId = SeedStranger("owner-inactive-former");
        using (var ctx = CreateContext())
        {
            // On the SAME student's team, but no longer active — must be treated exactly like a stranger.
            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = s.StudentId, UserId = formerMemberId, TeamRole = TeamRole.CaseManager, IsActive = false });
            ctx.SaveChanges();
        }

        DocumentInstanceValuesModel saved;
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_ownerUserId": {{formerMemberId}} } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            saved = result.Data!;
        }

        var warning = Assert.Single(saved.Warnings);
        Assert.Equal("ownerNotTeamMember", warning.Code);

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        Assert.False(row.TryGetProperty(RowMetaKeys.OwnerUserId, out _));
    }

    [Fact]
    public async Task SaveValues_DropsOwner_WithWarning_WhenAnActiveMemberOfAnotherStudentsTeam()
    {
        var s = SeedSchoolWithStudent("owner-other-student-a");
        var other = SeedSchoolWithStudent("owner-other-student-b");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        using (var ctx = CreateContext())
        {
            // Active, but on the OTHER student's team — must still be dropped for THIS student's document.
            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = other.StudentId, UserId = other.CollaboratorUserId, TeamRole = TeamRole.CaseManager, IsActive = true });
            ctx.SaveChanges();
        }

        DocumentInstanceValuesModel saved;
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_ownerUserId": {{other.CollaboratorUserId}} } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            saved = result.Data!;
        }

        var warning = Assert.Single(saved.Warnings);
        Assert.Equal("ownerNotTeamMember", warning.Code);

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        Assert.False(row.TryGetProperty(RowMetaKeys.OwnerUserId, out _));
    }

    [Fact]
    public async Task SaveValues_DropsOwner_WithWarning_WhenOwnerUserIdIsNotANumber()
    {
        var s = SeedSchoolWithStudent("owner-non-number");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        using (var ctx = CreateContext())
        {
            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = s.StudentId, UserId = s.CollaboratorUserId, TeamRole = TeamRole.CaseManager, IsActive = true });
            ctx.SaveChanges();
        }

        DocumentInstanceValuesModel saved;
        using (var ctx = CreateContext())
        {
            // A string (or any non-number, non-null) `_ownerUserId` on an owner-eligible table is rejected
            // with the same warning as a non-member — never silently coerced, never kept.
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_ownerUserId": "not-a-number" } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            saved = result.Data!;
        }

        var warning = Assert.Single(saved.Warnings);
        Assert.Equal("ownerNotTeamMember", warning.Code);

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        Assert.False(row.TryGetProperty(RowMetaKeys.OwnerUserId, out _));
    }

    [Fact]
    public async Task SaveValues_DropsOwner_Silently_WhenTableSemanticIsNotOwnerEligible()
    {
        var s = SeedSchoolWithStudent("owner-wrong-semantic");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        using (var ctx = CreateContext())
        {
            // The acting user IS an active team member — proves the drop is about the table's semantic,
            // not the user's eligibility.
            ctx.StudentTeamMembers.Add(new StudentTeamMember { SchoolStudentId = s.StudentId, UserId = s.CollaboratorUserId, TeamRole = TeamRole.CaseManager, IsActive = true });
            ctx.SaveChanges();
        }

        DocumentInstanceValuesModel saved;
        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.PlainTableFieldKey}}": [ { "{{keys.PlainTableCol}}": "A note", "_ownerUserId": {{s.CollaboratorUserId}} } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
            saved = result.Data!;
        }

        Assert.Empty(saved.Warnings); // wrong-semantic table: dropped like any other stray key, no warning
        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.PlainTableFieldKey.ToString())[0];
        Assert.False(row.TryGetProperty(RowMetaKeys.OwnerUserId, out _));
    }

    [Fact]
    public async Task SaveValues_Objectives_AssignsAndDedupesIds_CapsAtTwenty_DropsUnknownKeys()
    {
        var s = SeedSchoolWithStudent("objectives");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);

        var duplicateId = Guid.NewGuid();
        var objectives = new List<string>
        {
            $$"""{ "_rowId": "{{duplicateId}}", "description": "Read a paragraph", "criteria": "80% accuracy", "targetDate": "2026-12-01", "haunted": "dropped" }""",
            $$"""{ "_rowId": "{{duplicateId}}", "description": "Duplicate id gets a fresh one" }""" // same id as above -> re-issued
        };
        for (var i = objectives.Count; i < 22; i++) // push the total to 22 so the cap (20) actually trims
            objectives.Add($$"""{ "description": "Extra {{i}}" }""");
        var objectivesJson = "[" + string.Join(",", objectives) + "]";

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_objectives": {{objectivesJson}} } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        var savedObjectives = row.GetProperty(RowMetaKeys.Objectives);
        Assert.Equal(20, savedObjectives.GetArrayLength()); // capped, extras dropped, order preserved

        var first = savedObjectives[0];
        Assert.Equal("Read a paragraph", first.GetProperty("description").GetString());
        Assert.Equal("80% accuracy", first.GetProperty("criteria").GetString());
        Assert.Equal("2026-12-01", first.GetProperty("targetDate").GetString());
        Assert.False(first.TryGetProperty("haunted", out _)); // unknown key dropped
        var firstId = first.GetProperty(RowMetaKeys.RowId).GetString();
        Assert.True(Guid.TryParse(firstId, out var parsedFirstId) && parsedFirstId != Guid.Empty);

        var second = savedObjectives[1];
        var secondId = second.GetProperty(RowMetaKeys.RowId).GetString();
        Assert.NotEqual(firstId, secondId); // duplicate id was replaced with a fresh one, not silently merged
        Assert.True(Guid.TryParse(secondId, out var parsedSecondId) && parsedSecondId != Guid.Empty);
    }

    [Fact]
    public async Task SaveValues_Objectives_DroppedSilently_OnANonGoalsTable()
    {
        var s = SeedSchoolWithStudent("objectives-wrong-semantic");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.PlainTableFieldKey}}": [ { "{{keys.PlainTableCol}}": "A note", "_objectives": [ { "description": "Should not be kept" } ] } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.PlainTableFieldKey.ToString())[0];
        Assert.False(row.TryGetProperty(RowMetaKeys.Objectives, out _));
    }

    /// <summary>
    /// Review fix (plan 2026-10-02-002, P3): clearing every objective on an otherwise-empty row (no goal
    /// text, no owner) must drop the row entirely, the same as a row that never had objectives — not leave
    /// behind a vacuous <c>{ _rowId, _objectives: [] }</c> that CoerceTable's "row reduced to nothing"
    /// check previously treated as real content because <c>_objectives</c> was non-null.
    /// </summary>
    [Fact]
    public async Task SaveValues_Objectives_EmptyArray_OnAnOtherwiseEmptyRow_DropsTheRowEntirely()
    {
        var s = SeedSchoolWithStudent("objectives-cleared");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "_objectives": [] } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        using var verify = CreateContext();
        var rows = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString());
        Assert.Equal(0, rows.GetArrayLength());
    }

    [Fact]
    public async Task SaveValues_Objectives_TruncatesCriteria_LikeDescription()
    {
        var s = SeedSchoolWithStudent("objectives-criteria-truncate");
        var keys = SeedGoalsTemplate();
        var instanceId = await CreateInstanceAsync(s);
        var longCriteria = new string('c', 2100);

        using (var ctx = CreateContext())
        {
            var patch = Patch($$"""
            { "{{keys.GoalsFieldKey}}": [ { "{{keys.GoalTextCol}}": "Read better", "_objectives": [ { "description": "x", "criteria": "{{longCriteria}}" } ] } ] }
            """);
            var result = await CreateService(ctx).SaveValuesAsync(instanceId, patch, null, s.CollaboratorUserId);
            Assert.True(result.Success, result.Message);
        }

        using var verify = CreateContext();
        var row = ReadValues(verify, instanceId).GetProperty(keys.GoalsFieldKey.ToString())[0];
        var criteria = row.GetProperty(RowMetaKeys.Objectives)[0].GetProperty("criteria").GetString();
        Assert.Equal(2000, criteria!.Length);
    }

    public void Dispose() => _connection.Dispose();
}
