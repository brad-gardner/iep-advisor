using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Student roster XLSX import (see <see cref="IRosterImportService"/>). Rows match ONLY on
/// <c>(DistrictId, StudentId)</c>; a blank cell keeps the stored value, the literal <c>CLEAR</c> clears a
/// nullable field, and rows absent from the file never exit anyone. Preview evaluates every row against
/// live reference data and stores the parsed cells; commit re-evaluates each stored row (so stale
/// references become skipped errors rather than bad writes) and applies New/Updated rows set-based —
/// chunks of <see cref="CommitChunkSize"/> rows, a handful of queries and two saves per chunk — in one
/// transaction, exactly once per batch (claimed via <see cref="ImportBatchClaim"/>). A row that moves a
/// student between schools runs the same team/access deactivation as a transfer. Also serves batch
/// history / detail / error workbook for both import kinds. Admin-only (DistrictAdmin: district;
/// SchoolAdmin: own school rows only — students of other buildings are invisible to them, so a
/// cross-school id reads as unknown and never discloses a name).
///
/// Multilingual plan (2026-10-06) phase 6: every failure <see cref="Api.Controllers.DistrictImportsController"/>
/// maps to a status carries an explicit <see cref="ServiceErrorKind"/>, matching that controller's
/// PRE-existing English-substring heuristic so status is unchanged. Per-row <see cref="RowEvaluation.Fail"/>
/// messages (stored in <c>ImportRow.Message</c>, shown in the preview table) are localized too, but carry
/// no <see cref="ServiceErrorKind"/> — they never affect HTTP status. CSV column header tokens
/// (<c>StudentId</c>, <c>FirstName</c>, etc. — see <see cref="Columns"/>) stay English in both languages,
/// matching the literal header text in the workbook the user is editing.
///
/// <para>Phase 7: the per-field "Changes" preview lines (<c>eval.Changes</c> — "School: Old → New", etc.)
/// and <see cref="ImportWorkbook"/>'s shared file/sheet-level validation messages (used by
/// <c>StaffImportService</c> too) are now localized as well — only the LABEL before each "→" is
/// translated; the values either side (names, dates, enum display strings) are data, not translated.</para>
///
/// <para><c>ImportRow.Message</c> is written in whichever language is active for the request that wrote
/// it — the uploader's at preview, the committer's at commit (which re-evaluates and overwrites every
/// row's message; see <see cref="CommitClaimedAsync"/>). A batch previewed in one language and committed
/// in another ends up with its stored row messages in the committer's language, not the uploader's.</para>
/// </summary>
public class RosterImportService : IRosterImportService
{
    public const string SheetName = "Students";
    public static readonly IReadOnlyList<string> Columns = new[]
    {
        "StudentId", "SchoolName", "SchoolCode", "FirstName", "LastName", "DateOfBirth", "Grade", "DisabilityCategory",
        "HomeLanguage", "CaseManagerEmail", "IepDate", "AnnualReviewDue", "EtrDate", "ReevaluationDue", "Status"
    };
    private static readonly IReadOnlyList<string[]> RequiredColumns = new[]
    {
        new[] { "StudentId" }, new[] { "SchoolName", "SchoolCode" }, new[] { "FirstName" }, new[] { "LastName" }, new[] { "DateOfBirth" }, new[] { "Grade" }
    };

    /// <summary>Rows applied per unit of work at commit (bounds the change tracker; SaveChanges runs once or twice per chunk).</summary>
    internal const int CommitChunkSize = 500;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAuditLogger _audit;
    private readonly ILogger<RosterImportService> _logger;
    private readonly IStringLocalizer<Messages> _localizer;

    public RosterImportService(ApplicationDbContext context, IOrgAccessService orgAccess, IAuditLogger audit, ILogger<RosterImportService> logger, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _orgAccess = orgAccess;
        _audit = audit;
        _logger = logger;
        _localizer = localizer;
    }

    // ================================================================= Template

