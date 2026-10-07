using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Offline family participation (see <see cref="IFamilyContactService"/>, plan 7, decision 7).
///
/// Multilingual plan (2026-10-06) phase 5: every failure <c>FamilyContactController</c> maps to a status
/// carries an explicit <see cref="ServiceErrorKind"/>, and every message is localized
/// (<c>Messages.resx</c>/<c>.es.resx</c>).</summary>
public class FamilyContactService : IFamilyContactService
{
    private const int MaxNoteLength = 1000;
    private const int MaxSummaryLength = 4000;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IStringLocalizer<Messages> _localizer;

    public FamilyContactService(ApplicationDbContext context, IOrgAccessService orgAccess, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _orgAccess = orgAccess;
        _localizer = localizer;
    }

    public async Task<ServiceResult<List<FamilyContactAttemptModel>>> GetContactAttemptsAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<FamilyContactAttemptModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["FamilyContact.Permission"]);

        var rows = await MapAttemptQuery(_context.FamilyContactAttempts.AsNoTracking().Where(a => a.SchoolStudentId == schoolStudentId))
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync(ct);

        return ServiceResult<List<FamilyContactAttemptModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<FamilyContactAttemptModel>> RecordContactAttemptAsync(int userId, int schoolStudentId, CreateFamilyContactAttemptModel model, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<FamilyContactAttemptModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["FamilyContact.Permission"]);

        if (!await _context.SchoolStudents.AsNoTracking().AnyAsync(s => s.Id == schoolStudentId, ct))
            return ServiceResult<FamilyContactAttemptModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Educator.StudentNotFound"]);

        var note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim();
        if (note != null && note.Length > MaxNoteLength)
            note = note[..MaxNoteLength];

        var attempt = new FamilyContactAttempt
        {
            SchoolStudentId = schoolStudentId,
            AttemptedAt = model.AttemptedAt ?? DateTime.UtcNow,
            Method = model.Method,
            Outcome = model.Outcome,
            Note = note,
            RecordedByUserId = userId,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.FamilyContactAttempts.AddAsync(attempt, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<FamilyContactAttemptModel>.SuccessResult(await MapAttemptAsync(attempt.Id, ct));
    }

    public async Task<ServiceResult<List<OfflineFamilyInputModel>>> GetOfflineInputAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<OfflineFamilyInputModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["FamilyContact.Permission"]);

        var rows = await MapInputQuery(_context.OfflineFamilyInputs.AsNoTracking().Where(i => i.SchoolStudentId == schoolStudentId))
            .OrderByDescending(i => i.ReceivedAt)
            .ToListAsync(ct);

        return ServiceResult<List<OfflineFamilyInputModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<OfflineFamilyInputModel>> RecordOfflineInputAsync(int userId, int schoolStudentId, CreateOfflineFamilyInputModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Summary))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(ServiceErrorKind.Validation, _localizer["FamilyContact.SummaryRequired"]);
        var summary = model.Summary.Trim();
        if (summary.Length > MaxSummaryLength)
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(ServiceErrorKind.Validation, _localizer["FamilyContact.SummaryTooLong", MaxSummaryLength]);

        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["FamilyContact.Permission"]);

        if (!await _context.SchoolStudents.AsNoTracking().AnyAsync(s => s.Id == schoolStudentId, ct))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Educator.StudentNotFound"]);

        if (model.DocumentInstanceId.HasValue
            && !await _context.DocumentInstances.AsNoTracking().AnyAsync(i => i.Id == model.DocumentInstanceId.Value && i.SchoolStudentId == schoolStudentId, ct))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Documents.NotFound"]);

        var input = new OfflineFamilyInput
        {
            SchoolStudentId = schoolStudentId,
            DocumentInstanceId = model.DocumentInstanceId,
            ReceivedAt = model.ReceivedAt ?? DateTime.UtcNow,
            Method = model.Method,
            Summary = summary,
            RecordedByUserId = userId,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.OfflineFamilyInputs.AddAsync(input, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<OfflineFamilyInputModel>.SuccessResult(await MapInputAsync(input.Id, ct));
    }

    // ---------------------------------------------------------------- Mapping

    private IQueryable<FamilyContactAttemptModel> MapAttemptQuery(IQueryable<FamilyContactAttempt> query) =>
        from a in query
        join u in _context.Users.AsNoTracking() on a.RecordedByUserId equals u.Id
        select new FamilyContactAttemptModel
        {
            Id = a.Id,
            SchoolStudentId = a.SchoolStudentId,
            AttemptedAt = a.AttemptedAt,
            Method = a.Method,
            Outcome = a.Outcome,
            Note = a.Note,
            RecordedByUserId = a.RecordedByUserId,
            RecordedByName = (u.FirstName + " " + u.LastName).Trim()
        };

    private IQueryable<OfflineFamilyInputModel> MapInputQuery(IQueryable<OfflineFamilyInput> query) =>
        from i in query
        join u in _context.Users.AsNoTracking() on i.RecordedByUserId equals u.Id
        select new OfflineFamilyInputModel
        {
            Id = i.Id,
            SchoolStudentId = i.SchoolStudentId,
            DocumentInstanceId = i.DocumentInstanceId,
            ReceivedAt = i.ReceivedAt,
            Method = i.Method,
            Summary = i.Summary,
            RecordedByUserId = i.RecordedByUserId,
            RecordedByName = (u.FirstName + " " + u.LastName).Trim()
        };

    private async Task<FamilyContactAttemptModel> MapAttemptAsync(int id, CancellationToken ct) =>
        await MapAttemptQuery(_context.FamilyContactAttempts.AsNoTracking().Where(a => a.Id == id)).FirstAsync(ct);

    private async Task<OfflineFamilyInputModel> MapInputAsync(int id, CancellationToken ct) =>
        await MapInputQuery(_context.OfflineFamilyInputs.AsNoTracking().Where(i => i.Id == id)).FirstAsync(ct);
}
