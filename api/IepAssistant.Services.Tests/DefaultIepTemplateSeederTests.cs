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
/// Phase 5 coverage for the default IEP template seed: it creates a Published, state-less IEP template
/// reproducing the legacy typed structure (six narrative RichText sections + Goals/Services/
/// Accommodations/Transition tables), is idempotent, makes an IEP resolve/create for any state, and does
/// NOT let Section504/ETR (which have no template) borrow the IEP default (they stay blocked at create).
/// Real SQLite in-memory engine, same pattern as the other Phase 3-5 service tests. The IEP/Section504/ETR
/// document-type rows are HasData-seeded, so EnsureCreated materializes them.
/// </summary>
public sealed class DefaultIepTemplateSeederTests : IDisposable
{
    private const int IepTypeId = 1;
    private const int Section504TypeId = 2;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DefaultIepTemplateSeederTests()
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

    private DefaultIepTemplateSeeder CreateSeeder(ApplicationDbContext ctx)
        => new(ctx, NullLogger<DefaultIepTemplateSeeder>.Instance);

    private TemplateResolutionService CreateResolution(ApplicationDbContext ctx)
        => new(ctx, NullLogger<TemplateResolutionService>.Instance);

    private async Task<DefaultIepTemplateSeedResult> SeedOnceAsync()
    {
        using var ctx = CreateContext();
        return await CreateSeeder(ctx).SeedAsync();
    }

    // ---------------------------------------------------------------- Seeded structure

    [Fact]
    public async Task Seed_CreatesPublishedStatelessIepTemplate()
    {
        var result = await SeedOnceAsync();

        Assert.Equal(DefaultIepTemplateSeedOutcome.Created, result.Outcome);

        using var ctx = CreateContext();
        var template = Assert.Single(ctx.DocumentTemplates.Where(t => t.DocumentTypeId == IepTypeId).ToList());
        Assert.Null(template.StateCode); // default (state-less) template
        Assert.Equal(DefaultIepTemplateSeeder.DefaultTemplateName, template.Name);

        var version = Assert.Single(ctx.DocumentTemplateVersions
            .Where(v => v.DocumentTemplateId == template.Id).ToList());
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(TemplateVersionStatus.Published, version.Status);
        Assert.NotNull(version.PublishedAt);
        Assert.Equal(version.Id, result.DocumentTemplateVersionId);
    }

    [Fact]
    public async Task Seed_CreatesExpectedSectionsInPdfOrder()
    {
        await SeedOnceAsync();

        using var ctx = CreateContext();
        var versionId = ctx.DocumentTemplateVersions.Single().Id;
        var sections = ctx.TemplateSections.AsNoTracking()
            .Where(s => s.DocumentTemplateVersionId == versionId)
            .OrderBy(s => s.DisplayOrder)
            .ToList();

        // Narrative sections first (mirroring IepSectionKind order), then the four table sections.
        Assert.Equal(
            new[]
            {
                "Student Profile", "Present Levels", "Eligibility", "Placement",
                "Progress Monitoring", "Special Factors", "Goals", "Services",
                "Accommodations", "Transition"
            },
            sections.Select(s => s.Title).ToArray());

        // DisplayOrder is contiguous 0..9 and each SectionKey is a stable non-empty GUID.
        Assert.Equal(Enumerable.Range(0, 10).ToArray(), sections.Select(s => s.DisplayOrder).ToArray());
        Assert.DoesNotContain(sections, s => s.SectionKey == Guid.Empty);
        Assert.Equal(10, sections.Select(s => s.SectionKey).Distinct().Count());
    }

    [Fact]
    public async Task Seed_NarrativeSections_HaveSingleRichTextField()
    {
        await SeedOnceAsync();

        using var ctx = CreateContext();
        var versionId = ctx.DocumentTemplateVersions.Single().Id;

        foreach (var title in new[] { "Student Profile", "Present Levels", "Eligibility", "Placement", "Progress Monitoring", "Special Factors" })
        {
            var section = ctx.TemplateSections.AsNoTracking()
                .Include(s => s.Fields)
                .Single(s => s.DocumentTemplateVersionId == versionId && s.Title == title);

            var field = Assert.Single(section.Fields);
            Assert.Equal(FieldType.RichText, field.FieldType);
            Assert.False(field.Required); // freeform parity
            Assert.NotEqual(Guid.Empty, field.FieldKey);
            Assert.Equal(versionId, field.DocumentTemplateVersionId); // denormalized FK is populated
        }
    }

    [Fact]
    public async Task Seed_GoalsTable_HasExpectedColumns()
    {
        await SeedOnceAsync();
        var columns = LoadTableColumns("Goals");

        Assert.Equal(
            new[] { "Domain", "Goal", "Baseline", "Target Criteria", "Measurement Method", "Timeframe" },
            columns.Select(c => c.Label).ToArray());
        Assert.All(columns, c => Assert.Equal(FieldType.Text, c.Type));
        Assert.All(columns, c => Assert.NotEqual(Guid.Empty, c.ColumnKey));
    }

    [Fact]
    public async Task Seed_ServicesTable_HasExpectedColumnsAndDateTypes()
    {
        await SeedOnceAsync();
        var columns = LoadTableColumns("Services");

        Assert.Equal(
            new[] { "Service Type", "Frequency", "Duration", "Location", "Provider Role", "Start Date", "End Date" },
            columns.Select(c => c.Label).ToArray());
        Assert.Equal(FieldType.Date, columns.Single(c => c.Label == "Start Date").Type);
        Assert.Equal(FieldType.Date, columns.Single(c => c.Label == "End Date").Type);
        Assert.Equal(FieldType.Text, columns.Single(c => c.Label == "Service Type").Type);
    }

