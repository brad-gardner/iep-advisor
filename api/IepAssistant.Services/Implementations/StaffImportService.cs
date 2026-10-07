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
/// Staff XLSX import (see <see cref="IStaffImportService"/>): the roster pipeline over a <c>Staff</c>
/// sheet. Rows match on email (case-insensitive) against staff of the caller's district; matches get
/// role/school/title updates (scope rules mirror <c>StaffInviteService</c>: a SchoolAdmin only touches
/// non-admin staff of their own school), unknown emails become invites through
/// <see cref="IStaffInviteService.InviteAsync"/> on commit. Batch history/detail/errors are served by
/// <see cref="IRosterImportService"/> for both kinds.
///
/// Multilingual plan (2026-10-06) phase 6: every failure <see cref="Api.Controllers.DistrictImportsController"/>
/// maps to a status carries an explicit <see cref="ServiceErrorKind"/>, matching that controller's
/// PRE-existing English-substring heuristic so status is unchanged. Per-row evaluation/update messages
/// (stored in <c>ImportRow.Message</c>, shown in the preview table — see <see cref="Evaluate"/> and
/// <see cref="ApplyUpdateAsync"/>) are localized too, but carry no <see cref="ServiceErrorKind"/> — they
/// never affect HTTP status. <c>ImportRow.Message</c> is written in whichever language is active for the
/// request that wrote it — the uploader's at preview, the committer's at commit (which re-evaluates and
/// overwrites every row's message). CSV column header tokens (<c>Email</c>, <c>FirstName</c>, etc. — see
/// <see cref="Columns"/>) and the <c>Role</c> cell's accepted values (<c>DistrictAdmin</c>,
/// <c>SchoolAdmin</c>, <c>Teacher</c>, <c>RelatedServiceProvider</c>, <c>GeneralEducator</c> — see
/// <see cref="OrgRoleIds.FromName"/>) stay English in both languages: they are literal tokens the parser
/// matches, not prose.
/// </summary>
public class StaffImportService : IStaffImportService
{
    public const string SheetName = "Staff";
    public static readonly IReadOnlyList<string> Columns = new[] { "Email", "FirstName", "LastName", "Role", "SchoolName", "Title" };
    private static readonly IReadOnlyList<string[]> RequiredColumns = new[]
    {
        new[] { "Email" }, new[] { "FirstName" }, new[] { "LastName" }, new[] { "Role" }
    };

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IStaffInviteService _staffInvites;
    private readonly IAuditLogger _audit;
    private readonly ILogger<StaffImportService> _logger;
    private readonly IStringLocalizer<Messages> _localizer;

    private LocalizedString PermissionMessage => _localizer["StaffImport.NoPermissionImportStaff"];

    public StaffImportService(ApplicationDbContext context, IOrgAccessService orgAccess, IStaffInviteService staffInvites, IAuditLogger audit, ILogger<StaffImportService> logger, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _orgAccess = orgAccess;
        _staffInvites = staffInvites;
        _audit = audit;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<ServiceResult<byte[]>> GenerateTemplateAsync(int userId, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<byte[]>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var schools = await LoadSchoolsAsync(ctx, ct);
        var templateSchools = ctx.OrgRoleId == OrgRoleIds.SchoolAdmin ? schools.Where(s => s.Id == ctx.SchoolId).ToList() : schools;
        var roles = ctx.OrgRoleId == OrgRoleIds.DistrictAdmin
            ? OrgRoleIds.All
            : OrgRoleIds.All.Where(r => r != OrgRoleIds.DistrictAdmin).ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(SheetName);
        var headers = new[] { "Email*", "FirstName*", "LastName*", "Role*", "SchoolName", "Title" };
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).SetValue(headers[c]);
        sheet.Row(1).Style.Font.Bold = true;
        var exampleSchool = templateSchools.FirstOrDefault()?.Name ?? "Maple Elementary";
        var examples = new[]
        {
            new[] { "s.case@district.org", "Steph", "Case", "Teacher", exampleSchool, "Intervention Specialist" },
            new[] { "r.diaz@district.org", "Rosa", "Diaz", "RelatedServiceProvider", exampleSchool, "Speech-Language Pathologist" }
        };
        for (var r = 0; r < examples.Length; r++)
            for (var c = 0; c < examples[r].Length; c++)
                sheet.Cell(r + 2, c + 1).SetValue(examples[r][c]);
        sheet.Columns().AdjustToContents();

        var values = workbook.AddWorksheet("Values");
        ImportWorkbook.WriteValuesColumn(values, 1, "Role", roles.Select(r => OrgRoleIds.NameOf(r)!));
        ImportWorkbook.WriteValuesColumn(values, 2, "SchoolName", templateSchools.Select(s => s.Name));
        values.Columns().AdjustToContents();

        return ServiceResult<byte[]>.SuccessResult(ImportWorkbook.ToBytes(workbook));
    }

