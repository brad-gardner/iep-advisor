using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Plan 2026-09-15-002: the evidence bundle is role-filtered (identity + team, latest finalized version
/// per type, shareable student voice only, shared family notes only, strangers denied); a new IEP is
/// prefilled from it (identity, present levels with provenance, goals carried with the SAME lineage id
/// and a `_carriedFrom` stamp); a student with no history gets identity only; family notes are
/// parent-managed and only shared ones reach staff; CoerceTable preserves the provenance metadata.
/// </summary>
public sealed class StudentEvidenceAndPrefillTests : IDisposable
{
    private const int IepTypeId = 1;
    private const int EtrTypeId = 3;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly CapturingAuditLogger _audit = new();

    public StudentEvidenceAndPrefillTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    private ApplicationDbContext CreateContext() => new(_options);

    private sealed class NoClaude : IClaudeClient
    {
        public Task<string?> CompleteAsync(ClaudeCompletionRequest request, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private (StudentEvidenceService Evidence, DocumentPrefillService Prefill, ParentContributionService Contributions, DocumentInstanceService Instances) Services(ApplicationDbContext ctx)
    {
        var access = new AccessService(ctx);
        var org = new OrgAccessService(ctx);
        var workspace = new StudentWorkspaceService(ctx, access, org, new NoClaude(), NullLogger<StudentWorkspaceService>.Instance);
        var contributions = new ParentContributionService(ctx, access, org, _audit);
        var evidence = new StudentEvidenceService(ctx, org, workspace, contributions, _audit);
        var prefill = new DocumentPrefillService(ctx);
        var instances = new DocumentInstanceService(ctx, org,
            new TemplateResolutionService(ctx, NullLogger<TemplateResolutionService>.Instance),
            new TemplateAuthoringService(ctx, new CapturingAuditLogger(), NullLogger<TemplateAuthoringService>.Instance),
            new CapturingAuditLogger(), NullLogger<DocumentInstanceService>.Instance, evidence, prefill);
        return (evidence, prefill, contributions, instances);
    }

    private sealed record Scenario(int StudentId, int TeacherId, int StrangerId, int ParentId, int ChildId, int StudentUserId, Guid GoalRowId, int IepVersionId);

    /// <summary>District/school/teacher/student + OH catalog templates + a finalized OH IEP with one goal,
    /// a finalized ETR, a linked parent with one shared + one private note, and a student account with one
    /// shareable + one private entry.</summary>
    private async Task<Scenario> SeedAsync(bool withHistory = true)
    {
        using var ctx = CreateContext();
        await new TemplateCatalogSeeder(ctx, NullLogger<TemplateCatalogSeeder>.Instance).SeedAsync();

        var teacher = new User { Email = "t@example.com", PasswordHash = "x", FirstName = "Steph", LastName = "Case", Role = UserRole.Educator };
        var stranger = new User { Email = "s@example.com", PasswordHash = "x", FirstName = "No", LastName = "Access", Role = UserRole.Educator };
        var parent = new User { Email = "p@example.com", PasswordHash = "x", FirstName = "Dana", LastName = "Parent", Role = UserRole.Parent };
        var studentUser = new User { Email = "kid@example.com", PasswordHash = "x", FirstName = "Jordan", LastName = "Ellis", Role = UserRole.Student };
        ctx.Users.AddRange(teacher, stranger, parent, studentUser); ctx.SaveChanges();
        var district = new District { Name = "OH district", StateCode = "OH" }; ctx.Districts.Add(district); ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = "Maple Ridge" }; ctx.Schools.Add(school); ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = teacher.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher, Title = "Intervention Specialist" });
        ctx.StaffProfiles.Add(new StaffProfile { UserId = stranger.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, FirstName = "Jordan", LastName = "Ellis", GradeLevel = "7", DisabilityCategory = "Specific Learning Disability", DateOfBirth = new DateTime(2013, 4, 2) };
        ctx.SchoolStudents.Add(student); ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = teacher.Id, Role = AccessRole.Collaborator, IsActive = true });

        // Parent + child + accepted link
        var child = new ChildProfile { UserId = parent.Id, FirstName = "Jordan" }; ctx.ChildProfiles.Add(child); ctx.SaveChanges();
        ctx.ChildAccesses.Add(new ChildAccess { ChildProfileId = child.Id, UserId = parent.Id, Role = AccessRole.Owner, IsActive = true, AcceptedAt = DateTime.UtcNow });
        ctx.ChildLinks.Add(new ChildLink { ChildProfileId = child.Id, SchoolStudentId = student.Id, IsActive = true, AcceptedAt = DateTime.UtcNow, LinkedAt = DateTime.UtcNow });
        ctx.ParentContributions.Add(new ParentContribution { ChildProfileId = child.Id, Kind = ParentContributionKind.WorksAtHome, Text = "Reads aloud to his sister every night", IsShared = true });
        ctx.ParentContributions.Add(new ParentContribution { ChildProfileId = child.Id, Kind = ParentContributionKind.Concern, Text = "PRIVATE worry about the teacher", IsShared = false });

        // Student account + workspace
        ctx.StudentProfiles.Add(new StudentProfile { UserId = studentUser.Id, SchoolStudentId = student.Id, ChildProfileId = child.Id });
        var ws = new StudentWorkspace { UserId = studentUser.Id }; ctx.StudentWorkspaces.Add(ws); ctx.SaveChanges();
        ctx.StudentWorkspaceEntries.Add(new StudentWorkspaceEntry { StudentWorkspaceId = ws.Id, EntryKind = StudentEntryKind.Strength, Content = "I like graphic novels", IsShareable = true, DisplayOrder = 0 });
        ctx.StudentWorkspaceEntries.Add(new StudentWorkspaceEntry { StudentWorkspaceId = ws.Id, EntryKind = StudentEntryKind.MeetingStatement, Content = "PRIVATE thought", IsShareable = false, DisplayOrder = 1 });
        ctx.SaveChanges();

        var goalRowId = Guid.NewGuid();
        var iepVersionId = 0;
        if (withHistory)
        {
            // Finalized OH IEP v1 with present levels + one goal; finalized OH ETR v1 with a team summary.
            var ohIep = await ctx.DocumentTemplates.Include(t => t.Versions).ThenInclude(v => v.Sections).ThenInclude(s => s.Fields)
                .SingleAsync(t => t.StateCode == "OH" && t.DocumentTypeId == IepTypeId);
            var iepTv = ohIep.Versions.Single();
            var sem = TemplateSemanticsReader.Read(iepTv.Sections);
            var goals = sem[FieldSemantics.Goals];
            var values = new JsonObject
            {
                [sem[FieldSemantics.PresentLevels].FieldKey.ToString()] = "Reads 42 wpm on grade-level passages (Sept 2025).",
                [goals.FieldKey.ToString()] = new JsonArray(new JsonObject
                {
                    ["_rowId"] = goalRowId.ToString(),
                    [goals.Columns[ColumnSemantics.Domain].ToString()] = "Reading fluency",
                    [goals.Columns[ColumnSemantics.GoalText].ToString()] = "Read 70 wpm by May",
                    [goals.Columns[ColumnSemantics.Baseline].ToString()] = "42 wpm",
                })
            };
            var iepVersion = new AuthoredDocumentVersion
            {
                SchoolStudentId = student.Id, DocumentTypeId = IepTypeId, DocumentTemplateVersionId = iepTv.Id, VersionNumber = 1,
                ValuesJson = values.ToJsonString(), FinalizedByUserId = teacher.Id, FinalizedAt = new DateTime(2025, 10, 14)
            };
            ctx.AuthoredDocumentVersions.Add(iepVersion);

            var ohEtr = await ctx.DocumentTemplates.Include(t => t.Versions).ThenInclude(v => v.Sections).ThenInclude(s => s.Fields)
                .SingleAsync(t => t.StateCode == "OH" && t.DocumentTypeId == EtrTypeId);
            var etrTv = ohEtr.Versions.Single();
            var etrSem = TemplateSemanticsReader.Read(etrTv.Sections);
            var etrValues = new JsonObject { [etrSem[FieldSemantics.TeamSummary].FieldKey.ToString()] = "WISC-V FSIQ 98; CBM reading 38 wpm (Mar 2024)." };
            ctx.AuthoredDocumentVersions.Add(new AuthoredDocumentVersion
            {
                SchoolStudentId = student.Id, DocumentTypeId = EtrTypeId, DocumentTemplateVersionId = etrTv.Id, VersionNumber = 1,
                ValuesJson = etrValues.ToJsonString(), FinalizedByUserId = teacher.Id, FinalizedAt = new DateTime(2024, 3, 2)
            });
            ctx.SaveChanges();
            iepVersionId = iepVersion.Id;
        }
        return new Scenario(student.Id, teacher.Id, stranger.Id, parent.Id, child.Id, studentUser.Id, goalRowId, iepVersionId);
    }

    // ---------------------------------------------------------------- Evidence bundle

    [Fact]
    public async Task Evidence_IsRoleFiltered_AndCoversEverySource()
    {
        var s = await SeedAsync();
        using var ctx = CreateContext();
        var (evidence, _, _, _) = Services(ctx);
        var result = await evidence.BuildForStaffAsync(s.TeacherId, s.StudentId);
        Assert.True(result.Success, result.Message);
        var items = result.Data!.Items;

        Assert.Contains(items, i => i.Kind == EvidenceKind.Identity && i.Text.Contains("Jordan Ellis") && i.Text.Contains("Grade: 7"));
        Assert.Contains(items, i => i.Kind == EvidenceKind.TeamMember && i.Text.Contains("Steph Case") && i.Text.Contains("Intervention Specialist"));
        Assert.Contains(items, i => i.Kind == EvidenceKind.PresentLevels && i.Text.Contains("42 wpm") && i.SourceLabel.StartsWith("IEP v1"));
        var goal = Assert.Single(items, i => i.Kind == EvidenceKind.PriorGoal);
        Assert.Equal(s.GoalRowId.ToString(), goal.RowId);
        Assert.Equal("42 wpm", goal.Fields![ColumnSemantics.Baseline]);
        Assert.Contains(items, i => i.Kind == EvidenceKind.EtrFinding && i.Text.Contains("WISC-V"));
        Assert.Contains(items, i => i.Kind == EvidenceKind.StudentVoice && i.Text == "I like graphic novels");
        Assert.DoesNotContain(items, i => i.Text.Contains("PRIVATE"));
        Assert.Contains(items, i => i.Kind == EvidenceKind.ParentContribution && i.Text.Contains("reads aloud", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(2, result.Data.Sources.Count);

        // The aggregate read leaves the same trail as reading each finalized version directly.
        Assert.Contains(_audit.Entries, e => e.Action == AuditAction.View && e.ResourceType == "StudentEvidence" && e.ResourceId == s.StudentId && e.ActorUserId == s.TeacherId);
        Assert.Contains(_audit.Entries, e => e.Action == AuditAction.View && e.ResourceType == "AuthoredDocumentVersion");
        Assert.Contains(_audit.Entries, e => e.Action == AuditAction.View && e.ResourceType == "ParentContributions" && e.ResourceId == s.StudentId);

        var denied = await evidence.BuildForStaffAsync(s.StrangerId, s.StudentId);
        Assert.False(denied.Success);
        Assert.DoesNotContain(_audit.Entries, e => e.ActorUserId == s.StrangerId);
    }

    [Fact]
    public async Task OrgAccess_MemoizesStudentDecision_WithinOneScope()
    {
        var s = await SeedAsync(withHistory: false);
        using var ctx = CreateContext();
        var org = new OrgAccessService(ctx);
        Assert.True(await org.CanActOnStudentAsync(s.TeacherId, s.StudentId, AccessRole.Viewer));
        // Revoking access mid-scope is not observed by the same scoped instance (a request is atomic
        // with respect to its own authz), but a fresh scope sees the change.
        using (var mutate = CreateContext())
        {
            foreach (var a in mutate.SchoolStudentAccesses.Where(a => a.UserId == s.TeacherId)) a.IsActive = false;
            mutate.SaveChanges();
        }
        Assert.True(await org.CanActOnStudentAsync(s.TeacherId, s.StudentId, AccessRole.Viewer));
        using var fresh = CreateContext();
        Assert.False(await new OrgAccessService(fresh).CanActOnStudentAsync(s.TeacherId, s.StudentId, AccessRole.Viewer));
    }

    [Fact]
    public async Task Evidence_ExcludesFamilyNotes_WhenTheLinkIsRevoked()
    {
        var s = await SeedAsync();
        using (var ctx = CreateContext())
        {
            foreach (var l in ctx.ChildLinks.Where(l => l.SchoolStudentId == s.StudentId)) l.IsActive = false;
            ctx.SaveChanges();
        }
        using var verify = CreateContext();
        var (evidence, _, _, _) = Services(verify);
        var items = (await evidence.BuildForStaffAsync(s.TeacherId, s.StudentId)).Data!.Items;
        Assert.DoesNotContain(items, i => i.Kind == EvidenceKind.ParentContribution);
    }

    // ---------------------------------------------------------------- Prefill on create

    [Fact]
    public async Task NewIep_IsPrefilled_WithIdentityPresentLevelsAndCarriedGoals()
    {
        var s = await SeedAsync();
        using var ctx = CreateContext();
        var (_, _, _, instances) = Services(ctx);
        var created = await instances.CreateAsync(s.StudentId, IepTypeId, s.TeacherId);
        Assert.True(created.Success, created.Message);

        var values = JsonNode.Parse(created.Data!.ValuesJson) as JsonObject;
        var sections = await ctx.TemplateSections.Include(x => x.Fields).Where(x => x.DocumentTemplateVersionId == created.Data.DocumentTemplateVersionId).ToListAsync();
        var sem = TemplateSemanticsReader.Read(sections);

        var profile = values![sem[FieldSemantics.StudentProfile].FieldKey.ToString()]!.ToString();
        Assert.Contains("Jordan Ellis", profile);
        Assert.Contains("IEP team: Steph Case", profile);

        var plaafp = values[sem[FieldSemantics.PresentLevels].FieldKey.ToString()]!.ToString();
        Assert.StartsWith("[Carried from IEP v1", plaafp);
        Assert.Contains("42 wpm", plaafp);

        var goals = values[sem[FieldSemantics.Goals].FieldKey.ToString()] as JsonArray;
        var row = Assert.Single(goals!.OfType<JsonObject>());
        Assert.Equal(s.GoalRowId.ToString(), row[RowMetaKeys.RowId]!.ToString()); // lineage preserved
        Assert.Equal("Read 70 wpm by May", row[sem[FieldSemantics.Goals].Columns[ColumnSemantics.GoalText].ToString()]!.ToString());
        Assert.Equal(s.IepVersionId, row[RowMetaKeys.CarriedFrom]!["versionId"]!.GetValue<int>());
        Assert.Equal("IEP v1", row[RowMetaKeys.CarriedFrom]!["label"]!.ToString());
        Assert.False(row[RowMetaKeys.Confirmed]!.GetValue<bool>());
    }

    [Fact]
    public async Task NewEtr_DoesNotCarryIepGoals_AndNewStudentGetsIdentityOnly()
    {
        var s = await SeedAsync();
        using (var ctx = CreateContext())
        {
            var (_, _, _, instances) = Services(ctx);
            var etr = await instances.CreateAsync(s.StudentId, EtrTypeId, s.TeacherId);
            Assert.True(etr.Success, etr.Message);
            var values = JsonNode.Parse(etr.Data!.ValuesJson) as JsonObject;
            var sections = await ctx.TemplateSections.Include(x => x.Fields).Where(x => x.DocumentTemplateVersionId == etr.Data.DocumentTemplateVersionId).ToListAsync();
            var sem = TemplateSemanticsReader.Read(sections);
            Assert.Contains("Jordan Ellis", values![sem[FieldSemantics.StudentProfile].FieldKey.ToString()]!.ToString());
            Assert.Null(values[sem[FieldSemantics.EvaluatorReports].FieldKey.ToString()]);
            Assert.Null(values[sem[FieldSemantics.PresentLevels].FieldKey.ToString()]); // an ETR never inherits present levels
        }

        var fresh = await SeedFreshStudentAsync();
        using (var ctx = CreateContext())
        {
            var (_, _, _, instances) = Services(ctx);
            var iep = await instances.CreateAsync(fresh.StudentId, IepTypeId, fresh.TeacherId);
            Assert.True(iep.Success, iep.Message);
            var values = JsonNode.Parse(iep.Data!.ValuesJson) as JsonObject;
            Assert.Single(values!); // identity only — nothing invented
        }
    }

    private async Task<(int StudentId, int TeacherId)> SeedFreshStudentAsync()
    {
        using var ctx = CreateContext();
        var teacher = new User { Email = "t2@example.com", PasswordHash = "x", FirstName = "New", LastName = "Teacher", Role = UserRole.Educator };
        ctx.Users.Add(teacher); ctx.SaveChanges();
        var district = new District { Name = "d2", StateCode = "OH" }; ctx.Districts.Add(district); ctx.SaveChanges();
        var school = new School { DistrictId = district.Id, Name = "s2" }; ctx.Schools.Add(school); ctx.SaveChanges();
        ctx.StaffProfiles.Add(new StaffProfile { UserId = teacher.Id, DistrictId = district.Id, SchoolId = school.Id, OrgRoleId = OrgRoleIds.Teacher });
        var student = new SchoolStudent { SchoolId = school.Id, FirstName = "Fresh" }; ctx.SchoolStudents.Add(student); ctx.SaveChanges();
        ctx.SchoolStudentAccesses.Add(new SchoolStudentAccess { SchoolStudentId = student.Id, UserId = teacher.Id, Role = AccessRole.Collaborator, IsActive = true });
        ctx.SaveChanges();
        await Task.CompletedTask;
        return (student.Id, teacher.Id);
    }

    // ---------------------------------------------------------------- Family notes

    [Fact]
    public async Task ParentContributions_ParentManages_StaffSeesOnlyShared()
    {
        var s = await SeedAsync(withHistory: false);
        using var ctx = CreateContext();
        var (_, _, contributions, _) = Services(ctx);

        var mine = await contributions.GetForChildAsync(s.ChildId, s.ParentId);
        Assert.True(mine.Success);
        Assert.Equal(2, mine.Data!.Count);

        var created = await contributions.CreateAsync(s.ChildId, s.ParentId, new SaveParentContributionModel { Kind = ParentContributionKind.Priority, Text = "Keep him in gen-ed math", IsShared = true });
        Assert.True(created.Success, created.Message);
        Assert.Single(_audit.Entries, e => e.Action == AuditAction.Share && e.ResourceType == "ParentContribution" && e.ResourceId == created.Data!.Id);
        var updated = await contributions.UpdateAsync(created.Data!.Id, s.ParentId, new SaveParentContributionModel { Kind = ParentContributionKind.Priority, Text = "Keep him in gen-ed math", IsShared = false });
        Assert.True(updated.Success);

        var staffView = await contributions.GetSharedForSchoolStudentAsync(s.TeacherId, s.StudentId);
        Assert.True(staffView.Success);
        Assert.Single(staffView.Data!); // only the originally shared note; the unshared + just-unshared stay private
        Assert.DoesNotContain(staffView.Data!, c => c.Text.Contains("PRIVATE") || c.Text.Contains("gen-ed"));

        Assert.False((await contributions.GetForChildAsync(s.ChildId, s.TeacherId)).Success); // staff can't read the parent's full list
        Assert.False((await contributions.UpdateAsync(created.Data.Id, s.StrangerId, new SaveParentContributionModel { Kind = ParentContributionKind.Other, Text = "x", IsShared = true })).Success);
        Assert.False((await contributions.CreateAsync(s.ChildId, s.ParentId, new SaveParentContributionModel { Kind = ParentContributionKind.Other, Text = "   ", IsShared = false })).Success);
    }

    // ---------------------------------------------------------------- Row metadata survives saves

    [Fact]
    public async Task SaveValues_PreservesWellFormedCarriedFromAndConfirmed_DropsMalformed()
    {
        var s = await SeedAsync();
        using var ctx = CreateContext();
        var (_, _, _, instances) = Services(ctx);
        var created = await instances.CreateAsync(s.StudentId, IepTypeId, s.TeacherId);
        var sections = await ctx.TemplateSections.Include(x => x.Fields).Where(x => x.DocumentTemplateVersionId == created.Data!.DocumentTemplateVersionId).ToListAsync();
        var goals = TemplateSemanticsReader.Read(sections)[FieldSemantics.Goals];
        var goalCol = goals.Columns[ColumnSemantics.GoalText];

        var patch = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>($$"""
        { "{{goals.FieldKey}}": [
            { "_rowId": "{{s.GoalRowId}}", "_carriedFrom": { "versionId": {{s.IepVersionId}}, "rowId": "{{s.GoalRowId}}", "label": "IEP v1" }, "_confirmed": true, "{{goalCol}}": "kept" },
            { "_carriedFrom": "junk", "_confirmed": "yes", "{{goalCol}}": "new" } ] }
        """)!;
        var saved = await instances.SaveValuesAsync(created.Data!.Id, patch, null, s.TeacherId);
        Assert.True(saved.Success, saved.Message);
        var rows = (JsonNode.Parse(saved.Data!.ValuesJson) as JsonObject)![goals.FieldKey.ToString()] as JsonArray;
        Assert.Equal(2, rows!.Count);
        Assert.Equal("IEP v1", rows[0]![RowMetaKeys.CarriedFrom]!["label"]!.ToString());
        Assert.True(rows[0]![RowMetaKeys.Confirmed]!.GetValue<bool>());
        Assert.Null(rows[1]![RowMetaKeys.CarriedFrom]);
        Assert.Null(rows[1]![RowMetaKeys.Confirmed]);
    }

    public void Dispose() => _connection.Dispose();
}