    [Fact]
    public async Task Seed_AccommodationsAndTransitionTables_HaveExpectedColumns()
    {
        await SeedOnceAsync();

        Assert.Equal(
            new[] { "Category", "Accommodation" },
            LoadTableColumns("Accommodations").Select(c => c.Label).ToArray());

        Assert.Equal(
            new[] { "Postsecondary Goal Area", "Services" },
            LoadTableColumns("Transition").Select(c => c.Label).ToArray());
    }

    [Fact]
    public async Task Seed_TableConfig_IsValidPerConfigValidator()
    {
        await SeedOnceAsync();

        using var ctx = CreateContext();
        var tableFields = ctx.TemplateFields.AsNoTracking()
            .Where(f => f.FieldType == FieldType.Table)
            .ToList();

        Assert.Equal(4, tableFields.Count); // Goals, Services, Accommodations, Transition
        foreach (var field in tableFields)
            Assert.Null(TemplateFieldConfigValidator.Validate(FieldType.Table, field.ConfigJson));
    }

    // ---------------------------------------------------------------- Idempotency

    [Fact]
    public async Task Seed_IsIdempotent_SecondRunCreatesNothing()
    {
        var first = await SeedOnceAsync();
        var second = await SeedOnceAsync();

        Assert.Equal(DefaultIepTemplateSeedOutcome.Created, first.Outcome);
        Assert.Equal(DefaultIepTemplateSeedOutcome.AlreadySeeded, second.Outcome);

        using var ctx = CreateContext();
        Assert.Single(ctx.DocumentTemplates.Where(t => t.DocumentTypeId == IepTypeId).ToList());
        Assert.Single(ctx.DocumentTemplateVersions.ToList());
        Assert.Equal(10, ctx.TemplateSections.Count());
        Assert.Equal(10, ctx.TemplateFields.Count());
    }

    // ---------------------------------------------------------------- Resolution after seeding

    [Fact]
    public async Task Resolution_AfterSeed_ReturnsDefaultForIep_AnyState()
    {
        await SeedOnceAsync();

        using var ctx = CreateContext();
        var resolution = CreateResolution(ctx);

        // Unknown state falls back to the seeded default.
        var tx = await resolution.ResolveAsync("TX", IepTypeId);
        Assert.True(tx.Success, tx.Message);
        Assert.True(tx.Data!.UsedDefault);

        // Null/blank state uses the default too.
        var none = await resolution.ResolveAsync(null, IepTypeId);
        Assert.True(none.Success, none.Message);
        Assert.True(none.Data!.UsedDefault);
    }

    [Fact]
    public async Task Resolution_AfterSeed_Section504AndEtr_StayBlocked()
    {
        await SeedOnceAsync();

        using var ctx = CreateContext();
        var resolution = CreateResolution(ctx);

        // The seeded IEP default must NOT be borrowed by other document types.
        var s504 = await resolution.ResolveAsync("OH", Section504TypeId);
        Assert.False(s504.Success);
        Assert.Contains("no document template", s504.Message!, StringComparison.OrdinalIgnoreCase);

        var etr = await resolution.ResolveAsync(null, EtrTypeId);
        Assert.False(etr.Success);
        Assert.Contains("no document template", etr.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_AfterSeed_IepInstanceSucceeds_AndPinsSeededVersion()
    {
        await SeedOnceAsync();

        int seededVersionId;
        using (var ctx = CreateContext())
            seededVersionId = ctx.DocumentTemplateVersions.Single().Id;

        var scenario = SeedSchoolWithStudent("seed-create", studentState: "CA"); // no CA template → default

        using var ctx2 = CreateContext();
        var service = new DocumentInstanceService(
            ctx2,
            new OrgAccessService(ctx2),
            CreateResolution(ctx2),
            new TemplateAuthoringService(ctx2, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance),
            new CapturingAuditLogger(),
            NullLogger<DocumentInstanceService>.Instance);

        var result = await service.CreateAsync(scenario.StudentId, IepTypeId, scenario.CollaboratorUserId);

        Assert.True(result.Success, result.Message);
        Assert.Equal(seededVersionId, result.Data!.DocumentTemplateVersionId);
        Assert.Equal(10, result.Data.TemplateVersion.Sections.Count);
    }

    // ---------------------------------------------------------------- Helpers

    private sealed record ColumnInfo(Guid ColumnKey, FieldType Type, string Label);

    private List<ColumnInfo> LoadTableColumns(string sectionTitle)
    {
        using var ctx = CreateContext();
        var versionId = ctx.DocumentTemplateVersions.Single().Id;
        var section = ctx.TemplateSections.AsNoTracking()
            .Include(s => s.Fields)
            .Single(s => s.DocumentTemplateVersionId == versionId && s.Title == sectionTitle);

        var field = Assert.Single(section.Fields);
        Assert.Equal(FieldType.Table, field.FieldType);

        var config = JsonSerializer.Deserialize<TableFieldConfig>(field.ConfigJson!, TemplateFieldConfigValidator.JsonOptions)!;
        return config.Columns.Select(c => new ColumnInfo(c.ColumnKey, c.Type, c.Label)).ToList();
    }

    private sealed record SchoolScenario(int SchoolId, int CollaboratorUserId, int StudentId);

    private SchoolScenario SeedSchoolWithStudent(string prefix, string? studentState = null)
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
            Role = AccessRole.Collaborator,
            IsActive = true
        });
        ctx.SaveChanges();

        return new SchoolScenario(school.Id, user.Id, student.Id);
    }

    public void Dispose() => _connection.Dispose();
}