    public async Task<ServiceResult<ImportPreviewModel>> PreviewAsync(int userId, ImportUploadModel upload, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var rejection = ImportWorkbook.ValidateUpload(upload);
        if (rejection != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(ServiceErrorKind.Validation, rejection);

        var (parsedRows, readError) = ImportWorkbook.ReadSheet(upload.Content, SheetName, Columns, RequiredColumns);
        if (readError != null)
            return ServiceResult<ImportPreviewModel>.FailureResult(ServiceErrorKind.Validation, readError);
        var sheetRows = parsedRows!;

        var refs = await LoadReferenceAsync(ctx, sheetRows.Select(r => r.Get("Email")), ct);
        var rows = new List<ImportRow>(sheetRows.Count);
        foreach (var sheetRow in sheetRows)
        {
            var eval = Evaluate(ctx, refs, sheetRow.Cells);
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
        ImportWorkbook.FlagDuplicateKeys(rows, "Email");

        var batch = new ImportBatch
        {
            DistrictId = ctx.DistrictId,
            Kind = ImportKind.Staff,
            FileName = ImportWorkbook.Truncate(Path.GetFileName(upload.FileName), 260),
            Status = ImportBatchStatus.Previewed,
            CreatedById = userId,
            UpdatedById = userId,
            Rows = rows
        };
        ImportWorkbook.SetCounts(batch, rows);
        await _context.ImportBatches.AddAsync(batch, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Staff import batch {BatchId} previewed by user {UserId}: {Total} rows ({New} new, {Updated} updated, {Unchanged} unchanged, {Error} errors)",
            batch.Id, userId, batch.TotalCount, batch.NewCount, batch.UpdatedCount, batch.UnchangedCount, batch.ErrorCount);

        return ServiceResult<ImportPreviewModel>.SuccessResult(ImportWorkbook.MapPreview(batch, rows));
    }

    public async Task<ServiceResult<ImportResultModel>> CommitAsync(int userId, int batchId, bool commitValid, CancellationToken ct = default)
    {
        var (ctxOrNull, denied, deniedKind) = await RequireAdminAsync(userId, ct);
        if (denied != null)
            return ServiceResult<ImportResultModel>.FailureResult(deniedKind, denied);
        var ctx = ctxOrNull!;

        var batchQuery = _context.ImportBatches.Where(b => b.Id == batchId && b.DistrictId == ctx.DistrictId);
        if (ctx.OrgRoleId != OrgRoleIds.DistrictAdmin)
            batchQuery = batchQuery.Where(b => b.CreatedById == ctx.UserId);
        var batch = await batchQuery.FirstOrDefaultAsync(ct);
        if (batch == null)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["StaffImport.ImportNotFound"]);
        if (batch.Kind != ImportKind.Staff)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["StaffImport.NotStaffList"]);
        if (batch.Status != ImportBatchStatus.Previewed)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["StaffImport.AlreadyCommitted"]);
        if (!commitValid && batch.ErrorCount > 0)
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["StaffImport.FixErrorsOrImportValidOnly"]);
        if (!await ImportBatchClaim.TryClaimAsync(_context, batchId, ct))
            return ServiceResult<ImportResultModel>.FailureResult(ServiceErrorKind.Validation, _localizer["StaffImport.AlreadyCommitted"]);
        batch.Status = ImportBatchStatus.Committing; // mirror the claim on the tracked copy

        try
        {
            return await CommitClaimedAsync(ctx, userId, batch, commitValid, ct);
        }
        catch
        {
            await ImportBatchClaim.ReleaseAsync(_context, batchId);
            throw;
        }
    }

    private async Task<ServiceResult<ImportResultModel>> CommitClaimedAsync(StaffContext ctx, int userId, ImportBatch batch, bool commitValid, CancellationToken ct)
    {
        var batchId = batch.Id;
        var rows = await _context.ImportRows.Where(r => r.BatchId == batchId).OrderBy(r => r.RowNumber).ToListAsync(ct);
        var payloads = rows.ToDictionary(r => r.Id, r => ImportWorkbook.DeserializePayload(r.PayloadJson));
        var refs = await LoadReferenceAsync(ctx, payloads.Values.Select(p => p.TryGetValue("Email", out var e) ? e : ""), ct);

        var committed = new ImportCommittedCountsModel();
        var skipped = 0;
        var invites = new List<(ImportRow Row, RowEvaluation Eval)>();

        // Pass 1 (one transaction): profile updates. Pass 2: invites — each InviteAsync saves + emails on
        // its own, so they run after the updates are durable rather than inside the transaction.
        await using (var tx = await _context.Database.BeginTransactionAsync(ct))
        {
            foreach (var row in rows)
            {
                if (row.Outcome == ImportRowOutcome.Error) { skipped++; continue; }

                var eval = Evaluate(ctx, refs, payloads[row.Id]);
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
                    case ImportRowOutcome.Updated:
                    {
                        var error = await ApplyUpdateAsync(userId, eval, ct);
                        if (error != null) { row.Outcome = ImportRowOutcome.Error; row.Message = error; skipped++; }
                        else { row.Outcome = ImportRowOutcome.Updated; committed.Updated++; }
                        break;
                    }
                    case ImportRowOutcome.New:
                        invites.Add((row, eval));
                        break;
                }
            }
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        foreach (var (row, eval) in invites)
        {
            var invite = await _staffInvites.InviteAsync(userId, new CreateStaffInviteModel
            {
                Email = eval.Key,
                OrgRoleId = eval.OrgRoleId!.Value,
                SchoolId = eval.School?.Id
            }, ct);
            if (invite.Success) { row.Outcome = ImportRowOutcome.New; committed.New++; }
            else { row.Outcome = ImportRowOutcome.Error; row.Message = invite.Message; skipped++; }
        }

        batch.Status = ImportBatchStatus.Committed;
        batch.CommittedAt = DateTime.UtcNow;
        batch.UpdatedById = userId;
        ImportWorkbook.SetCounts(batch, rows);
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "ImportBatch", batch.Id);
        _logger.LogInformation("Staff import batch {BatchId} committed by user {UserId}: {New} invited, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped",
            batch.Id, userId, committed.New, committed.Updated, committed.Unchanged, skipped);

        return ServiceResult<ImportResultModel>.SuccessResult(new ImportResultModel
        {
            BatchId = batch.Id,
            Committed = committed,
            Skipped = skipped,
            Status = batch.Status
        });
    }

    // ================================================================= Evaluation

    internal sealed record RefSchool(int Id, string Name);
    internal sealed record ExistingStaff(int ProfileId, int UserId, string Email, int OrgRoleId, int? SchoolId, string? Title, bool IsActive);

    internal sealed class ReferenceData
    {
        public List<RefSchool> Schools { get; init; } = new();
        public Dictionary<string, ExistingStaff> StaffByEmail { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OtherAccountEmails { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PendingInviteEmails { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public int ActiveDistrictAdminCount { get; init; }
    }

    internal sealed class RowEvaluation
    {
        public ImportRowOutcome Outcome { get; set; } = ImportRowOutcome.Unchanged;
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Message { get; set; }
        public List<string> Changes { get; } = new();
        public ExistingStaff? Existing { get; set; }
        public int? OrgRoleId { get; set; }
        public RefSchool? School { get; set; }
        public (bool Set, string? Value) Title { get; set; }

        public RowEvaluation Fail(string message)
        {
            Outcome = ImportRowOutcome.Error;
            Message = message;
            Changes.Clear();
            return this;
        }
    }

    internal RowEvaluation Evaluate(StaffContext ctx, ReferenceData refs, Dictionary<string, string> cells)
    {
        string Cell(string column) => cells.TryGetValue(column, out var v) ? v.Trim() : string.Empty;

        var eval = new RowEvaluation { Key = Cell("Email") };
        eval.DisplayName = $"{Cell("FirstName")} {Cell("LastName")}".Trim();
        if (eval.Key.Length == 0) return eval.Fail(_localizer["StaffImport.EmailRequired"]);
        if (eval.Key.Length > 256 || !eval.Key.Contains('@') || eval.Key.Contains(' ')) return eval.Fail(_localizer["StaffImport.InvalidEmail", eval.Key]);

        refs.StaffByEmail.TryGetValue(eval.Key, out var existing);
        eval.Existing = existing;

        var roleText = Cell("Role");
        if (roleText.Length == 0) return eval.Fail(_localizer["StaffImport.RoleRequired"]);
        var roleId = OrgRoleIds.FromName(roleText);
        if (roleId == null) return eval.Fail(_localizer["StaffImport.UnknownRole", roleText]);
        eval.OrgRoleId = roleId;

        var schoolName = Cell("SchoolName");
        if (ImportWorkbook.IsClear(schoolName)) return eval.Fail(_localizer["StaffImport.SchoolNameCannotBeCleared"]);
        if (roleId == OrgRoleIds.DistrictAdmin)
        {
            if (schoolName.Length > 0) return eval.Fail(_localizer["StaffImport.DistrictAdminMustNotHaveSchool"]);
        }
        else if (schoolName.Length == 0)
        {
            // Blank keeps an existing member's school; a new member (or a district admin becoming
            // school-bound) needs one.
            var kept = existing?.SchoolId != null ? refs.Schools.FirstOrDefault(s => s.Id == existing.SchoolId) : null;
            if (kept == null) return eval.Fail(_localizer["StaffImport.SchoolNameRequiredForRole"]);
            eval.School = kept;
        }
        else
        {
            var school = refs.Schools.FirstOrDefault(s => string.Equals(s.Name, schoolName, StringComparison.OrdinalIgnoreCase));
            if (school == null) return eval.Fail(_localizer["StaffImport.UnknownSchool", schoolName]);
            eval.School = school;
        }

        var title = Cell("Title");
        if (ImportWorkbook.IsClear(title)) eval.Title = (true, null);
        else if (title.Length > 0)
        {
            if (title.Length > 150) return eval.Fail(_localizer["StaffImport.TitleTooLong"]);
            eval.Title = (true, title);
        }

        // Caller scope (mirrors StaffInviteService): SchoolAdmins only manage non-admin staff of their own school.
        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin)
        {
            if (roleId == OrgRoleIds.DistrictAdmin) return eval.Fail(_localizer["StaffImport.NoPermissionImportDistrictAdmin"]);
            if (eval.School == null || eval.School.Id != ctx.SchoolId) return eval.Fail(_localizer["StaffImport.OnlyImportForOwnSchool"]);
            if (existing != null && (existing.OrgRoleId == OrgRoleIds.DistrictAdmin || existing.SchoolId != ctx.SchoolId))
                return eval.Fail(_localizer["StaffImport.NoPermissionManageThatStaffMember"]);
        }

        if (existing == null)
        {
            if (refs.OtherAccountEmails.Contains(eval.Key))
                return eval.Fail(_localizer["StaffImport.EmailAlreadyHasAccount"]);
            if (refs.PendingInviteEmails.Contains(eval.Key))
                return eval.Fail(_localizer["StaffImport.EmailAlreadyInvited"]);
            eval.Outcome = ImportRowOutcome.New;
            return eval;
        }

        if (existing.OrgRoleId != roleId)
        {
            if (existing.OrgRoleId == OrgRoleIds.DistrictAdmin && existing.IsActive && refs.ActiveDistrictAdminCount <= 1)
                return eval.Fail(_localizer["StaffImport.CannotChangeRoleOfLastAdmin"]);
            eval.Changes.Add($"Role: {OrgRoleIds.NameOf(existing.OrgRoleId)} → {OrgRoleIds.NameOf(roleId.Value)}");
        }
        if ((eval.School?.Id) != existing.SchoolId)
            eval.Changes.Add($"School: {refs.Schools.FirstOrDefault(s => s.Id == existing.SchoolId)?.Name ?? ""} → {eval.School?.Name ?? ""}");
        if (eval.Title.Set && !string.Equals(eval.Title.Value, existing.Title, StringComparison.Ordinal))
            eval.Changes.Add($"Title: {existing.Title} → {eval.Title.Value}");

        eval.Outcome = eval.Changes.Count > 0 ? ImportRowOutcome.Updated : ImportRowOutcome.Unchanged;
        return eval;
    }

    private async Task<string?> ApplyUpdateAsync(int userId, RowEvaluation eval, CancellationToken ct)
    {
        var profile = await _context.StaffProfiles.FirstOrDefaultAsync(p => p.Id == eval.Existing!.ProfileId, ct);
        if (profile == null)
            return _localizer["StaffImport.StaffMemberNotFound"];
        profile.OrgRoleId = eval.OrgRoleId!.Value;
        profile.SchoolId = eval.School?.Id;
        if (eval.Title.Set) profile.Title = eval.Title.Value;
        profile.UpdatedById = userId;
        return null;
    }

    // ================================================================= Helpers

    private async Task<(StaffContext? Ctx, string? Denied, ServiceErrorKind Kind)> RequireAdminAsync(int userId, CancellationToken ct)
    {
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null)
            return (null, _localizer["StaffImport.ProfileNotFound"], ServiceErrorKind.NotFound);
        if (!OrgRoleIds.IsAdmin(ctx.OrgRoleId))
            return (null, PermissionMessage, ServiceErrorKind.Forbidden);
        if (ctx.OrgRoleId == OrgRoleIds.SchoolAdmin && ctx.SchoolId == null)
            return (null, _localizer["StaffImport.CallerNoSchool"], ServiceErrorKind.Validation);
        return (ctx, null, ServiceErrorKind.None);
    }

    private Task<List<RefSchool>> LoadSchoolsAsync(StaffContext ctx, CancellationToken ct)
        => _context.Schools.AsNoTracking()
            .Where(s => s.DistrictId == ctx.DistrictId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new RefSchool(s.Id, s.Name))
            .ToListAsync(ct);

    private async Task<ReferenceData> LoadReferenceAsync(StaffContext ctx, IEnumerable<string> emails, CancellationToken ct)
    {
        var wanted = emails.Select(e => e.Trim().ToLowerInvariant()).Where(e => e.Length > 0).Distinct().ToList();
        var schools = await LoadSchoolsAsync(ctx, ct);

        var staff = new Dictionary<string, ExistingStaff>(StringComparer.OrdinalIgnoreCase);
        var otherAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        foreach (var chunk in wanted.Chunk(500))
        {
            var users = await _context.Users.AsNoTracking()
                .Where(u => chunk.Contains(u.Email.ToLower()))
                .Select(u => new
                {
                    u.Id, u.Email,
                    Profile = _context.StaffProfiles.Where(p => p.UserId == u.Id && p.DistrictId == ctx.DistrictId)
                        .Select(p => new { p.Id, p.OrgRoleId, p.SchoolId, p.Title, p.IsActive }).FirstOrDefault()
                })
                .ToListAsync(ct);
            foreach (var u in users)
            {
                if (u.Profile != null)
                    staff[u.Email] = new ExistingStaff(u.Profile.Id, u.Id, u.Email, u.Profile.OrgRoleId, u.Profile.SchoolId, u.Profile.Title, u.Profile.IsActive);
                else
                    otherAccounts.Add(u.Email);
            }

            var invited = await _context.StaffInvites.AsNoTracking()
                .Where(i => chunk.Contains(i.Email.ToLower()) && i.IsActive && i.AcceptedAt == null && i.InviteToken != null && i.InviteExpiresAt > now)
                .Select(i => i.Email)
                .ToListAsync(ct);
            foreach (var e in invited) pending.Add(e);
        }

        var activeAdmins = await _context.StaffProfiles.AsNoTracking()
            .CountAsync(p => p.DistrictId == ctx.DistrictId && p.OrgRoleId == OrgRoleIds.DistrictAdmin && p.IsActive, ct);

        return new ReferenceData
        {
            Schools = schools,
            StaffByEmail = staff,
            OtherAccountEmails = otherAccounts,
            PendingInviteEmails = pending,
            ActiveDistrictAdminCount = activeAdmins
        };
    }
}
