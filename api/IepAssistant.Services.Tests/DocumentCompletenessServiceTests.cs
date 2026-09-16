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
/// Plan 5, deliverable B: server-side completeness math mirroring
/// <c>web/src/features/document-authoring/lib/completeness.ts</c>'s blank/required rules, collapsed to
/// percent/filledCount/totalCount/requiredMissing (no itemized advisory messages — that stays client-only).
/// Test cases 1-4 below are the server-shape equivalents of the web test file's cases; case 5 covers the
/// server-only "required Table column" counting the web's advisory-only semantic checks don't need.
/// </summary>
public sealed class DocumentCompletenessServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public DocumentCompletenessServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private DocumentCompletenessService CreateService(ApplicationDbContext ctx)
        => new(ctx, new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance));

    // ----------------------------------------------------------------- Fixtures

    private static readonly Guid PlaafpKey = Guid.Parse("f2222222-2222-2222-2222-222222222222");
    private static readonly Guid GoalsKey = Guid.Parse("f1111111-1111-1111-1111-111111111111");
    private static readonly Guid GoalCol = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    private static readonly Guid BaseCol = Guid.Parse("c2222222-2222-2222-2222-222222222222");
    private static readonly Guid MeasCol = Guid.Parse("c3333333-3333-3333-3333-333333333333");
    private static readonly Guid ServicesKey = Guid.Parse("f3333333-3333-3333-3333-333333333333");
    private static readonly Guid SvcTypeCol = Guid.Parse("c4444444-4444-4444-4444-444444444444");
    private static readonly Guid SvcFreqCol = Guid.Parse("c5555555-5555-5555-5555-555555555555");

    /// <summary>Profile (PLAAFP, required RichText) + Goals (optional Table, no required columns) — mirrors the web test's <c>template</c>.</summary>
    private static List<TemplateSectionModel> ProfileAndGoalsSections(bool includeGoalsSection = true)
    {
        var sections = new List<TemplateSectionModel>
        {
            new()
            {
                Id = 10, Title = "Profile", DisplayOrder = 0,
                Fields = new List<TemplateFieldModel>
                {
                    new() { Id = 100, FieldKey = PlaafpKey, FieldType = FieldType.RichText, Label = "Present Levels", Required = true, DisplayOrder = 0 }
                }
            }
        };
        if (includeGoalsSection)
        {
            sections.Add(new TemplateSectionModel
            {
                Id = 20, Title = "Goals", DisplayOrder = 1,
                Fields = new List<TemplateFieldModel>
                {
                    new()
                    {
                        Id = 200, FieldKey = GoalsKey, FieldType = FieldType.Table, Label = "Goals", Required = false, DisplayOrder = 0,
                        ConfigJson = JsonSerializer.Serialize(new
                        {
                            columns = new object[]
                            {
                                new { columnKey = GoalCol, type = "Text", label = "Goal", required = false },
                                new { columnKey = BaseCol, type = "Text", label = "Baseline", required = false },
                                new { columnKey = MeasCol, type = "Text", label = "Measure", required = false }
                            }
                        })
                    }
                }
            });
        }
        return sections;
    }

    /// <summary>A required Services table whose columns are NOT marked required — mirrors the web test's <c>servicesTemplate</c>.</summary>
    private static List<TemplateSectionModel> ServicesSections(bool freqColumnRequired = false)
        => new()
        {
            new TemplateSectionModel
            {
                Id = 30, Title = "Services", DisplayOrder = 0,
                Fields = new List<TemplateFieldModel>
                {
                    new()
                    {
                        Id = 300, FieldKey = ServicesKey, FieldType = FieldType.Table, Label = "Services", Required = true, DisplayOrder = 0,
                        ConfigJson = JsonSerializer.Serialize(new
                        {
                            columns = new object[]
                            {
                                new { columnKey = SvcTypeCol, type = "Text", label = "Service", required = false },
                                new { columnKey = SvcFreqCol, type = "Text", label = "Frequency", required = freqColumnRequired }
                            }
                        })
                    }
                }
            }
        };

    private static string Json(object o) => JsonSerializer.Serialize(o);

    // ----------------------------------------------------------------- Case 1: required empty table / non-required column left blank

    [Fact]
    public void Compute_RequiredEmptyTable_CountsOneRequiredMissing_ZeroPercent()
    {
        using var ctx = CreateContext();
        var result = CreateService(ctx).Compute(ServicesSections(), Json(new Dictionary<string, object> { [ServicesKey.ToString()] = Array.Empty<object>() }));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(0, result.FilledCount);
        Assert.Equal(1, result.RequiredMissing); // the required Table field itself is blank (no rows)
        Assert.Equal(0, result.Percent);
    }

    [Fact]
    public void Compute_RequiredTableWithOneRow_FieldNoLongerBlank_NonRequiredColumnDoesNotCount()
    {
        using var ctx = CreateContext();
        var values = Json(new Dictionary<string, object>
        {
            [ServicesKey.ToString()] = new object[] { new Dictionary<string, object?> { ["_rowId"] = "r", [SvcTypeCol.ToString()] = "Speech", [SvcFreqCol.ToString()] = "" } }
        });
        var result = CreateService(ctx).Compute(ServicesSections(freqColumnRequired: false), values);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.FilledCount); // Table: filled once it has any row
        Assert.Equal(0, result.RequiredMissing); // frequency column isn't marked required
        Assert.Equal(100, result.Percent);
    }

    // ----------------------------------------------------------------- Case 2: required blank + 50% partial fill

    [Fact]
    public void Compute_RequiredFieldBlank_PlusPartiallyFilledTable_Gives50Percent()
    {
        using var ctx = CreateContext();
        var values = Json(new Dictionary<string, object>
        {
            [GoalsKey.ToString()] = new object[]
            {
                new Dictionary<string, object?> { ["_rowId"] = "r1", [GoalCol.ToString()] = "Read 90 wpm", [BaseCol.ToString()] = "", [MeasCol.ToString()] = "CBM" }
            }
        });
        var result = CreateService(ctx).Compute(ProfileAndGoalsSections(), values);

        Assert.Equal(2, result.TotalCount); // PLAAFP + Goals
        Assert.Equal(1, result.FilledCount); // Goals has a row; PLAAFP is missing
        Assert.Equal(1, result.RequiredMissing); // PLAAFP is required and blank
        Assert.Equal(50, result.Percent);
    }

    // ----------------------------------------------------------------- Case 3: HTML-only rich text is blank

    [Fact]
    public void Compute_HtmlOnlyRichText_TreatedAsBlank_ZeroPercent()
    {
        using var ctx = CreateContext();
        var values = Json(new Dictionary<string, object> { [PlaafpKey.ToString()] = "<p></p>", [GoalsKey.ToString()] = Array.Empty<object>() });
        var result = CreateService(ctx).Compute(ProfileAndGoalsSections(), values);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(0, result.FilledCount);
        Assert.Equal(1, result.RequiredMissing);
        Assert.Equal(0, result.Percent);
    }

    // ----------------------------------------------------------------- Case 4: fully filled -> 100%, nothing missing

    [Fact]
    public void Compute_EverythingFilled_Gives100Percent_NoRequiredMissing()
    {
        using var ctx = CreateContext();
        var values = Json(new Dictionary<string, object> { [PlaafpKey.ToString()] = "<p>Reads at 42 wpm.</p>" });
        var result = CreateService(ctx).Compute(ProfileAndGoalsSections(includeGoalsSection: false), values);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.FilledCount);
        Assert.Equal(0, result.RequiredMissing);
        Assert.Equal(100, result.Percent);
    }

    // ----------------------------------------------------------------- Case 5: required Table COLUMN, per-row (server-only; the web's advisory rules don't gate on column.required)

    [Fact]
    public void Compute_RequiredTableColumn_CountsOnePerRowThatLeavesItBlank()
    {
        using var ctx = CreateContext();
        var values = Json(new Dictionary<string, object>
        {
            [ServicesKey.ToString()] = new object[]
            {
                new Dictionary<string, object?> { ["_rowId"] = "r1", [SvcTypeCol.ToString()] = "Speech", [SvcFreqCol.ToString()] = "" },
                new Dictionary<string, object?> { ["_rowId"] = "r2", [SvcTypeCol.ToString()] = "OT", [SvcFreqCol.ToString()] = "2x/week" },
                new Dictionary<string, object?> { ["_rowId"] = "r3", [SvcTypeCol.ToString()] = "PT" } // frequency column entirely absent
            }
        });
        var result = CreateService(ctx).Compute(ServicesSections(freqColumnRequired: true), values);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.FilledCount); // the field itself has rows -> filled
        // Field-level required-missing: 0 (the field is non-blank); per-row required-column-missing: rows 1 and 3 (2).
        Assert.Equal(2, result.RequiredMissing);
    }

    // ----------------------------------------------------------------- ComputeAsync (DB-backed)

    [Fact]
    public async Task ComputeAsync_LoadsInstanceAndPinnedVersion_MatchesDirectCompute()
    {
        using var ctx = CreateContext();

        var district = new District { Name = "D" };
        ctx.Districts.Add(district);
        ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = "S" };
        ctx.Schools.Add(school);
        ctx.SaveChanges();
        var student = new SchoolStudent { SchoolId = school.Id, DistrictId = district.Id, FirstName = "Sam", IsActive = true };
        ctx.SchoolStudents.Add(student);
        ctx.SaveChanges();

        var version = new DocumentTemplateVersion { VersionNumber = 1, Status = TemplateVersionStatus.Published, PublishedAt = DateTime.UtcNow };
        var template = new DocumentTemplate { DocumentTypeId = 1, Name = "T", Versions = { version } };
        ctx.DocumentTemplates.Add(template);
        ctx.SaveChanges();

        ctx.TemplateSections.Add(new TemplateSection
        {
            DocumentTemplateVersionId = version.Id,
            SectionKey = Guid.NewGuid(),
            Title = "Profile",
            DisplayOrder = 0,
            Fields = { new TemplateField { DocumentTemplateVersionId = version.Id, FieldKey = PlaafpKey, FieldType = FieldType.RichText, Label = "Present Levels", Required = true, DisplayOrder = 0 } }
        });
        ctx.SaveChanges();

        var valuesJson = Json(new Dictionary<string, object> { [PlaafpKey.ToString()] = "<p>Reads at 42 wpm.</p>" });
        var instance = new DocumentInstance
        {
            SchoolStudentId = student.Id,
            DocumentTypeId = 1,
            DocumentTemplateVersionId = version.Id,
            Status = DocumentInstanceStatus.Draft,
            ValuesJson = valuesJson,
            RowVersion = Guid.NewGuid().ToByteArray()
        };
        ctx.DocumentInstances.Add(instance);
        ctx.SaveChanges();

        var direct = CreateService(ctx).Compute(ProfileAndGoalsSections(includeGoalsSection: false), valuesJson);

        using var readCtx = CreateContext();
        var result = await CreateService(readCtx).ComputeAsync(instance.Id);

        Assert.True(result.Success, result.Message);
        Assert.Equal(direct.Percent, result.Data!.Percent);
        Assert.Equal(direct.FilledCount, result.Data.FilledCount);
        Assert.Equal(direct.TotalCount, result.Data.TotalCount);
        Assert.Equal(direct.RequiredMissing, result.Data.RequiredMissing);
    }

    [Fact]
    public async Task ComputeAsync_UnknownInstance_ReturnsFailure()
    {
        using var ctx = CreateContext();
        var result = await CreateService(ctx).ComputeAsync(999);
        Assert.False(result.Success);
    }

    public void Dispose() => _connection.Dispose();
}