    public async Task<ServiceResult<byte[]>> GenerateTemplateAsync(int userId, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<byte[]>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var refs = await LoadReferenceDataAsync(ctx, ct);
        var templateSchools = ctx.OrgRoleId == OrgRoleIds.SchoolAdmin ? refs.Schools.Where(s => s.Id == ctx.SchoolId).ToList() : refs.Schools;
        var managers = refs.Staff
            .Where(s => s.OrgRoleId != OrgRoleIds.DistrictAdmin)
            .Where(s => ctx.OrgRoleId != OrgRoleIds.SchoolAdmin || s.SchoolId == ctx.SchoolId || s.OrgRoleId == OrgRoleIds.RelatedServiceProvider)
            .OrderBy(s => s.Email)
            .ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName);
        var headers = new[]
        {
            "StudentId*", "SchoolName*", "SchoolCode", "FirstName*", "LastName*", "DateOfBirth*", "Grade*", "DisabilityCategory",
            "HomeLanguage", "CaseManagerEmail", "IepDate", "AnnualReviewDue", "EtrDate", "ReevaluationDue", "Status"
        };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).SetValue(headers[c]);
        sheet.Row(1).Style.Font.Bold = true;

        var exampleSchool = templateSchools.FirstOrDefault();
        var exampleManager = managers.FirstOrDefault();
        var examples = new[]
        {
            new[] { "000123", exampleSchool?.Name ?? "Maple Elementary", exampleSchool?.Id.ToString() ?? "", "Jordan", "Ellis", "2015-04-02", "5", "Specific Learning Disability", "en", exampleManager?.Email ?? "", "2026-01-15", "2027-01-14", "2024-03-01", "2027-02-28", "Active" },
            new[] { "000124", exampleSchool?.Name ?? "Maple Elementary", exampleSchool?.Id.ToString() ?? "", "Priya", "Nair", "2013-09-18", "7", "Autism", "es", "", "", "", "", "", "Active" }
        };
        for (var r = 0; r < examples.Length; r++)
            for (var c = 0; c < examples[r].Length; c++)
                sheet.Cell(r + 2, c + 1).SetValue(examples[r][c]);
        // Whole-column text format for ids so typed leading zeros survive; ISO date format for dates.
        sheet.Column(1).Style.NumberFormat.Format = "@";
        sheet.Column(3).Style.NumberFormat.Format = "@";
        foreach (var dateColumn in new[] { 6, 11, 12, 13, 14 })
            sheet.Column(dateColumn).Style.NumberFormat.Format = "@";
        sheet.Columns().AdjustToContents();

        var values = workbook.AddWorksheet("Values");
        ImportWorkbook.WriteValuesColumn(values, 1, "Grade", Enum.GetValues<GradeLevel>().Select(g => g.ToDisplay()));
        ImportWorkbook.WriteValuesColumn(values, 2, "DisabilityCategory", Enum.GetValues<DisabilityCategory>().Select(d => d.ToDisplay()));
        ImportWorkbook.WriteValuesColumn(values, 3, "Status", Enum.GetNames<StudentStatus>());
        ImportWorkbook.WriteValuesColumn(values, 4, "ExitReason", Enum.GetNames<ExitReason>());
        ImportWorkbook.WriteValuesColumn(values, 5, "SchoolName", templateSchools.Select(s => s.Name));
        ImportWorkbook.WriteValuesColumn(values, 6, "SchoolCode", templateSchools.Select(s => s.Id.ToString()));
        ImportWorkbook.WriteValuesColumn(values, 7, "CaseManagerEmail", managers.Select(s => s.Email));
        ImportWorkbook.WriteValuesColumn(values, 8, "CaseManagerName", managers.Select(s => s.Name));
        values.Columns().AdjustToContents();

        return ServiceResult<byte[]>.SuccessResult(ImportWorkbook.ToBytes(workbook));
    }

    // ================================================================= Preview

    public async Task<ServiceResult<ImportPreviewModel>> PreviewAsync(int userId, ImportUploadModel upload, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var rejection = ImportWorkbook.ValidateUpload(upload, _localizer);
        if (rejection != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(ServiceErrorKind.Validation, rejection);

        var (parsedRows, readError) = ImportWorkbook.ReadSheet(upload.Content, SheetName, Columns, RequiredColumns, _localizer);
        if (readError != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(ServiceErrorKind.Validation, readError);
        var sheetRows = parsedRows!;

        var refs = await LoadReferenceDataAsync(ctx, ct);
        var keys = sheetRows.Select(r => r.Get("StudentId").Trim()).Where(k => k.Length > 0).Distinct().ToList();
        var existing = await LoadExistingAsync(ctx, keys, ct);

        var rows = new List<ImportRow>(sheetRows.Count);
        foreach (var sheetRow in sheetRows)
        {
            var eval = Evaluate(ctx, refs, existing, sheetRow.Cells);
            rows.Add(new ImportRow
            {
                RowNumber = sheetRow.RowNumber,
                Outcome = eval.Outcome,
                Key = ImportWorkbook.Truncate(eval.Key, 256),
                DisplayName = ImportWorkbook.Truncate(eval.DisplayName, 256),
                Message = eval.Message == null ? null : ImportWorkbook.Truncate(eval.Message, 1000),
                ChangesJson = ImportWorkbook.SerializeChanges(eval.Changes),
                PayloadJson = ImportWorkbook.SerializePayload(sheetRow.Cells)
            });
        }
        ImportWorkbook.FlagDuplicateKeys(rows, "StudentId", _localizer);

        var batch = new ImportBatch
        {
            DistrictId = ctx.DistrictId,
            Kind = ImportKind.Students,
            FileName = ImportWorkbook.Truncate(Path.GetFileName(upload.FileName), 260),
            Status = ImportBatchStatus.Previewed,
            CreatedById = userId,
            UpdatedById = userId,
            Rows = rows
        };
        ImportWorkbook.SetCounts(batch, rows);
        await _context.ImportBatches.AddAsync(batch, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Student import batch {BatchId} previewed by user {UserId}: {Total} rows ({New} new, {Updated} updated, {Unchanged} unchanged, {Error} errors)",
            batch.Id, userId, batch.TotalCount, batch.NewCount, batch.UpdatedCount, batch.UnchangedCount, batch.ErrorCount);

        return ServiceResult<ImportPreviewModel>.SuccessResult(ImportWorkbook.MapPreview(batch, rows));
    }

    // ================================================================= Commit

    public async Task<ServiceResult<ImportResultModel>> CommitAsync(int userId, int batchId, bool commitValid, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<ImportResultModel>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var batch = await FindBatchAsync(ctx, batchId, ct, track: false);
        if (batch == null)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["RosterImport.ImportNotFound"]);
        if (batch.Kind != ImportKind.Students)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["RosterImport.NotStudentRoster"]);
        if (batch.Status != ImportBatchStatus.Previewed)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["RosterImport.AlreadyCommitted"]);
        if (!commitValid && batch.ErrorCount > 0)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["RosterImport.FixErrorsOrImportValidOnly"]);

        // Claim the batch (Previewed → Committing) before touching any row so an overlapping commit of
        // the same batch is refused rather than racing this one on the external-id index.
        if (!await ImportBatchClaim.TryClaimAsync(_context, batchId, ct))
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["RosterImport.AlreadyCommitted"]);

        try
        {
            return await CommitClaimedAsync(ctx, userId, batchId, ct);
        }
        catch
        {
            await ImportBatchClaim.ReleaseAsync(_context, batchId);
            throw;
        }
    }

    /// <summary>One row to write at commit: its stored row, its evaluation, and the late error (if any).</summary>
    private sealed class PendingRow
    {
        public PendingRow(ImportRow row, RowEvaluation eval) { Row = row; Eval = eval; }
        public ImportRow Row { get; }
        public RowEvaluation Eval { get; }
        public SchoolStudent? Student { get; set; }
        public string? Error { get; set; }
    }

    private async Task<ServiceResult<ImportResultModel>> CommitClaimedAsync(StaffContext ctx, int userId, int batchId, CancellationToken ct)
    {
        var rows = await _context.ImportRows.AsNoTracking().Where(r => r.BatchId == batchId).OrderBy(r => r.RowNumber).ToListAsync(ct);
        var refs = await LoadReferenceDataAsync(ctx, ct);
        var candidateKeys = rows.Where(r => r.Outcome != ImportRowOutcome.Error).Select(r => r.Key).Where(k => k.Length > 0).Distinct().ToList();
        var existing = await LoadExistingAsync(ctx, candidateKeys, ct);

        var committed = new ImportCommittedCountsModel();
        var skipped = 0;
        var pending = new List<PendingRow>();
        var before = rows.ToDictionary(r => r.Id, r => (r.Outcome, r.Message, r.ChangesJson));

        // Pass 1 (no writes): re-evaluate every row against live data — a school renamed or a student
        // changed since preview must not be written blindly. Duplicate keys were flagged at preview, so
        // no two applied rows share a student.
        foreach (var row in rows)
        {
            if (row.Outcome == ImportRowOutcome.Error)
            {
                skipped++;
                continue;
            }
            var eval = Evaluate(ctx, refs, existing, ImportWorkbook.DeserializePayload(row.PayloadJson));
            row.Message = eval.Message == null ? null : ImportWorkbook.Truncate(eval.Message, 1000);
            row.ChangesJson = ImportWorkbook.SerializeChanges(eval.Changes);
            switch (eval.Outcome)
            {
                case ImportRowOutcome.Error:
                    row.Outcome = ImportRowOutcome.Error;
                    skipped++;
                    break;
                case ImportRowOutcome.Unchanged:
                    row.Outcome = ImportRowOutcome.Unchanged;
                    committed.Unchanged++;
                    break;
                default:
                    pending.Add(new PendingRow(row, eval));
                    break;
            }
        }

        // Pass 2: apply in chunks. Each chunk pre-loads its students and their team/access rows, mutates
        // in memory, saves once or twice (StudentTeamBatch), then the tracker is cleared.
        var touchedStudentIds = new List<int>(pending.Count);
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        foreach (var chunk in pending.Chunk(CommitChunkSize))
        {
            await ApplyChunkAsync(ctx, userId, refs, chunk, ct);
            foreach (var p in chunk)
            {
                if (p.Error != null)
                {
                    p.Row.Outcome = ImportRowOutcome.Error;
                    p.Row.Message = p.Error;
                    skipped++;
                    continue;
                }
                p.Row.Outcome = p.Eval.Outcome;
                touchedStudentIds.Add(p.Student!.Id);
                if (p.Eval.Outcome == ImportRowOutcome.New) committed.New++; else committed.Updated++;
            }
            _context.ChangeTracker.Clear();
        }

        // Persist only the rows whose stored outcome/message/changes moved, as batched UPDATEs.
        var dirty = rows.Where(r => before[r.Id] != (r.Outcome, r.Message, r.ChangesJson)).ToList();
        foreach (var chunk in dirty.Chunk(CommitChunkSize))
        {
            foreach (var row in chunk)
            {
                var entry = _context.ImportRows.Attach(row);
                entry.Property(r => r.Outcome).IsModified = true;
                entry.Property(r => r.Message).IsModified = true;
                entry.Property(r => r.ChangesJson).IsModified = true;
            }
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        var batch = await _context.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
        batch.Status = ImportBatchStatus.Committed;
        batch.CommittedAt = DateTime.UtcNow;
        batch.UpdatedById = userId;
        ImportWorkbook.SetCounts(batch, rows);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // Audit only what is durable: the batch, then each student actually written.
        _audit.Record(AuditAction.Edit, userId, "ImportBatch", batch.Id);
        foreach (var studentId in touchedStudentIds)
            _audit.Record(AuditAction.Edit, userId, "SchoolStudent", studentId);
        _logger.LogInformation("Student import batch {BatchId} committed by user {UserId}: {New} new, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped",
            batch.Id, userId, committed.New, committed.Updated, committed.Unchanged, skipped);

        return ServiceResult<ImportResultModel>.SuccessResult(new ImportResultModel
        {
            BatchId = batch.Id,
            Committed = committed,
            Skipped = skipped,
            Status = batch.Status
        });
    }

    // ================================================================= History / detail / errors

    public async Task<ServiceResult<List<ImportBatchModel>>> GetHistoryAsync(int userId, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<List<ImportBatchModel>>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var batches = await ScopedBatches(ctx)
            .OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id)
            .Take(50)
            .Select(b => new
            {
                Batch = b,
                CreatedByName = _context.Users.Where(u => u.Id == b.CreatedById).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()
            })
            .ToListAsync(ct);

        return ServiceResult<List<ImportBatchModel>>.SuccessResult(batches.Select(x => new ImportBatchModel
        {
            BatchId = x.Batch.Id,
            Kind = x.Batch.Kind,
            FileName = x.Batch.FileName,
            Status = x.Batch.Status,
            Counts = ImportWorkbook.MapCounts(x.Batch),
            CreatedAt = x.Batch.CreatedAt,
            CommittedAt = x.Batch.CommittedAt,
            CreatedByName = (x.CreatedByName ?? string.Empty).Trim()
        }).ToList());
    }

    public async Task<ServiceResult<ImportPreviewModel>> GetBatchAsync(int userId, int batchId, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var batch = await FindBatchAsync(ctx, batchId, ct, track: false);
        if (batch == null)
            return ServiceResult<ImportPreviewModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["RosterImport.ImportNotFound"]);

        var rows = await _context.ImportRows.AsNoTracking().Where(r => r.BatchId == batchId).ToListAsync(ct);
        return ServiceResult<ImportPreviewModel>.SuccessResult(ImportWorkbook.MapPreview(batch, rows));
    }

    public async Task<ServiceResult<ImportBatchModel>> GetBatchSummaryAsync(int userId, int batchId, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<ImportBatchModel>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var batch = await FindBatchAsync(ctx, batchId, ct, track: false);
        if (batch == null)
            return ServiceResult<ImportBatchModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["RosterImport.ImportNotFound"]);

        return ServiceResult<ImportBatchModel>.SuccessResult(new ImportBatchModel
        {
            BatchId = batch.Id,
            Kind = batch.Kind,
            FileName = batch.FileName,
            Status = batch.Status,
            Counts = ImportWorkbook.MapCounts(batch),
            CreatedAt = batch.CreatedAt,
            CommittedAt = batch.CommittedAt
        });
    }

    public async Task<ServiceResult<byte[]>> BuildErrorWorkbookAsync(int userId, int batchId, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<byte[]>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var batch = await FindBatchAsync(ctx, batchId, ct, track: false);
        if (batch == null)
            return ServiceResult<byte[]>.FailureResult(ServiceErrorKind.NotFound, _localizer["RosterImport.ImportNotFound"]);

        var errorRows = await _context.ImportRows.AsNoTracking()
            .Where(r => r.BatchId == batchId && r.Outcome == ImportRowOutcome.Error)
            .OrderBy(r => r.RowNumber)
            .ToListAsync(ct);

        var (sheetName, columns) = batch.Kind == ImportKind.Staff
            ? (StaffImportService.SheetName, StaffImportService.Columns)
            : (SheetName, Columns);
        var bytes = ImportWorkbook.BuildErrorWorkbook(sheetName, columns,
            errorRows.Select(r => (ImportWorkbook.DeserializePayload(r.PayloadJson), r.Message ?? "Error")));
        return ServiceResult<byte[]>.SuccessResult(bytes);
    }

    // ================================================================= Row evaluation

    internal sealed record RefSchool(int Id, string Name, string? StateCode);
    internal sealed record RefStaff(int UserId, string Email, string Name, int OrgRoleId, int? SchoolId);

    internal sealed class ReferenceData
    {
        public List<RefSchool> Schools { get; init; } = new();
        public List<RefStaff> Staff { get; init; } = new();
        public string? DistrictStateCode { get; init; }

        public RefSchool? FindSchool(string code, string name)
        {
            if (code.Length > 0)
                return int.TryParse(code, out var id) ? Schools.FirstOrDefault(s => s.Id == id) : null;
            return Schools.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public RefStaff? FindStaff(string email)
            => Staff.FirstOrDefault(s => string.Equals(s.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));

        private readonly Dictionary<int, HashSet<int>> _portableBySchool = new();

        /// <summary>Active staff who may stay on a team after a move to <paramref name="schoolId"/> (transfer rule, in memory).</summary>
        public HashSet<int> PortableUserIds(int schoolId)
        {
            if (!_portableBySchool.TryGetValue(schoolId, out var set))
                _portableBySchool[schoolId] = set = Staff
                    .Where(st => StudentTeamWriter.IsPortable(st.OrgRoleId, st.SchoolId, schoolId))
                    .Select(st => st.UserId)
                    .ToHashSet();
            return set;
        }
    }

    /// <summary>Students already stored for the candidate keys (scope-filtered), plus their active team for the transfer preview line.</summary>
    internal sealed class ExistingStudents
    {
        private readonly Dictionary<string, SchoolStudent> _byKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, List<int>> _activeTeamByStudentId = new();

        public void Add(SchoolStudent student) => _byKey[student.ExternalStudentId!] = student;
        public void AddTeamMember(int studentId, int userId)
        {
            if (!_activeTeamByStudentId.TryGetValue(studentId, out var list))
                _activeTeamByStudentId[studentId] = list = new List<int>();
            list.Add(userId);
        }

        public SchoolStudent? Find(string key) => _byKey.TryGetValue(key, out var s) ? s : null;
        public IReadOnlyList<int> ActiveTeamUserIds(int studentId)
            => _activeTeamByStudentId.TryGetValue(studentId, out var list) ? list : Array.Empty<int>();
    }

    /// <summary>The fully-resolved result of one row: what to write, or why not.</summary>
    internal sealed class RowEvaluation
    {
        private readonly string _sheetDisplayName;

        public RowEvaluation(string sheetDisplayName)
        {
            _sheetDisplayName = sheetDisplayName;
            DisplayName = sheetDisplayName;
        }

        public ImportRowOutcome Outcome { get; set; } = ImportRowOutcome.Unchanged;
        public string Key { get; set; } = string.Empty;

        /// <summary>Sheet-supplied name, or the stored name for a matched row — reset to the sheet's on <see cref="Fail"/>.</summary>
        public string DisplayName { get; set; }
        public string? Message { get; set; }
        public List<string> Changes { get; } = new();

        public SchoolStudent? Existing { get; set; }
        public RefSchool? School { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public GradeLevel? Grade { get; set; }
        public (bool Set, DisabilityCategory? Value) Disability { get; set; }
        public (bool Set, string? Value) HomeLanguage { get; set; }
        public (bool Set, RefStaff? Value) CaseManager { get; set; }
        public (bool Set, DateTime? Value) IepDate { get; set; }
        public (bool Set, DateTime? Value) AnnualReviewDue { get; set; }
        public (bool Set, DateTime? Value) EtrDate { get; set; }
        public (bool Set, DateTime? Value) ReevaluationDue { get; set; }
        public StudentStatus? Status { get; set; }

        public RowEvaluation Fail(string message)
        {
            Outcome = ImportRowOutcome.Error;
            Message = message;
            Changes.Clear();
            DisplayName = _sheetDisplayName; // an error row never echoes a stored name back
            return this;
        }
    }

    internal RowEvaluation Evaluate(StaffContext ctx, ReferenceData refs, ExistingStudents existingStudents, Dictionary<string, string> cells)
    {
        string Cell(string column) => cells.TryGetValue(column, out var v) ? v.Trim() : string.Empty;

        var first = Cell("FirstName");
        var last = Cell("LastName");
        var eval = new RowEvaluation(sheetDisplayName: $"{first} {last}".Trim()) { Key = Cell("StudentId") };

        if (eval.Key.Length == 0)
            return eval.Fail(_localizer["RosterImport.StudentIdRequired"]);
        if (eval.Key.Length > 64)
            return eval.Fail(_localizer["RosterImport.StudentIdTooLong"]);
        if (ImportWorkbook.IsClear(eval.Key))
            return eval.Fail(_localizer["RosterImport.StudentIdCannotBeCleared"]);

        var existing = existingStudents.Find(eval.Key);
        eval.Existing = existing;
        var isNew = existing == null;
        if (eval.DisplayName.Length == 0 && existing != null)
            eval.DisplayName = $"{existing.FirstName} {existing.LastName}".Trim();

        // ---- School (SchoolCode wins over SchoolName when both are present)
        var code = Cell("SchoolCode");
        var schoolName = Cell("SchoolName");
        if (ImportWorkbook.IsClear(code) || ImportWorkbook.IsClear(schoolName))
            return eval.Fail(_localizer["RosterImport.SchoolCannotBeCleared"]);
        if (code.Length > 0 || schoolName.Length > 0)
        {
            var school = refs.FindSchool(code, schoolName);
            if (school == null)
                return eval.Fail(code.Length > 0
                    ? _localizer["RosterImport.UnknownSchoolCode", code]
                    : _localizer["RosterImport.UnknownSchoolName", schoolName]);
            eval.School = school;
        }
        else if (isNew)
        {
            return eval.Fail(_localizer["RosterImport.SchoolRequiredForNewStudent"]);
        }

        // A SchoolAdmin may only touch rows for their own building — both the target school and the
        // student's current school must be theirs.
        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            var own = refs.Schools.FirstOrDefault(s => s.Id == ctx.SchoolId);
            if (own == null)
                return eval.Fail(_localizer["RosterImport.CallerNoSchool"]);
            if ((eval.School != null && eval.School.Id != own.Id) || (existing != null && existing.SchoolId != own.Id))
                return eval.Fail(_localizer["RosterImport.OnlyImportForSchool", own.Name]);
        }

        // ---- Names
        if (ImportWorkbook.IsClear(first)) return eval.Fail(_localizer["RosterImport.FirstNameCannotBeCleared"]);
        if (ImportWorkbook.IsClear(last)) return eval.Fail(_localizer["RosterImport.LastNameCannotBeCleared"]);
        if (first.Length > 100 || last.Length > 100) return eval.Fail(_localizer["RosterImport.NamesTooLong"]);
        if (isNew && first.Length == 0) return eval.Fail(_localizer["RosterImport.FirstNameRequiredForNewStudent"]);
        if (isNew && last.Length == 0) return eval.Fail(_localizer["RosterImport.LastNameRequiredForNewStudent"]);
        eval.FirstName = first.Length > 0 ? first : null;
        eval.LastName = last.Length > 0 ? last : null;

        // ---- Date of birth
        var dobText = Cell("DateOfBirth");
        if (ImportWorkbook.IsClear(dobText)) return eval.Fail(_localizer["RosterImport.DateOfBirthCannotBeCleared"]);
        var (dob, dobOk) = ImportWorkbook.ParseDate(dobText);
        if (!dobOk) return eval.Fail(_localizer["RosterImport.UnrecognizedDateOfBirth", dobText]);
        if (isNew && dob == null) return eval.Fail(_localizer["RosterImport.DateOfBirthRequiredForNewStudent"]);
        eval.DateOfBirth = dob;

        // ---- Grade
        var gradeText = Cell("Grade");
        if (ImportWorkbook.IsClear(gradeText)) return eval.Fail(_localizer["RosterImport.GradeCannotBeCleared"]);
        if (gradeText.Length > 0)
        {
            var grade = GradeLevelExtensions.TryParseDisplay(gradeText);
            if (grade == null) return eval.Fail(_localizer["RosterImport.UnknownGrade", gradeText]);
            eval.Grade = grade;
        }
        else if (isNew)
        {
            return eval.Fail(_localizer["RosterImport.GradeRequiredForNewStudent"]);
        }

        // ---- Disability
        var disabilityText = Cell("DisabilityCategory");
        if (ImportWorkbook.IsClear(disabilityText)) eval.Disability = (true, null);
        else if (disabilityText.Length > 0)
        {
            var category = DisabilityCategoryExtensions.TryParseDisplay(disabilityText);
            if (category == null) return eval.Fail(_localizer["RosterImport.UnknownDisabilityCategory", disabilityText]);
            eval.Disability = (true, category);
        }

        // ---- Home language
        var language = Cell("HomeLanguage");
        if (ImportWorkbook.IsClear(language)) eval.HomeLanguage = (true, null);
        else if (language.Length > 0)
        {
            if (language.Length > 32) return eval.Fail(_localizer["RosterImport.HomeLanguageTooLong"]);
            eval.HomeLanguage = (true, language);
        }

        // ---- Case manager
        var managerEmail = Cell("CaseManagerEmail");
        if (ImportWorkbook.IsClear(managerEmail)) eval.CaseManager = (true, null);
        else if (managerEmail.Length > 0)
        {
            var staff = refs.FindStaff(managerEmail);
            // A SchoolAdmin sees one neutral refusal for any email outside their eligible pool (own
            // building + RelatedServiceProviders) so the importer is not a staff-directory oracle.
            if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin
                && (staff == null || staff.OrgRoleId == OrgRoleIds.DistrictAdmin
                    || (staff.OrgRoleId != OrgRoleIds.RelatedServiceProvider && staff.SchoolId != ctx.SchoolId)))
                return eval.Fail(_localizer["RosterImport.CaseManagerNotAvailable", managerEmail]);
            if (staff == null) return eval.Fail(_localizer["RosterImport.CaseManagerNotActiveStaff", managerEmail]);
            if (staff.OrgRoleId == OrgRoleIds.DistrictAdmin) return eval.Fail(_localizer["RosterImport.CaseManagerIsDistrictAdmin", managerEmail]);
            var targetSchoolId = eval.School?.Id ?? existing?.SchoolId;
            if (staff.OrgRoleId != OrgRoleIds.RelatedServiceProvider && targetSchoolId != null && staff.SchoolId != targetSchoolId)
                return eval.Fail(_localizer["RosterImport.CaseManagerNotAtStudentSchool", managerEmail]);
            eval.CaseManager = (true, staff);
        }

        // ---- Timeline dates
        var dateError = ParseOptionalDate(Cell("IepDate"), "IepDate", v => eval.IepDate = v)
                        ?? ParseOptionalDate(Cell("AnnualReviewDue"), "AnnualReviewDue", v => eval.AnnualReviewDue = v)
                        ?? ParseOptionalDate(Cell("EtrDate"), "EtrDate", v => eval.EtrDate = v)
                        ?? ParseOptionalDate(Cell("ReevaluationDue"), "ReevaluationDue", v => eval.ReevaluationDue = v);
        if (dateError != null) return eval.Fail(dateError);

        // ---- Status
        var statusText = Cell("Status");
        if (ImportWorkbook.IsClear(statusText)) return eval.Fail(_localizer["RosterImport.StatusCannotBeCleared"]);
        if (statusText.Length > 0)
        {
            if (!Enum.TryParse<StudentStatus>(statusText, ignoreCase: true, out var status))
                return eval.Fail(_localizer["RosterImport.UnknownStatus", statusText]);
            eval.Status = status;
        }

        if (isNew)
        {
            eval.Outcome = ImportRowOutcome.New;
            return eval;
        }

        // ---- Diff against the stored record (blank = keep, so only SET values can differ)
        var s = existing!;
        // Multilingual plan phase 7: the LABEL before each "→" is localized; names/dates/enum display
        // strings either side of it are data, not translated.
        if (eval.School != null && eval.School.Id != s.SchoolId)
        {
            eval.Changes.Add($"{_localizer["RosterImport.FieldSchool"]}: {refs.Schools.FirstOrDefault(x => x.Id == s.SchoolId)?.Name ?? s.SchoolId.ToString()} → {eval.School.Name}");
            var newSchoolId = eval.School.Id;
            var portable = refs.PortableUserIds(newSchoolId);
            var leaving = existingStudents.ActiveTeamUserIds(s.Id).Count(u => !portable.Contains(u));
            if (leaving > 0)
                eval.Changes.Add(string.Format(_localizer["RosterImport.TeamMembersWillBeDeactivated"].Value, leaving));
        }
        if (eval.FirstName != null && eval.FirstName != s.FirstName)
            eval.Changes.Add($"{_localizer["RosterImport.FieldFirstName"]}: {s.FirstName} → {eval.FirstName}");
        if (eval.LastName != null && eval.LastName != (s.LastName ?? string.Empty))
            eval.Changes.Add($"{_localizer["RosterImport.FieldLastName"]}: {s.LastName} → {eval.LastName}");
        if (eval.DateOfBirth != null && eval.DateOfBirth != s.DateOfBirth?.Date)
            eval.Changes.Add($"{_localizer["RosterImport.FieldDateOfBirth"]}: {ImportWorkbook.FormatDate(s.DateOfBirth)} → {ImportWorkbook.FormatDate(eval.DateOfBirth)}");
        if (eval.Grade != null && eval.Grade != s.GradeLevel)
            eval.Changes.Add($"{_localizer["RosterImport.FieldGrade"]}: {s.GradeLevel?.ToDisplay()} → {eval.Grade.Value.ToDisplay()}");
        if (eval.Disability.Set && eval.Disability.Value != s.DisabilityCategory)
            eval.Changes.Add($"{_localizer["RosterImport.FieldDisability"]}: {s.DisabilityCategory?.ToDisplay()} → {eval.Disability.Value?.ToDisplay()}");
        if (eval.HomeLanguage.Set && !string.Equals(eval.HomeLanguage.Value, s.HomeLanguage, StringComparison.Ordinal))
            eval.Changes.Add($"{_localizer["RosterImport.FieldHomeLanguage"]}: {s.HomeLanguage} → {eval.HomeLanguage.Value}");
        if (eval.CaseManager.Set && eval.CaseManager.Value?.UserId != s.CaseManagerUserId)
        {
            var current = refs.Staff.FirstOrDefault(x => x.UserId == s.CaseManagerUserId)?.Email ?? (s.CaseManagerUserId == null ? "" : "(other)");
            eval.Changes.Add($"{_localizer["RosterImport.FieldCaseManager"]}: {current} → {eval.CaseManager.Value?.Email}");
        }
        DiffDate(eval, _localizer["RosterImport.FieldIepDate"], s.IepDate, eval.IepDate);
        DiffDate(eval, _localizer["RosterImport.FieldAnnualReviewDue"], s.AnnualReviewDueDate, eval.AnnualReviewDue);
        DiffDate(eval, _localizer["RosterImport.FieldEtrDate"], s.EtrDate, eval.EtrDate);
        DiffDate(eval, _localizer["RosterImport.FieldReevaluationDue"], s.ReevaluationDueDate, eval.ReevaluationDue);
        if (eval.Status != null && eval.Status != s.Status)
            eval.Changes.Add($"{_localizer["RosterImport.FieldStatus"]}: {s.Status} → {eval.Status}");

        eval.Outcome = eval.Changes.Count > 0 ? ImportRowOutcome.Updated : ImportRowOutcome.Unchanged;
        return eval;
    }

    private string? ParseOptionalDate(string text, string column, Action<(bool, DateTime?)> assign)
    {
        if (ImportWorkbook.IsClear(text)) { assign((true, null)); return null; }
        if (text.Length == 0) return null;
        var (value, ok) = ImportWorkbook.ParseDate(text);
        if (!ok) return _localizer["RosterImport.UnrecognizedDate", column, text];
        assign((true, value));
        return null;
    }

    private static void DiffDate(RowEvaluation eval, string label, DateTime? current, (bool Set, DateTime? Value) incoming)
    {
        if (incoming.Set && incoming.Value != current?.Date)
            eval.Changes.Add($"{label}: {ImportWorkbook.FormatDate(current)} → {ImportWorkbook.FormatDate(incoming.Value)}");
    }

    /// <summary>
    /// Applies one chunk of New/Updated evaluations. On an external-id collision (a student created
    /// since preview) the chunk's statements are rolled back to EF's savepoint and re-applied one row
    /// at a time so the duplicate lands on exactly its row; any other DbUpdateException propagates and
    /// fails the whole commit (the batch stays Previewed).
    /// </summary>
    private async Task ApplyChunkAsync(StaffContext ctx, int userId, ReferenceData refs, IReadOnlyList<PendingRow> chunk, CancellationToken ct)
    {
        try
        {
            await ApplyChunkCoreAsync(ctx, userId, refs, chunk, ct);
        }
        catch (DbUpdateException ex) when (EducatorService.IsExternalIdCollision(ex))
        {
            _context.ChangeTracker.Clear();
            if (chunk.Count == 1)
            {
                chunk[0].Error = _localizer["Educator.DuplicateExternalId"];
                chunk[0].Student = null;
                return;
            }
            foreach (var one in chunk)
            {
                one.Error = null;
                one.Student = null;
                await ApplyChunkAsync(ctx, userId, refs, new[] { one }, ct);
                _context.ChangeTracker.Clear();
            }
        }
    }

    private async Task ApplyChunkCoreAsync(StaffContext ctx, int userId, ReferenceData refs, IReadOnlyList<PendingRow> chunk, CancellationToken ct)
    {
        // The evaluation holds AsNoTracking copies; the writes go through tracked entities loaded here.
        var existingIds = chunk.Where(p => p.Eval.Existing != null).Select(p => p.Eval.Existing!.Id).Distinct().ToList();
        var tracked = existingIds.Count == 0
            ? new Dictionary<int, SchoolStudent>()
            : await _context.SchoolStudents.Where(st => existingIds.Contains(st.Id)).ToDictionaryAsync(st => st.Id, ct);

        var schoolMoves = new List<(PendingRow Row, RefSchool NewSchool)>();
        foreach (var p in chunk)
        {
            var eval = p.Eval;
            if (eval.Existing == null)
            {
                p.Student = BuildNewStudent(ctx, userId, refs, eval);
                _context.SchoolStudents.Add(p.Student);
                continue;
            }
            if (!tracked.TryGetValue(eval.Existing.Id, out var student))
            {
                p.Error = _localizer["RosterImport.StudentNoLongerExists"];
                continue;
            }
            p.Student = student;
            if (eval.School != null && eval.School.Id != student.SchoolId)
                schoolMoves.Add((p, eval.School));
            ApplyUpdate(userId, refs, eval, student);
        }

        var team = await StudentTeamBatch.LoadAsync(_context, tracked.Values.ToList(), userId, ct);
        foreach (var (p, newSchool) in schoolMoves)
            team.DeactivateNonPortable(p.Student!, refs.PortableUserIds(newSchool.Id), EducatorService.TransferNote(newSchool.Name));
        foreach (var p in chunk)
        {
            if (p.Error != null || !p.Eval.CaseManager.Set)
                continue;
            var manager = p.Eval.CaseManager.Value;
            if (manager == null)
                team.ClearLead(p.Student!);
            else if (p.Student!.CaseManagerUserId != manager.UserId)
                team.AssignLead(p.Student, manager.UserId, TeamRole.CaseManager, accessOverride: null);
        }
        await team.SaveAsync(ct);
    }

    private static SchoolStudent BuildNewStudent(StaffContext ctx, int userId, ReferenceData refs, RowEvaluation eval)
    {
        var school = eval.School!;
        var student = new SchoolStudent
        {
            SchoolId = school.Id,
            DistrictId = ctx.DistrictId,
            ExternalStudentId = eval.Key,
            FirstName = eval.FirstName!,
            LastName = eval.LastName,
            DateOfBirth = eval.DateOfBirth,
            StateCode = school.StateCode ?? refs.DistrictStateCode,
            GradeLevel = eval.Grade,
            DisabilityCategory = eval.Disability.Set ? eval.Disability.Value : null,
            HomeLanguage = eval.HomeLanguage.Set ? eval.HomeLanguage.Value : "en",
            IepDate = eval.IepDate.Value,
            AnnualReviewDueDate = eval.AnnualReviewDue.Value,
            EtrDate = eval.EtrDate.Value,
            ReevaluationDueDate = eval.ReevaluationDue.Value,
            Status = eval.Status ?? StudentStatus.Active,
            CreatedById = userId,
            UpdatedById = userId
        };
        if (student.Status == StudentStatus.Exited)
        {
            student.ExitedAt = DateTime.UtcNow;
            student.ExitReason = ExitReason.Other;
        }
        return student;
    }

    private static void ApplyUpdate(int userId, ReferenceData refs, RowEvaluation eval, SchoolStudent student)
    {
        if (eval.School != null && eval.School.Id != student.SchoolId)
        {
            var oldSchool = refs.Schools.FirstOrDefault(x => x.Id == student.SchoolId);
            var oldState = oldSchool?.StateCode ?? refs.DistrictStateCode;
            if (student.StateCode == null || string.Equals(student.StateCode, oldState, StringComparison.OrdinalIgnoreCase))
                student.StateCode = eval.School.StateCode ?? refs.DistrictStateCode;
            student.SchoolId = eval.School.Id;
        }
        if (eval.FirstName != null) student.FirstName = eval.FirstName;
        if (eval.LastName != null) student.LastName = eval.LastName;
        if (eval.DateOfBirth != null) student.DateOfBirth = eval.DateOfBirth;
        if (eval.Grade != null) student.GradeLevel = eval.Grade;
        if (eval.Disability.Set)
        {
            student.DisabilityCategory = eval.Disability.Value;
            if (eval.Disability.Value != DisabilityCategory.Other) student.LegacyDisabilityText = null;
        }
        if (eval.HomeLanguage.Set) student.HomeLanguage = eval.HomeLanguage.Value;
        if (eval.IepDate.Set) student.IepDate = eval.IepDate.Value;
        if (eval.AnnualReviewDue.Set) student.AnnualReviewDueDate = eval.AnnualReviewDue.Value;
        if (eval.EtrDate.Set) student.EtrDate = eval.EtrDate.Value;
        if (eval.ReevaluationDue.Set) student.ReevaluationDueDate = eval.ReevaluationDue.Value;
        if (eval.Status != null && eval.Status != student.Status)
        {
            student.Status = eval.Status.Value;
            if (eval.Status == StudentStatus.Exited)
            {
                student.ExitedAt = DateTime.UtcNow;
                student.ExitReason = ExitReason.Other;
            }
            else if (eval.Status == StudentStatus.Active)
            {
                student.ExitedAt = null;
                student.ExitReason = null;
            }
        }
        student.UpdatedById = userId;
    }

    // ================================================================= Data access helpers

    private async Task<(StaffContext? Ctx, string? Denied, ServiceErrorKind Kind)> RequireAdminAsync(int userId, CancellationToken ct)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return (null, _localizer["RosterImport.ProfileNotFound"], ServiceErrorKind.NotFound);
        if (!OrgRoleIds.IsAdmin(ctx.OrgRoleId))
            return (null, _localizer["RosterImport.NoPermissionImportStudents"], ServiceErrorKind.Forbidden);
        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin && ctx.SchoolId == null)
            return (null, _localizer["RosterImport.CallerNoSchool"], ServiceErrorKind.Validation);
        return (ctx, null, ServiceErrorKind.None);
    }

    private IQueryable<ImportBatch> ScopedBatches(StaffContext ctx)
    {
        var query = _context.ImportBatches.AsNoTracking().Where(b => b.DistrictId == ctx.DistrictId);
        // SchoolAdmins see only the batches they uploaded; DistrictAdmins see the whole district's history.
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            query = query.Where(b => b.CreatedById == ctx.UserId);
        return query;
    }

    private async Task<ImportBatch?> FindBatchAsync(StaffContext ctx, int batchId, CancellationToken ct, bool track = true)
    {
        var query = _context.ImportBatches.Where(b => b.Id == batchId && b.DistrictId == ctx.DistrictId);
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            query = query.Where(b => b.CreatedById == ctx.UserId);
        if (!track)
            query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(ct);
    }

    internal async Task<ReferenceData> LoadReferenceDataAsync(StaffContext ctx, CancellationToken ct)
    {
        var districtState = await _context.Districts.AsNoTracking()
            .Where(d => d.Id == ctx.DistrictId).Select(d => d.StateCode).FirstOrDefaultAsync(ct);

        // Every active school / staff member of the district is loaded for LOOKUP (so a SchoolAdmin's
        // cross-school row gets the specific "only your school" error); the template lists are narrowed
        // to the caller's scope by the callers.
        var schools = await _context.Schools.AsNoTracking()
            .Where(s => s.DistrictId == ctx.DistrictId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new RefSchool(s.Id, s.Name, s.StateCode)).ToListAsync(ct);

        var staff = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.DistrictId == ctx.DistrictId && p.IsActive)
            .Select(p => new RefStaff(p.UserId, p.User.Email, (p.User.FirstName + " " + p.User.LastName).Trim(), p.OrgRoleId, p.SchoolId))
            .ToListAsync(ct);

        return new ReferenceData { Schools = schools, Staff = staff, DistrictStateCode = districtState };
    }

    /// <summary>
    /// Existing students keyed by ExternalStudentId (any status) for the given keys, plus their active
    /// team user ids. Scope: the district for a DistrictAdmin; ONLY the caller's school for a SchoolAdmin,
    /// so a student of another building is indistinguishable from an unknown id (no name disclosure).
    /// </summary>
    private async Task<ExistingStudents> LoadExistingAsync(StaffContext ctx, List<string> keys, CancellationToken ct)
    {
        var result = new ExistingStudents();
        var districtId = ctx.DistrictId;
        foreach (var chunk in keys.Chunk(500))
        {
            var query = _context.SchoolStudents.AsNoTracking()
                .Where(s => s.DistrictId == districtId && s.ExternalStudentId != null && chunk.Contains(s.ExternalStudentId));
            if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
            {
                var schoolId = ctx.SchoolId!.Value;
                query = query.Where(s => s.SchoolId == schoolId);
            }
            var students = await query.ToListAsync(ct);
            if (students.Count == 0)
                continue;
            foreach (var s in students)
                result.Add(s);

            var ids = students.Select(s => s.Id).ToList();
            var members = await _context.StudentTeamMembers.AsNoTracking()
                .Where(m => ids.Contains(m.SchoolStudentId) && m.IsActive)
                .Select(m => new { m.SchoolStudentId, m.UserId })
                .ToListAsync(ct);
            foreach (var m in members)
                result.AddTeamMember(m.SchoolStudentId, m.UserId);
        }
        return result;
    }
}
