using System.IO.Compression;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;
using IepAssistant.Services.Security;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Plan 3 XLSX import pipeline: template contents, upload rejections (.xlsm, oversize, row cap),
/// preview outcomes (new/updated/unchanged/error incl. leading-zero ids, duplicates, unknown school,
/// blank-keeps / CLEAR-clears, SchoolAdmin cross-school rows), idempotent commit, error workbook, and the
/// staff sheet (update existing, invite unknown). Workbooks are built in memory with ClosedXML; the
/// database is real SQLite in-memory via <see cref="RosterTestDb"/>.
/// </summary>
public sealed class RosterImportServiceTests : IDisposable
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] StudentHeaders =
    {
        "StudentId*", "SchoolName*", "SchoolCode", "FirstName*", "LastName*", "DateOfBirth*", "Grade*", "DisabilityCategory",
        "HomeLanguage", "CaseManagerEmail", "IepDate", "AnnualReviewDue", "EtrDate", "ReevaluationDue", "Status"
    };

    private readonly RosterTestDb _db = new();
    private readonly CapturingAuditLogger _audit = new();
    private readonly CapturingEmailService _email = new();
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

    private RosterImportService Roster(ApplicationDbContext ctx)
        => new(ctx, new OrgAccessService(ctx), _audit, NullLogger<RosterImportService>.Instance);

    private StaffImportService StaffImports(ApplicationDbContext ctx)
    {
        var org = new OrgAccessService(ctx);
        var invites = new StaffInviteService(ctx, org, _email, new JwtTokenFactory(_configuration), new InviteLinkExposure(false), _configuration, NullLogger<StaffInviteService>.Instance);
        return new StaffImportService(ctx, org, invites, _audit, NullLogger<StaffImportService>.Instance);
    }

    // ----------------------------------------------------------------- workbook builders

    /// <summary>Students sheet with the canonical headers; each row is a dictionary of column → value (missing = blank).</summary>
    private static byte[] StudentsWorkbook(params Dictionary<string, object>[] rows)
        => Workbook("Students", StudentHeaders, rows);

    private static byte[] StaffWorkbook(params Dictionary<string, object>[] rows)
        => Workbook("Staff", new[] { "Email*", "FirstName*", "LastName*", "Role*", "SchoolName", "Title" }, rows);

    private static byte[] Workbook(string sheetName, string[] headers, Dictionary<string, object>[] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).SetValue(headers[c]);
        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < headers.Length; c++)
            {
                var key = headers[c].TrimEnd('*');
                if (!rows[r].TryGetValue(key, out var value)) continue;
                var cell = ws.Cell(r + 2, c + 1);
                switch (value)
                {
                    case string text: cell.SetValue(text); break;
                    case DateTime date: cell.SetValue(date); break;
                    case double number: cell.SetValue(number); break;
                    case int number: cell.SetValue(number); break;
                    default: cell.SetValue(value.ToString()); break;
                }
            }
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static Dictionary<string, object> StudentRow(string id, string school, string first = "Sam", string last = "Student", object? dob = null, string grade = "5", params (string Column, object Value)[] extra)
    {
        var row = new Dictionary<string, object>
        {
            ["StudentId"] = id, ["SchoolName"] = school, ["FirstName"] = first, ["LastName"] = last,
            ["DateOfBirth"] = dob ?? "2015-04-02", ["Grade"] = grade
        };
        foreach (var (column, value) in extra) row[column] = value;
        return row;
    }

    private static ImportUploadModel Upload(byte[] content, string fileName = "roster.xlsx", string contentType = Xlsx)
        => new() { FileName = fileName, ContentType = contentType, Length = content.Length, Content = content };

    private (int District, int SchoolA, int SchoolB, int Admin) Org()
    {
        var district = _db.District(stateCode: "OH");
        var schoolA = _db.School(district, "Maple Elementary", "OH");
        var schoolB = _db.School(district, "Oak Middle", "OH");
        var (admin, _) = _db.Staff("da@x.com", district, null, OrgRoleIds.DistrictAdmin);
        return (district, schoolA, schoolB, admin);
    }

    // ----------------------------------------------------------------- template

    [Fact]
    public async Task GenerateTemplate_ContainsLiveSchoolsAndCaseManagers_AndTeacherIsDenied()
    {
        var o = Org();
        _db.Staff("cm@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher, "Steph", "Case");
        var (teacher, _) = _db.Staff("t@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);

        using var ctx = _db.Context();
        var result = await Roster(ctx).GenerateTemplateAsync(o.Admin);
        var denied = await Roster(ctx).GenerateTemplateAsync(teacher);

        Assert.True(result.Success, result.Message);
        using var wb = new XLWorkbook(new MemoryStream(result.Data!));
        var students = wb.Worksheet("Students");
        Assert.Equal("StudentId*", students.Cell(1, 1).GetString());
        Assert.Equal("Maple Elementary", students.Cell(2, 2).GetString()); // example row uses a live school
        var values = wb.Worksheet("Values");
        var schoolNames = values.Column(5).CellsUsed().Skip(1).Select(c => c.GetString()).ToList();
        Assert.Equal(new[] { "Maple Elementary", "Oak Middle" }, schoolNames);
        var managers = values.Column(7).CellsUsed().Skip(1).Select(c => c.GetString()).ToList();
        Assert.Equal(new[] { "cm@x.com", "t@x.com" }, managers); // district admin excluded
        Assert.Contains("Specific Learning Disability", values.Column(2).CellsUsed().Select(c => c.GetString()));
        Assert.False(denied.Success);
        Assert.Contains("permission", denied.Message);
    }

    // ----------------------------------------------------------------- upload rejections

    [Fact]
    public async Task Preview_RejectsXlsmByExtensionAndByContentType()
    {
        var o = Org();
        var content = StudentsWorkbook(StudentRow("1", "Maple Elementary"));

        using var ctx = _db.Context();
        var byExtension = await Roster(ctx).PreviewAsync(o.Admin, Upload(content, "roster.xlsm", Xlsx));
        var byContentType = await Roster(ctx).PreviewAsync(o.Admin, Upload(content, "roster.xlsx", "application/vnd.ms-excel.sheet.macroEnabled.12"));

        Assert.False(byExtension.Success);
        Assert.Contains(".xlsm", byExtension.Message);
        Assert.False(byContentType.Success);
        Assert.Equal("Only .xlsx workbooks are accepted.", byContentType.Message);
        Assert.Empty(ctx.ImportBatches);
    }

    [Fact]
    public async Task Preview_RejectsOversizeFile_AndTooManyRows()
    {
        var o = Org();
        var oversize = new ImportUploadModel { FileName = "big.xlsx", ContentType = Xlsx, Length = 5 * 1024 * 1024 + 1, Content = new byte[10] };
        var tooMany = StudentsWorkbook(Enumerable.Range(1, 5001).Select(i => StudentRow(i.ToString(), "Maple Elementary")).ToArray());

        using var ctx = _db.Context();
        var big = await Roster(ctx).PreviewAsync(o.Admin, oversize);
        var rows = await Roster(ctx).PreviewAsync(o.Admin, Upload(tooMany));

        Assert.Equal("The file is larger than 5 MB.", big.Message);
        Assert.Contains("5,000", rows.Message);
        Assert.Empty(ctx.ImportBatches);
    }

    [Fact]
    public async Task Preview_MissingRequiredColumn_OrMissingSheet_IsRejected()
    {
        var o = Org();
        var noSheet = Workbook("Kids", StudentHeaders, Array.Empty<Dictionary<string, object>>());
        var noGrade = Workbook("Students", new[] { "StudentId", "SchoolName", "FirstName", "LastName", "DateOfBirth" }, Array.Empty<Dictionary<string, object>>());

        using var ctx = _db.Context();
        var sheet = await Roster(ctx).PreviewAsync(o.Admin, Upload(noSheet));
        var column = await Roster(ctx).PreviewAsync(o.Admin, Upload(noGrade));

        Assert.Equal("The workbook must contain a sheet named 'Students'.", sheet.Message);
        Assert.Equal("Missing required column: Grade.", column.Message);
    }

    // ----------------------------------------------------------------- preview outcomes

    [Fact]
    public async Task Preview_NewRows_PreserveLeadingZeroIds_FromTextAndFormattedNumberCells()
    {
        var o = Org();
        byte[] content;
        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("Students");
            for (var c = 0; c < StudentHeaders.Length; c++) ws.Cell(1, c + 1).SetValue(StudentHeaders[c]);
            // Row 2: text id with leading zeros; row 3: numeric id under a "00000" format; row 4: formula (cached value only).
            ws.Cell(2, 1).SetValue("000123");
            ws.Cell(3, 1).SetValue(45).Style.NumberFormat.Format = "00000";
            ws.Cell(4, 1).FormulaA1 = "=\"X\"&\"9\"";
            foreach (var r in new[] { 2, 3, 4 })
            {
                ws.Cell(r, 2).SetValue("Maple Elementary");
                ws.Cell(r, 4).SetValue("Kid" + r);
                ws.Cell(r, 5).SetValue("Last");
                ws.Cell(r, 6).SetValue(new DateTime(2015, 4, 2));
                ws.Cell(r, 7).SetValue(r == 2 ? "K" : "5");
            }
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            content = ms.ToArray();
        }

        using var ctx = _db.Context();
        var result = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));

        Assert.True(result.Success, result.Message);
        Assert.Equal(new[] { "000123", "00045" }, result.Data!.Rows.Take(2).Select(r => r.Key));
        Assert.All(result.Data.Rows.Take(2), r => Assert.Equal(ImportRowOutcome.New, r.Outcome));
        // The formula cell has no cached value in a freshly built file and is never evaluated.
        Assert.Equal(ImportRowOutcome.Error, result.Data.Rows[2].Outcome);
        Assert.Equal("StudentId is required.", result.Data.Rows[2].Message);
    }

    [Fact]
    public async Task Preview_IdenticalReupload_IsAllUnchanged_AndChangedGradeIsUpdatedWithDiff()
    {
        var o = Org();
        var (cm, _) = _db.Staff("cm@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var content = StudentsWorkbook(
            StudentRow("000123", "Maple Elementary", "Ann", "Zed", new DateTime(2015, 4, 2), "5", ("DisabilityCategory", "SLD"), ("CaseManagerEmail", "cm@x.com"), ("IepDate", "2026-01-15")),
            StudentRow("000124", "Oak Middle", "Bob", "Young", "2013-09-18", "7"));

        using var ctx = _db.Context();
        var first = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var commit = await Roster(ctx).CommitAsync(o.Admin, first.Data!.BatchId, commitValid: false);
        var again = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var changed = await Roster(ctx).PreviewAsync(o.Admin, Upload(StudentsWorkbook(
            StudentRow("000123", "Maple Elementary", "Ann", "Zed", new DateTime(2015, 4, 2), "6"))));

        Assert.Equal(2, commit.Data!.Committed.New);
        Assert.All(again.Data!.Rows, r => Assert.Equal(ImportRowOutcome.Unchanged, r.Outcome));
        Assert.Equal(2, again.Data.Counts.Unchanged);
        var row = Assert.Single(changed.Data!.Rows);
        Assert.Equal(ImportRowOutcome.Updated, row.Outcome);
        Assert.Equal(new[] { "Grade: 5 → 6" }, row.Changes);
        var stored = ctx.SchoolStudents.AsNoTracking().Single(s => s.ExternalStudentId == "000123");
        Assert.Equal(cm, stored.CaseManagerUserId);
        Assert.Equal(DisabilityCategory.SpecificLearningDisability, stored.DisabilityCategory);
        Assert.Equal(new DateTime(2026, 1, 15), stored.IepDate);
    }

    [Fact]
    public async Task Preview_UnknownSchool_UnknownCaseManager_BadGrade_AreRowErrors()
    {
        var o = Org();
        var content = StudentsWorkbook(
            StudentRow("1", "Nowhere High"),
            StudentRow("2", "Maple Elementary", extra: ("CaseManagerEmail", "ghost@x.com")),
            StudentRow("3", "Maple Elementary", grade: "13"));

        using var ctx = _db.Context();
        var result = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));

        Assert.Equal(3, result.Data!.Counts.Error);
        Assert.Equal("Unknown school 'Nowhere High'.", result.Data.Rows[0].Message);
        Assert.Contains("ghost@x.com", result.Data.Rows[1].Message);
        Assert.Contains("Unknown grade '13'", result.Data.Rows[2].Message);
    }

    [Fact]
    public async Task Preview_DuplicateStudentIdInFile_FlagsBothRows()
    {
        var o = Org();
        var content = StudentsWorkbook(
            StudentRow("000123", "Maple Elementary", "First"),
            StudentRow("000124", "Maple Elementary", "Fine"),
            StudentRow("000123", "Maple Elementary", "Second"));

        using var ctx = _db.Context();
        var result = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));

        Assert.Equal(ImportRowOutcome.Error, result.Data!.Rows[0].Outcome);
        Assert.Equal(ImportRowOutcome.Error, result.Data.Rows[2].Outcome);
        Assert.Equal("Duplicate StudentId in file (rows 2, 4).", result.Data.Rows[0].Message);
        Assert.Equal(ImportRowOutcome.New, result.Data.Rows[1].Outcome);
    }

    [Fact]
    public async Task Commit_BlankCellKeepsValue_ClearTokenClearsNullableField_AndRefusesClearingRequired()
    {
        var o = Org();
        var (cm, _) = _db.Staff("cm@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var student = _db.Student(o.SchoolA, "Ann", "Zed", "000123", GradeLevel.G5, dob: new DateTime(2015, 4, 2));
        using (var seed = _db.Context())
        {
            var s = seed.SchoolStudents.Single(x => x.Id == student);
            s.DisabilityCategory = DisabilityCategory.Autism;
            s.HomeLanguage = "es";
            s.IepDate = new DateTime(2026, 1, 15);
            seed.SaveChanges();
        }
        _db.TeamMember(student, cm, TeamRole.CaseManager, isLead: true);
        var content = StudentsWorkbook(
            new Dictionary<string, object> { ["StudentId"] = "000123", ["DisabilityCategory"] = "CLEAR", ["CaseManagerEmail"] = "CLEAR", ["IepDate"] = "CLEAR" },
            StudentRow("000999", "Maple Elementary", "New", "Kid", extra: ("FirstName", "CLEAR")));

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var commit = await Roster(ctx).CommitAsync(o.Admin, preview.Data!.BatchId, commitValid: true);

        Assert.Equal(ImportRowOutcome.Updated, preview.Data.Rows[0].Outcome);
        Assert.Equal("FirstName cannot be cleared.", preview.Data.Rows[1].Message);
        Assert.Equal(1, commit.Data!.Committed.Updated);
        Assert.Equal(1, commit.Data.Skipped);
        using var check = _db.Context();
        var stored = check.SchoolStudents.Single(s => s.Id == student);
        Assert.Equal("Ann", stored.FirstName);             // blank kept
        Assert.Equal(GradeLevel.G5, stored.GradeLevel);     // blank kept
        Assert.Equal("es", stored.HomeLanguage);            // blank kept
        Assert.Null(stored.DisabilityCategory);             // CLEAR
        Assert.Null(stored.IepDate);                        // CLEAR
        Assert.Null(stored.CaseManagerUserId);              // CLEAR demotes the lead
        Assert.False(check.StudentTeamMembers.Single(m => m.SchoolStudentId == student).IsLead);
        Assert.Contains(_audit.Entries, e => e.ResourceType == "ImportBatch" && e.ResourceId == preview.Data.BatchId && e.Action == AuditAction.Edit);
    }

    [Fact]
    public async Task Preview_SchoolAdmin_CrossSchoolRowIsError_OwnSchoolRowIsFine()
    {
        var o = Org();
        var (schoolAdmin, _) = _db.Staff("sa@x.com", o.District, o.SchoolA, OrgRoleIds.SchoolAdmin);
        var content = StudentsWorkbook(
            StudentRow("1", "Maple Elementary"),
            StudentRow("2", "Oak Middle"));

        using var ctx = _db.Context();
        var result = await Roster(ctx).PreviewAsync(schoolAdmin, Upload(content));

        Assert.Equal(ImportRowOutcome.New, result.Data!.Rows[0].Outcome);
        Assert.Equal(ImportRowOutcome.Error, result.Data.Rows[1].Outcome);
        Assert.Equal("You can only import students for Maple Elementary.", result.Data.Rows[1].Message);
        Assert.Equal("Sam Student", result.Data.Rows[1].DisplayName); // sheet-supplied only
    }

    // ----------------------------------------------------------------- commit

    [Fact]
    public async Task Commit_WithErrorsAndCommitValidFalse_IsRefused_ThenValidOnlyCommits_AndSecondCommitIsRefused()
    {
        var o = Org();
        var content = StudentsWorkbook(
            StudentRow("000123", "Maple Elementary", "Ann"),
            StudentRow("000124", "Nowhere"));

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var refused = await Roster(ctx).CommitAsync(o.Admin, preview.Data!.BatchId, commitValid: false);
        var committed = await Roster(ctx).CommitAsync(o.Admin, preview.Data.BatchId, commitValid: true);
        var again = await Roster(ctx).CommitAsync(o.Admin, preview.Data.BatchId, commitValid: true);

        Assert.Equal("Fix the errors or choose to import valid rows only.", refused.Message);
        Assert.True(committed.Success, committed.Message);
        Assert.Equal(1, committed.Data!.Committed.New);
        Assert.Equal(1, committed.Data.Skipped);
        Assert.Equal(ImportBatchStatus.Committed, committed.Data.Status);
        Assert.Equal("This import has already been committed.", again.Message);
        Assert.Equal(1, ctx.SchoolStudents.Count(s => s.DistrictId == o.District));
        var stored = ctx.SchoolStudents.AsNoTracking().Single(s => s.ExternalStudentId == "000123");
        Assert.Equal((o.SchoolA, o.District, "OH", "en"), (stored.SchoolId, stored.DistrictId, stored.StateCode, stored.HomeLanguage));
    }

    [Fact]
    public async Task Commit_OtherDistrictAdmin_CannotSeeOrCommitBatch()
    {
        var o = Org();
        var (otherAdmin, _) = _db.Staff("db@x.com", _db.District("Other"), null, OrgRoleIds.DistrictAdmin);

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(StudentsWorkbook(StudentRow("1", "Maple Elementary"))));
        var commit = await Roster(ctx).CommitAsync(otherAdmin, preview.Data!.BatchId, commitValid: true);
        var detail = await Roster(ctx).GetBatchAsync(otherAdmin, preview.Data.BatchId);
        var history = await Roster(ctx).GetHistoryAsync(otherAdmin);

        Assert.Equal("Import not found.", commit.Message);
        Assert.Equal("Import not found.", detail.Message);
        Assert.Empty(history.Data!);
    }

    [Fact]
    public async Task History_AndErrorWorkbook_ReflectTheBatch()
    {
        var o = Org();
        var content = StudentsWorkbook(StudentRow("000123", "Maple Elementary"), StudentRow("000124", "Nowhere", "Lost", "Kid"));

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content, "fall-roster.xlsx"));
        var history = await Roster(ctx).GetHistoryAsync(o.Admin);
        var errors = await Roster(ctx).BuildErrorWorkbookAsync(o.Admin, preview.Data!.BatchId);

        var batch = Assert.Single(history.Data!);
        Assert.Equal(("fall-roster.xlsx", ImportKind.Students, ImportBatchStatus.Previewed, 2, 1, 1), (batch.FileName, batch.Kind, batch.Status, batch.Counts.Total, batch.Counts.New, batch.Counts.Error));
        Assert.Equal("Staff Member", batch.CreatedByName);
        using var wb = new XLWorkbook(new MemoryStream(errors.Data!));
        var ws = wb.Worksheet("Students");
        Assert.Equal("Error", ws.Cell(1, 16).GetString());
        Assert.Equal("000124", ws.Cell(2, 1).GetString());
        Assert.Equal("Lost", ws.Cell(2, 4).GetString());
        Assert.Equal("Unknown school 'Nowhere'.", ws.Cell(2, 16).GetString());
        Assert.True(ws.Cell(3, 1).IsEmpty());
    }

    [Fact]
    public async Task Commit_1000NewRowsWithCaseManagers_IsSetBased_AndAuditsAfterCommit()
    {
        var o = Org();
        var (cm, _) = _db.Staff("cm@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var content = StudentsWorkbook(Enumerable.Range(1, 1000)
            .Select(i => StudentRow(i.ToString("D6"), "Maple Elementary", "Kid", "Number" + i, extra: ("CaseManagerEmail", "cm@x.com")))
            .ToArray());
        var counter = new DbActivityCounter();

        using var ctx = _db.Context(counter);
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        Assert.Equal(1000, preview.Data!.Counts.New);
        counter.Reset();
        var commit = await Roster(ctx).CommitAsync(o.Admin, preview.Data.BatchId, commitValid: false);

        Assert.True(commit.Success, commit.Message);
        Assert.Equal(1000, commit.Data!.Committed.New);
        // Linear, chunked cost: a handful of saves and lookups per 500-row chunk — not per row (the old
        // path was ~3 saves and ~6 round trips per row; SQLite itself still sends one INSERT per row).
        Assert.True(counter.SaveChanges < 50, $"SaveChanges = {counter.SaveChanges}");
        Assert.True(counter.Queries < 40, $"Queries = {counter.Queries}");
        using var check = _db.Context();
        Assert.Equal(1000, check.SchoolStudents.Count(s => s.DistrictId == o.District && s.CaseManagerUserId == cm));
        Assert.Equal(1000, check.StudentTeamMembers.Count(m => m.UserId == cm && m.IsLead && m.IsActive));
        Assert.Equal(1000, check.SchoolStudentAccesses.Count(a => a.UserId == cm && a.IsActive && a.Role == AccessRole.Owner));
        Assert.Equal(ImportBatchStatus.Committed, check.ImportBatches.Single().Status);
        Assert.Equal(1000, _audit.Entries.Count(e => e.ResourceType == "SchoolStudent" && e.Action == AuditAction.Edit));
    }

    [Fact]
    public async Task Commit_LeadSwapAcrossManyStudents_RespectsSingleActiveLeadIndex()
    {
        var o = Org();
        var (oldLead, _) = _db.Staff("old@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (newLead, _) = _db.Staff("new@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var ids = Enumerable.Range(1, 20).Select(i => _db.Student(o.SchoolA, "Kid", "N" + i, i.ToString("D6"))).ToList();
        foreach (var id in ids)
            _db.TeamMember(id, oldLead, TeamRole.CaseManager, isLead: true);
        var content = StudentsWorkbook(Enumerable.Range(1, 20)
            .Select(i => new Dictionary<string, object> { ["StudentId"] = i.ToString("D6"), ["CaseManagerEmail"] = "new@x.com" })
            .ToArray());

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var commit = await Roster(ctx).CommitAsync(o.Admin, preview.Data!.BatchId, commitValid: false);

        Assert.True(commit.Success, commit.Message);
        Assert.Equal(20, commit.Data!.Committed.Updated);
        using var check = _db.Context();
        foreach (var id in ids)
        {
            var lead = Assert.Single(check.StudentTeamMembers.Where(m => m.SchoolStudentId == id && m.IsActive && m.IsLead));
            Assert.Equal(newLead, lead.UserId);
            Assert.True(check.StudentTeamMembers.Single(m => m.SchoolStudentId == id && m.UserId == oldLead).IsActive);
            Assert.Equal(newLead, check.SchoolStudents.Single(s => s.Id == id).CaseManagerUserId);
        }
    }

    [Fact]
    public async Task Commit_SchoolChange_DeactivatesNonPortableTeam_AndPreviewAnnouncesIt()
    {
        var o = Org();
        var (teacherA, _) = _db.Staff("ta@x.com", o.District, o.SchoolA, OrgRoleIds.Teacher);
        var (slp, _) = _db.Staff("slp@x.com", o.District, o.SchoolA, OrgRoleIds.RelatedServiceProvider);
        var (teacherB, _) = _db.Staff("tb@x.com", o.District, o.SchoolB, OrgRoleIds.Teacher);
        var student = _db.Student(o.SchoolA, "Ann", "Zed", "000123", stateCode: "OH");
        _db.TeamMember(student, teacherA, TeamRole.CaseManager, isLead: true);
        _db.TeamMember(student, slp, TeamRole.SpeechLanguagePathologist);
        var content = StudentsWorkbook(new Dictionary<string, object> { ["StudentId"] = "000123", ["SchoolName"] = "Oak Middle", ["CaseManagerEmail"] = "tb@x.com" });

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var commit = await Roster(ctx).CommitAsync(o.Admin, preview.Data!.BatchId, commitValid: false);

        var row = Assert.Single(preview.Data.Rows);
        Assert.Equal(ImportRowOutcome.Updated, row.Outcome);
        Assert.Contains("School: Maple Elementary → Oak Middle", row.Changes);
        Assert.Contains("Team: 1 member(s) will be deactivated", row.Changes);
        Assert.Contains("Case manager: ta@x.com → tb@x.com", row.Changes);
        Assert.True(commit.Success, commit.Message);
        using var check = _db.Context();
        var stored = check.SchoolStudents.Single(s => s.Id == student);
        Assert.Equal(o.SchoolB, stored.SchoolId);
        Assert.Equal(teacherB, stored.CaseManagerUserId);
        var members = check.StudentTeamMembers.Where(m => m.SchoolStudentId == student).ToDictionary(m => m.UserId);
        Assert.False(members[teacherA].IsActive);
        Assert.False(members[teacherA].IsLead);
        Assert.Contains("Oak Middle", members[teacherA].Note);
        Assert.True(members[slp].IsActive);                 // provider spans buildings
        Assert.True(members[teacherB].IsActive);
        Assert.True(members[teacherB].IsLead);
        var access = check.SchoolStudentAccesses.Where(a => a.SchoolStudentId == student).ToDictionary(a => a.UserId);
        Assert.False(access[teacherA].IsActive);
        Assert.True(access[slp].IsActive);
        Assert.True(access[teacherB].IsActive);
    }

    [Fact]
    public async Task Preview_SchoolAdmin_OtherSchoolStudentLooksUnknown_NoNameDisclosed_AndCommitFlagsOnlyThatRow()
    {
        var o = Org();
        var (schoolAdmin, _) = _db.Staff("sa@x.com", o.District, o.SchoolA, OrgRoleIds.SchoolAdmin);
        _db.Student(o.SchoolB, "Secret", "Person", "000555");
        var content = StudentsWorkbook(
            new Dictionary<string, object> { ["StudentId"] = "000555" },                       // ids only, like a probe
            StudentRow("000555", "Maple Elementary", "Probe", "Name"),                          // full row (duplicate key → both flagged; separate batch below)
            StudentRow("000777", "Maple Elementary", "Own", "Kid"));

        using var ctx = _db.Context();
        var probe = await Roster(ctx).PreviewAsync(schoolAdmin, Upload(StudentsWorkbook(new Dictionary<string, object> { ["StudentId"] = "000555" })));
        var preview = await Roster(ctx).PreviewAsync(schoolAdmin, Upload(StudentsWorkbook(
            StudentRow("000555", "Maple Elementary", "Probe", "Name"),
            StudentRow("000777", "Maple Elementary", "Own", "Kid"))));
        var commit = await Roster(ctx).CommitAsync(schoolAdmin, preview.Data!.BatchId, commitValid: true);

        // The ids-only probe: no stored name, and a message that does not reveal the student exists.
        var probeRow = Assert.Single(probe.Data!.Rows);
        Assert.Equal(ImportRowOutcome.Error, probeRow.Outcome);
        Assert.Equal(string.Empty, probeRow.DisplayName);
        Assert.Equal("SchoolName (or SchoolCode) is required for a new student.", probeRow.Message);
        Assert.DoesNotContain("Secret", ctx.ImportRows.AsNoTracking().Select(r => r.DisplayName).ToList());
        // The full row previews as New (the other building's record is invisible) and only fails at commit.
        Assert.Equal(new[] { ImportRowOutcome.New, ImportRowOutcome.New }, preview.Data.Rows.Select(r => r.Outcome));
        Assert.True(commit.Success, commit.Message);
        Assert.Equal((1, 1), (commit.Data!.Committed.New, commit.Data.Skipped));
        using var check = _db.Context();
        var rows = check.ImportRows.Where(r => r.BatchId == preview.Data.BatchId).OrderBy(r => r.RowNumber).ToList();
        Assert.Equal(ImportRowOutcome.Error, rows[0].Outcome);
        Assert.Equal("Student ID already in use in this district.", rows[0].Message);
        Assert.Equal(ImportRowOutcome.New, rows[1].Outcome);
        Assert.Equal("Own", check.SchoolStudents.Single(s => s.ExternalStudentId == "000777").FirstName);
        Assert.Equal("Secret", check.SchoolStudents.Single(s => s.ExternalStudentId == "000555").FirstName);
        Assert.Equal(ImportBatchStatus.Committed, check.ImportBatches.Single(b => b.Id == preview.Data.BatchId).Status);
        _ = content;
    }

    [Fact]
    public async Task Preview_SchoolAdmin_CaseManagerOutsideEligiblePool_GetsOneNeutralMessage()
    {
        var o = Org();
        var (schoolAdmin, _) = _db.Staff("sa@x.com", o.District, o.SchoolA, OrgRoleIds.SchoolAdmin);
        _db.Staff("tb@x.com", o.District, o.SchoolB, OrgRoleIds.Teacher);
        _db.Staff("slp@x.com", o.District, o.SchoolB, OrgRoleIds.RelatedServiceProvider);
        var content = StudentsWorkbook(
            StudentRow("1", "Maple Elementary", extra: ("CaseManagerEmail", "tb@x.com")),      // exists, other building
            StudentRow("2", "Maple Elementary", extra: ("CaseManagerEmail", "nobody@x.com")),  // does not exist
            StudentRow("3", "Maple Elementary", extra: ("CaseManagerEmail", "da@x.com")),      // district admin
            StudentRow("4", "Maple Elementary", extra: ("CaseManagerEmail", "slp@x.com")));    // provider: eligible

        using var ctx = _db.Context();
        var result = await Roster(ctx).PreviewAsync(schoolAdmin, Upload(content));

        Assert.Equal("Case manager 'tb@x.com' is not available for your school.", result.Data!.Rows[0].Message);
        Assert.Equal("Case manager 'nobody@x.com' is not available for your school.", result.Data.Rows[1].Message);
        Assert.Equal("Case manager 'da@x.com' is not available for your school.", result.Data.Rows[2].Message);
        Assert.Equal(ImportRowOutcome.New, result.Data.Rows[3].Outcome);
    }

    [Fact]
    public async Task Preview_RejectsDecompressionBomb_ByDeclaredPartSize_AndCapsCellText()
    {
        var o = Org();
        var longText = new string('x', 1500);
        var content = StudentsWorkbook(StudentRow("1", "Maple Elementary", extra: ("HomeLanguage", longText)));
        // Append a 26 MB worksheet part that deflates to a few KB: passes the 5 MB transport cap,
        // fails the per-part decompression budget before ClosedXML ever inflates it.
        byte[] bomb;
        using (var ms = new MemoryStream())
        {
            ms.Write(content);
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
            {
                var entry = zip.CreateEntry("xl/worksheets/sheet9.xml", CompressionLevel.SmallestSize);
                using var stream = entry.Open();
                var block = new byte[1024 * 1024];
                for (var i = 0; i < 26; i++) stream.Write(block);
            }
            bomb = ms.ToArray();
        }
        Assert.True(bomb.Length < 5 * 1024 * 1024);

        using var ctx = _db.Context();
        var rejected = await Roster(ctx).PreviewAsync(o.Admin, Upload(bomb));
        var capped = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));

        Assert.False(rejected.Success);
        Assert.Contains("too large to import", rejected.Message);
        Assert.Equal("HomeLanguage must be 32 characters or fewer.", Assert.Single(capped.Data!.Rows).Message);
        var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(ctx.ImportRows.AsNoTracking().Single().PayloadJson)!;
        Assert.Equal(1000, payload["HomeLanguage"].Length);
    }

    [Fact]
    public async Task Commit_ClaimsTheBatch_SoAnInFlightCommitIsRefused_AndAFailedCommitReleasesIt()
    {
        var o = Org();
        var content = StudentsWorkbook(StudentRow("000123", "Maple Elementary"));
        var cts = new CancellationTokenSource();
        var aborter = new AbortOnStudentInsert(cts);

        using var ctx = _db.Context();
        var preview = await Roster(ctx).PreviewAsync(o.Admin, Upload(content));
        var batchId = preview.Data!.BatchId;
        using (var abortCtx = _db.Context(aborter))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Roster(abortCtx).CommitAsync(o.Admin, batchId, commitValid: false, cts.Token));
        }
        using (var check = _db.Context())
        {
            Assert.Equal(ImportBatchStatus.Previewed, check.ImportBatches.Single(b => b.Id == batchId).Status); // claim released
            Assert.Empty(check.SchoolStudents);                                                                    // work rolled back
            check.ImportBatches.Single(b => b.Id == batchId).Status = ImportBatchStatus.Committing;              // another commit in flight
            check.SaveChanges();
        }
        var refused = await Roster(ctx).CommitAsync(o.Admin, batchId, commitValid: false);

        Assert.False(refused.Success);
        Assert.Equal("This import has already been committed.", refused.Message);
    }

    /// <summary>Cancels the token the moment the commit tries to insert a student — a mid-commit failure.</summary>
    private sealed class AbortOnStudentInsert : DbActivityCounter
    {
        private readonly CancellationTokenSource _cts;
        public AbortOnStudentInsert(CancellationTokenSource cts) => _cts = cts;

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"SchoolStudents\"", StringComparison.Ordinal))
            {
                _cts.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    // ----------------------------------------------------------------- staff import

    [Fact]
    public async Task StaffImport_UpdatesExistingByEmail_InvitesUnknown_AndFlagsBadRole()
    {
        var o = Org();
        var (existingUser, existingProfile) = _db.Staff("Existing@X.com", o.District, o.SchoolA, OrgRoleIds.Teacher, title: "Old title");
        var content = StaffWorkbook(
            new Dictionary<string, object> { ["Email"] = "existing@x.com", ["FirstName"] = "Ex", ["LastName"] = "Isting", ["Role"] = "RelatedServiceProvider", ["SchoolName"] = "Oak Middle", ["Title"] = "SLP" },
            new Dictionary<string, object> { ["Email"] = "new@x.com", ["FirstName"] = "New", ["LastName"] = "Hire", ["Role"] = "GeneralEducator", ["SchoolName"] = "Maple Elementary" },
            new Dictionary<string, object> { ["Email"] = "bad@x.com", ["FirstName"] = "Bad", ["LastName"] = "Role", ["Role"] = "Principal", ["SchoolName"] = "Maple Elementary" });

        using var ctx = _db.Context();
        var preview = await StaffImports(ctx).PreviewAsync(o.Admin, Upload(content, "staff.xlsx"));
        var commit = await StaffImports(ctx).CommitAsync(o.Admin, preview.Data!.BatchId, commitValid: true);

        Assert.Equal(new[] { ImportRowOutcome.Updated, ImportRowOutcome.New, ImportRowOutcome.Error }, preview.Data.Rows.Select(r => r.Outcome));
        Assert.Equal(new[] { "Role: Teacher → RelatedServiceProvider", "School: Maple Elementary → Oak Middle", "Title: Old title → SLP" }, preview.Data.Rows[0].Changes);
        Assert.Contains("Unknown role 'Principal'", preview.Data.Rows[2].Message);
        Assert.Equal((1, 1, 1), (commit.Data!.Committed.New, commit.Data.Committed.Updated, commit.Data.Skipped));
        using var check = _db.Context();
        var profile = check.StaffProfiles.Single(p => p.Id == existingProfile);
        Assert.Equal((OrgRoleIds.RelatedServiceProvider, o.SchoolB, "SLP", existingUser), (profile.OrgRoleId, profile.SchoolId, profile.Title, profile.UserId));
        var invite = Assert.Single(check.StaffInvites);
        Assert.Equal(("new@x.com", OrgRoleIds.GeneralEducator, o.SchoolA), (invite.Email, invite.OrgRoleId, invite.SchoolId));
        Assert.Equal("new@x.com", Assert.Single(_email.StaffInvitesSentTo));
    }

    [Fact]
    public async Task StaffImport_SchoolAdmin_CannotImportDistrictAdminOrOtherSchool()
    {
        var o = Org();
        var (schoolAdmin, _) = _db.Staff("sa@x.com", o.District, o.SchoolA, OrgRoleIds.SchoolAdmin);
        var content = StaffWorkbook(
            new Dictionary<string, object> { ["Email"] = "boss@x.com", ["FirstName"] = "B", ["LastName"] = "Oss", ["Role"] = "DistrictAdmin" },
            new Dictionary<string, object> { ["Email"] = "far@x.com", ["FirstName"] = "F", ["LastName"] = "Ar", ["Role"] = "Teacher", ["SchoolName"] = "Oak Middle" },
            new Dictionary<string, object> { ["Email"] = "ok@x.com", ["FirstName"] = "O", ["LastName"] = "K", ["Role"] = "Teacher", ["SchoolName"] = "Maple Elementary" });

        using var ctx = _db.Context();
        var preview = await StaffImports(ctx).PreviewAsync(schoolAdmin, Upload(content, "staff.xlsx"));

        Assert.Equal("You do not have permission to import a District Admin.", preview.Data!.Rows[0].Message);
        Assert.Equal("You can only import staff for your own school.", preview.Data.Rows[1].Message);
        Assert.Equal(ImportRowOutcome.New, preview.Data.Rows[2].Outcome);
    }

    private sealed class CapturingEmailService : TestSupport.TestEmailServiceBase
    {
        public List<string> StaffInvitesSentTo { get; } = new();

        public override Task SendStaffInviteEmailAsync(string toEmail, string districtName, string? schoolName, string roleName, string inviteToken, CancellationToken ct = default)
        {
            StaffInvitesSentTo.Add(toEmail);
            return Task.CompletedTask;
        }
    }

    public void Dispose() => _db.Dispose();
}
