using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Authoring-spine coverage (plan 2026-09-15-001): semantic tags validate against the closed vocabularies
/// and round-trip through the reader; every persisted Table row carries a stable <c>_rowId</c> that
/// survives edits and duplicates get fresh ids; the launch-state catalog seeds OH IEP / OH ETR / default
/// 504 idempotently and every document type resolves; a pre-semantics default IEP template is upgraded
/// to a semantic v2 without touching v1.
/// </summary>
public sealed class DocumentSemanticsAndRowIdentityTests : IDisposable
{
    private const int IepTypeId = 1;
    private const int Section504TypeId = 2;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DocumentSemanticsAndRowIdentityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private DocumentInstanceService CreateInstanceService(ApplicationDbContext ctx) => new(
        ctx,
        new OrgAccessService(ctx),
        new TemplateResolutionService(ctx, NullLogger<TemplateResolutionService>.Instance),
        new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance),
        new CapturingAuditLogger(),
        NullLogger<DocumentInstanceService>.Instance);

    // ---------------------------------------------------------------- Validator: semantics

    [Theory]
    [InlineData(FieldType.Text, """{"semantic":"presentLevels"}""")]
    [InlineData(FieldType.RichText, """{"semantic":"studentProfile"}""")]
    [InlineData(FieldType.Checkbox, """{"semantic":"extendedSchoolYear"}""")]
    [InlineData(FieldType.Date, """{"semantic":"meetingDate"}""")]
    [InlineData(FieldType.Select, """{"semantic":"placement","options":[{"value":"a"}]}""")]
    public void Validator_AcceptsKnownFieldSemantics(FieldType type, string config)
        => Assert.Null(TemplateFieldConfigValidator.Validate(type, config));

    [Theory]
    [InlineData(FieldType.Text, """{"semantic":"nope"}""")]
    [InlineData(FieldType.RichText, """{"semantic":"Goals"}""")] // case-sensitive
    [InlineData(FieldType.Checkbox, """{"semantic":"x"}""")]
    public void Validator_RejectsUnknownFieldSemantics(FieldType type, string config)
        => Assert.Contains("not a recognized field semantic", TemplateFieldConfigValidator.Validate(type, config));

    [Fact]
    public void Validator_Table_AcceptsSemanticsAndRejectsUnknownOrDuplicateColumnSemantics()
    {
        var ok = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals,
            (Guid.NewGuid(), FieldType.Text, "Goal", ColumnSemantics.GoalText),
            (Guid.NewGuid(), FieldType.Text, "Baseline", ColumnSemantics.Baseline));
        Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Table, ok));

        var unknownTable = TemplateGraphBuilder.TableConfig("mystery", (Guid.NewGuid(), FieldType.Text, "Goal", null));
        Assert.Contains("not a recognized field semantic", TemplateFieldConfigValidator.Validate(FieldType.Table, unknownTable));

        var unknownColumn = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals, (Guid.NewGuid(), FieldType.Text, "Goal", "banana"));
        Assert.Contains("not a recognized column semantic", TemplateFieldConfigValidator.Validate(FieldType.Table, unknownColumn));

        var dup = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals,
            (Guid.NewGuid(), FieldType.Text, "Goal", ColumnSemantics.GoalText),
            (Guid.NewGuid(), FieldType.Text, "Goal 2", ColumnSemantics.GoalText));
        Assert.Contains("must be unique", TemplateFieldConfigValidator.Validate(FieldType.Table, dup));
    }

    [Fact]
    public void SemanticsReader_ResolvesFieldsAndColumnsBySemantic()
    {
        var goalCol = Guid.NewGuid();
        var baselineCol = Guid.NewGuid();
        var goalsKey = Guid.NewGuid();
        var plaafpKey = Guid.NewGuid();
        var sections = new List<TemplateSection>
        {
            new()
            {
                Title = "Profile", DisplayOrder = 0,
                Fields =
                {
                    new TemplateField { FieldKey = plaafpKey, FieldType = FieldType.RichText, Label = "PLAAFP", ConfigJson = TemplateGraphBuilder.RichTextConfig(FieldSemantics.PresentLevels), DisplayOrder = 0 },
                    new TemplateField { FieldKey = Guid.NewGuid(), FieldType = FieldType.RichText, Label = "Untagged", ConfigJson = null, DisplayOrder = 1 },
                }
            },
            new()
            {
                Title = "Goals", DisplayOrder = 1,
                Fields =
                {
                    new TemplateField
                    {
                        FieldKey = goalsKey, FieldType = FieldType.Table, Label = "Goals", DisplayOrder = 0,
                        ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals,
                            (goalCol, FieldType.Text, "Goal", ColumnSemantics.GoalText),
                            (baselineCol, FieldType.Text, "Baseline", ColumnSemantics.Baseline),
                            (Guid.NewGuid(), FieldType.Text, "Notes", null))
                    }
                }
            }
        };

        var map = TemplateSemanticsReader.Read(sections);

        Assert.Equal(2, map.Count);
        Assert.Equal(plaafpKey, map[FieldSemantics.PresentLevels].FieldKey);
        Assert.Equal("Profile", map[FieldSemantics.PresentLevels].SectionTitle);
        var goals = map[FieldSemantics.Goals];
        Assert.Equal(goalsKey, goals.FieldKey);
        Assert.Equal(goalCol, goals.Columns[ColumnSemantics.GoalText]);
        Assert.Equal(baselineCol, goals.Columns[ColumnSemantics.Baseline]);
        Assert.Equal(2, goals.Columns.Count);
    }

    // ---------------------------------------------------------------- Row identity

    private sealed record Scenario(int StudentId, int UserId, Guid TableKey, Guid GoalCol);

    private Scenario SeedScenario(string prefix)
    {
        using var ctx = CreateContext();
        var user = new User { Email = $"{prefix}@example.com", PasswordHash = "x", FirstName = "E", LastName = "D", Role = UserRole.Educator };
        ctx.Users.Add(user); ctx.SaveChanges();
        var district = new District { Name = $"{prefix}-d" };
        ctx.Districts.Add(district); ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = $"{prefix}-s" };
        ctx.Schools.Add(school); ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = user.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Kid" };
        ctx.SchoolStudents.Add(student); ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = user.Id, Role = AccessRole.Collaborator, IsActive = true });

        var tableKey = Guid.NewGuid();
        var goalCol = Guid.NewGuid();
        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        ctx.DocumentTemplates.Add(new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = "T", Versions = { version } });
        ctx.SaveChanges();
        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id, SectionKey = Guid.NewGuid(), Title = "Goals", DisplayOrder = 0,
            Fields =
            {
                new TemplateField
                {
                    DocumentTemplateVersionId = version.Id, FieldKey = tableKey, FieldType = FieldType.Table, Label = "Goals", DisplayOrder = 0,
                    ConfigJson = TemplateGraphBuilder.TableConfig(FieldSemantics.Goals, (goalCol, FieldType.Text, "Goal", ColumnSemantics.GoalText))
                }
            }
        });
        ctx.SaveChanges();
        return new Scenario(student.Id, user.Id, tableKey, goalCol);
    }

    private static Dictionary<string, JsonElement> Patch(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static JsonElement ReadTable(ApplicationDbContext ctx, int instanceId, Guid tableKey)
    {
        var json = ctx.DocumentInstances.AsNoTracking().Single(i => i.Id == instanceId).ValuesJson;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty(tableKey.ToString()).Clone();
    }

    [Fact]
    public async Task SaveValues_AssignsStableRowIds_PreservesThem_AndReplacesDuplicates()
    {
        var s = SeedScenario("rowid");
        int instanceId;
        using (var ctx = CreateContext())
        {
            var created = await CreateInstanceService(ctx).CreateAsync(s.StudentId, IepTypeId, s.UserId);
            Assert.True(created.Success, created.Message);
            instanceId = created.Data!.Id;
        }

        // First save: rows without ids get fresh GUIDs.
        string firstId;
        using (var ctx = CreateContext())
        {
            var r = await CreateInstanceService(ctx).SaveValuesAsync(instanceId,
                Patch($$"""
                { "{{s.TableKey}}": [ { "{{s.GoalCol}}": "Read 80 wpm" }, { "{{s.GoalCol}}": "Write a paragraph" } ] }
                """),
                null, s.UserId);
            Assert.True(r.Success, r.Message);
        }
        using (var ctx = CreateContext())
        {
            var table = ReadTable(ctx, instanceId, s.TableKey);
            Assert.Equal(2, table.GetArrayLength());
            firstId = table[0].GetProperty(RowMetaKeys.RowId).GetString()!;
            Assert.True(Guid.TryParse(firstId, out var g) && g != Guid.Empty);
            Assert.NotEqual(firstId, table[1].GetProperty(RowMetaKeys.RowId).GetString());
        }

        // Second save echoing the id back with an edit keeps it; a duplicated id gets a fresh one; junk id replaced.
        using (var ctx = CreateContext())
        {
            var r = await CreateInstanceService(ctx).SaveValuesAsync(instanceId,
                Patch($$"""
                { "{{s.TableKey}}": [
                    { "_rowId": "{{firstId}}", "{{s.GoalCol}}": "Read 90 wpm" },
                    { "_rowId": "{{firstId}}", "{{s.GoalCol}}": "copy of row" },
                    { "_rowId": "not-a-guid", "{{s.GoalCol}}": "junk id" } ] }
                """),
                null, s.UserId);
            Assert.True(r.Success, r.Message);
        }
        using (var ctx = CreateContext())
        {
            var table = ReadTable(ctx, instanceId, s.TableKey);
            Assert.Equal(3, table.GetArrayLength());
            Assert.Equal(firstId, table[0].GetProperty(RowMetaKeys.RowId).GetString());
            Assert.Equal("Read 90 wpm", table[0].GetProperty(s.GoalCol.ToString()).GetString());
            var ids = Enumerable.Range(0, 3).Select(i => table[i].GetProperty(RowMetaKeys.RowId).GetString()).ToList();
            Assert.Equal(3, ids.Distinct().Count());
            Assert.All(ids, id => Assert.True(Guid.TryParse(id, out _)));
        }
    }

    // ---------------------------------------------------------------- Catalog seeder

    [Fact]
    public async Task CatalogSeed_CreatesOhioIepEtrAndDefault504_Idempotently_AndEveryTypeResolves()
    {
        TemplateCatalogSeedResult first, second;
        using (var ctx = CreateContext())
            first = await new TemplateCatalogSeeder(ctx, NullLogger<TemplateCatalogSeeder>.Instance).SeedAsync();
        using (var ctx = CreateContext())
            second = await new TemplateCatalogSeeder(ctx, NullLogger<TemplateCatalogSeeder>.Instance).SeedAsync();

        Assert.Equal(3, first.Created.Count);
        Assert.Empty(second.Created);
        Assert.Equal(3, second.Skipped.Count);

        using var verify = CreateContext();
        var resolution = new TemplateResolutionService(verify, NullLogger<TemplateResolutionService>.Instance);
        var ohIep = await resolution.ResolveAsync("OH", IepTypeId);
        var ohEtr = await resolution.ResolveAsync("OH", EtrTypeId);
        var def504 = await resolution.ResolveAsync(null, Section504TypeId);
        var paEtr = await resolution.ResolveAsync("PA", EtrTypeId); // no PA template, no default ETR → null
        Assert.True(ohIep.Success, ohIep.Message);
        Assert.True(ohEtr.Success, ohEtr.Message);
        Assert.True(def504.Success, def504.Message);
        Assert.False(paEtr.Success);

        // Every seeded field config validates, and the OH IEP exposes the block semantics downstream code relies on.
        var versions = verify.DocumentTemplateVersions.AsNoTracking().Include(v => v.Sections).ThenInclude(s => s.Fields).ToList();
        foreach (var field in versions.SelectMany(v => v.Sections).SelectMany(s => s.Fields))
            Assert.Null(TemplateFieldConfigValidator.Validate(field.FieldType, field.ConfigJson));

        var ohIepVersion = versions.Single(v => v.Id == ohIep.Data!.DocumentTemplateVersionId);
        var semantics = TemplateSemanticsReader.Read(ohIepVersion.Sections);
        Assert.Contains(FieldSemantics.Goals, semantics.Keys);
        Assert.Contains(FieldSemantics.Services, semantics.Keys);
        Assert.Contains(FieldSemantics.Accommodations, semantics.Keys);
        Assert.Contains(FieldSemantics.PresentLevels, semantics.Keys);
        Assert.Contains(FieldSemantics.StudentProfile, semantics.Keys);
        Assert.Equal(6, semantics[FieldSemantics.Goals].Columns.Count);

        var ohEtrVersion = versions.Single(v => v.Id == ohEtr.Data!.DocumentTemplateVersionId);
        var etrSemantics = TemplateSemanticsReader.Read(ohEtrVersion.Sections);
        Assert.Contains(FieldSemantics.EligibilityDetermination, etrSemantics.Keys);
        Assert.Contains(FieldSemantics.EvaluatorReports, etrSemantics.Keys);

        // Deterministic keys are unique across the whole catalog.
        var allKeys = versions.SelectMany(v => v.Sections).SelectMany(s => s.Fields).Select(f => f.FieldKey).ToList();
        Assert.Equal(allKeys.Count, allKeys.Distinct().Count());
    }

    // ---------------------------------------------------------------- State inheritance on create

    [Fact]
    public async Task Create_ResolvesStateFromSchoolThenDistrict_WhenStudentHasNone()
    {
        // District OH, school with no state, student with no state → the OH ETR must resolve.
        int studentId, userId;
        using (var ctx = CreateContext())
        {
            var user = new User { Email = "inherit@example.com", PasswordHash = "x", FirstName = "E", LastName = "D", Role = UserRole.Educator };
            ctx.Users.Add(user); ctx.SaveChanges();
            var district = new District { Name = "OH district", StateCode = "OH" };
            ctx.Districts.Add(district); ctx.SaveChanges();
            var school = new School { DistrictId = district.Id, Name = "s", StateCode = null };
            ctx.Schools.Add(school); ctx.SaveChanges();
            ctx.StaffProfiles.Add(new StaffProfile { UserId = user.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
            var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Kid", StateCode = null };
            ctx.SchoolStudents.Add(student); ctx.SaveChanges();
            ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = user.Id, Role = AccessRole.Collaborator, IsActive = true });
            ctx.SaveChanges();
            studentId = student.Id; userId = user.Id;
            await new TemplateCatalogSeeder(ctx, NullLogger<TemplateCatalogSeeder>.Instance).SeedAsync();
        }

        using var verify = CreateContext();
        var etr = await CreateInstanceService(verify).CreateAsync(studentId, EtrTypeId, userId);
        Assert.True(etr.Success, etr.Message);
        var pinned = verify.DocumentTemplateVersions.AsNoTracking().Include(v => v.DocumentTemplate)
            .Single(v => v.Id == etr.Data!.DocumentTemplateVersionId);
        Assert.Equal("OH", pinned.DocumentTemplate.StateCode);
    }

    // ---------------------------------------------------------------- Default IEP semantic upgrade

    /// <summary>Seeds a pre-semantics default IEP exactly as the old seeder did (same keys, no tags).</summary>
    private (int TemplateId, int V1Id) SeedPristineLegacyDefault()
    {
        using var ctx = CreateContext();
        var v1 = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        var template = new DocumentTemplate { StateCode = null, DocumentTypeId = IepTypeId, Name = DefaultIepTemplateSeeder.DefaultTemplateName, Versions = { v1 } };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();
        var section = new TemplateSection { DocumentTemplateVersionId = v1.Id, SectionKey = Guid.NewGuid(), Title = "All", DisplayOrder = 0 };
        var order = 0;
        foreach (var key in DefaultIepTemplateSeeder.Keys.AllFieldKeys)
        {
            var isTable = key == DefaultIepTemplateSeeder.Keys.GoalsTableField || key == DefaultIepTemplateSeeder.Keys.ServicesTableField
                || key == DefaultIepTemplateSeeder.Keys.AccommodationsTableField || key == DefaultIepTemplateSeeder.Keys.TransitionTableField;
            section.Fields.Add(new TemplateField
            {
                DocumentTemplateVersionId = v1.Id, FieldKey = key, DisplayOrder = order++, Label = "f",
                FieldType = isTable ? FieldType.Table : FieldType.RichText,
                ConfigJson = isTable ? TemplateGraphBuilder.TableConfig(null, (Guid.NewGuid(), FieldType.Text, "Col", null)) : null
            });
        }
        ctx.TemplateSections.Add(section);
        ctx.SaveChanges();
        return (template.Id, v1.Id);
    }

    [Fact]
    public async Task DefaultSeed_UpgradesPristinePreSemanticTemplateToSemanticV2_LeavingV1Untouched()
    {
        var (templateId, v1Id) = SeedPristineLegacyDefault();

        DefaultIepTemplateSeedResult first, second;
        using (var ctx = CreateContext())
            first = await new DefaultIepTemplateSeeder(ctx, NullLogger<DefaultIepTemplateSeeder>.Instance).SeedAsync();
        using (var ctx = CreateContext())
            second = await new DefaultIepTemplateSeeder(ctx, NullLogger<DefaultIepTemplateSeeder>.Instance).SeedAsync();

        Assert.Equal(DefaultIepTemplateSeedOutcome.Upgraded, first.Outcome);
        Assert.Equal(DefaultIepTemplateSeedOutcome.AlreadySeeded, second.Outcome);

        using var verify = CreateContext();
        var versions = verify.DocumentTemplateVersions.AsNoTracking().Where(v => v.DocumentTemplateId == templateId)
            .Include(v => v.Sections).ThenInclude(s => s.Fields).OrderBy(v => v.VersionNumber).ToList();
        Assert.Equal(2, versions.Count);
        Assert.Equal(v1Id, versions[0].Id);
        Assert.Single(versions[0].Sections); // v1 untouched
        Assert.Equal(TemplateVersionStatus.Published, versions[1].Status);
        Assert.Contains(FieldSemantics.Goals, TemplateSemanticsReader.Read(versions[1].Sections).Keys);

        var resolved = await new TemplateResolutionService(verify, NullLogger<TemplateResolutionService>.Instance).ResolveAsync(null, IepTypeId);
        Assert.Equal(versions[1].Id, resolved.Data!.DocumentTemplateVersionId); // new documents pick v2
    }

    [Fact]
    public async Task DefaultSeed_NeverSupersedesAnAdminCustomizedDefault()
    {
        // Admin forked v1 into a customized Published v2 (different keys, no semantics) — the seeder must
        // not publish a stock v3 over it, and must not touch a template that has an in-progress draft.
        var (templateId, _) = SeedPristineLegacyDefault();
        using (var ctx = CreateContext())
        {
            var v2 = new DocumentTemplateVersion { DocumentTemplateId = templateId, VersionNumber = 2, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
            ctx.DocumentTemplateVersions.Add(v2);
            ctx.SaveChanges();
            ctx.TemplateSections.Add(new TemplateSection
            {
                DocumentTemplateVersionId = v2.Id, SectionKey = Guid.NewGuid(), Title = "District section", DisplayOrder = 0,
                Fields = { new TemplateField { DocumentTemplateVersionId = v2.Id, FieldKey = Guid.NewGuid(), FieldType = FieldType.RichText, Label = "Custom", DisplayOrder = 0 } }
            });
            ctx.SaveChanges();
        }

        using (var ctx = CreateContext())
        {
            var result = await new DefaultIepTemplateSeeder(ctx, NullLogger<DefaultIepTemplateSeeder>.Instance).SeedAsync();
            Assert.Equal(DefaultIepTemplateSeedOutcome.AlreadySeeded, result.Outcome);
        }

        using var verify = CreateContext();
        Assert.Equal(2, verify.DocumentTemplateVersions.Count(v => v.DocumentTemplateId == templateId));
        var resolved = await new TemplateResolutionService(verify, NullLogger<TemplateResolutionService>.Instance).ResolveAsync(null, IepTypeId);
        Assert.Equal(2, resolved.Data!.VersionNumber); // admin's v2 still wins
    }

    [Fact]
    public async Task DefaultSeed_LeavesPristineTemplateAlone_WhenAnAdminDraftExists()
    {
        var (templateId, _) = SeedPristineLegacyDefault();
        using (var ctx = CreateContext())
        {
            ctx.DocumentTemplateVersions.Add(new DocumentTemplateVersion { DocumentTemplateId = templateId, VersionNumber = 2, Status = TemplateVersionStatus.Draft });
            ctx.SaveChanges();
        }
        using (var ctx = CreateContext())
            Assert.Equal(DefaultIepTemplateSeedOutcome.AlreadySeeded, (await new DefaultIepTemplateSeeder(ctx, NullLogger<DefaultIepTemplateSeeder>.Instance).SeedAsync()).Outcome);
        using var verify = CreateContext();
        Assert.Equal(2, verify.DocumentTemplateVersions.Count(v => v.DocumentTemplateId == templateId)); // nothing added
    }

    [Fact]
    public async Task SaveValues_RowIds_FollowRowsThroughReorder_AndIgnoreNonStringOrSoleIds()
    {
        var s = SeedScenario("reorder");
        int instanceId;
        using (var ctx = CreateContext())
        {
            var created = await CreateInstanceService(ctx).CreateAsync(s.StudentId, IepTypeId, s.UserId);
            instanceId = created.Data!.Id;
            Assert.True((await CreateInstanceService(ctx).SaveValuesAsync(instanceId,
                Patch($$"""
                { "{{s.TableKey}}": [ { "{{s.GoalCol}}": "A" }, { "{{s.GoalCol}}": "B" } ] }
                """), null, s.UserId)).Success);
        }
        string idA, idB;
        using (var ctx = CreateContext())
        {
            var t = ReadTable(ctx, instanceId, s.TableKey);
            idA = t[0].GetProperty(RowMetaKeys.RowId).GetString()!; idB = t[1].GetProperty(RowMetaKeys.RowId).GetString()!;
        }
        using (var ctx = CreateContext())
        {
            // Reorder B before A, send a numeric junk id and a row with only an id.
            var r = await CreateInstanceService(ctx).SaveValuesAsync(instanceId,
                Patch($$"""
                { "{{s.TableKey}}": [
                    { "_rowId": "{{idB}}", "{{s.GoalCol}}": "B" },
                    { "_rowId": "{{idA}}", "{{s.GoalCol}}": "A" },
                    { "_rowId": 12345, "{{s.GoalCol}}": "C" },
                    { "_rowId": "{{Guid.NewGuid()}}" } ] }
                """), null, s.UserId);
            Assert.True(r.Success, r.Message);
        }
        using (var ctx = CreateContext())
        {
            var t = ReadTable(ctx, instanceId, s.TableKey);
            Assert.Equal(3, t.GetArrayLength()); // sole-id row dropped
            Assert.Equal(idB, t[0].GetProperty(RowMetaKeys.RowId).GetString()); // ids follow rows, not positions
            Assert.Equal(idA, t[1].GetProperty(RowMetaKeys.RowId).GetString());
            Assert.True(Guid.TryParse(t[2].GetProperty(RowMetaKeys.RowId).GetString(), out var fresh) && fresh != Guid.Empty); // numeric id replaced
        }
    }

    public void Dispose() => _connection.Dispose();
}
