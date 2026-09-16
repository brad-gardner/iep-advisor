using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Offline family participation (see <see cref="IFamilyContactService"/>, plan 7, decision 7).</summary>
public class FamilyContactService : IFamilyContactService
{
    private const string PermissionMessage = "You do not have permission to access this student.";
    private const string StudentNotFoundMessage = "Student not found.";
    private const string InstanceNotFoundMessage = "Document not found.";
    private const string SummaryRequiredMessage = "A summary of the family input is required.";
    private const int MaxNoteLength = 1000;
    private const int MaxSummaryLength = 4000;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;

    public FamilyContactService(ApplicationDbContext context, IOrgAccessService orgAccess)
    {
        _context = context;
        _orgAccess = orgAccess;
    }

    public async Task<ServiceResult<List<FamilyContactAttemptModel>>> GetContactAttemptsAsync(int userId, int schoolStudentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<FamilyContactAttemptModel>>.FailureResult(PermissionMessage);

        var rows = await MapAttemptQuery(_context.FamilyContactAttempts.AsNoTracking().Where(a => a.SchoolStudentId == schoolStudentId))
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync(ct);

        return ServiceResult<List<FamilyContactAttemptModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<FamilyContactAttemptModel>> RecordContactAttemptAsync(int userId, int schoolStudentId, CreateFamilyContactAttemptModel model, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<FamilyContactAttemptModel>.FailureResult(PermissionMessage);

        if (!await _context.SchoolStudents.AsNoTracking().AnyAsync(s => s.Id == schoolStudentId, ct))
            return ServiceResult<FamilyContactAttemptModel>.FailureResult(StudentNotFoundMessage);

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
            return ServiceResult<List<OfflineFamilyInputModel>>.FailureResult(PermissionMessage);

        var rows = await MapInputQuery(_context.OfflineFamilyInputs.AsNoTracking().Where(i => i.SchoolStudentId == schoolStudentId))
            .OrderByDescending(i => i.ReceivedAt)
            .ToListAsync(ct);

        return ServiceResult<List<OfflineFamilyInputModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<OfflineFamilyInputModel>> RecordOfflineInputAsync(int userId, int schoolStudentId, CreateOfflineFamilyInputModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Summary))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(SummaryRequiredMessage);
        var summary = model.Summary.Trim();
        if (summary.Length > MaxSummaryLength)
            return ServiceResult<OfflineFamilyInputModel>.FailureResult($"Summary must be {MaxSummaryLength} characters or fewer.");

        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(PermissionMessage);

        if (!await _context.SchoolStudents.AsNoTracking().AnyAsync(s => s.Id == schoolStudentId, ct))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(StudentNotFoundMessage);

        if (model.DocumentInstanceId.HasValue
            && !await _context.DocumentInstances.AsNoTracking().AnyAsync(i => i.Id == model.DocumentInstanceId.Value && i.SchoolStudentId == schoolStudentId, ct))
            return ServiceResult<OfflineFamilyInputModel>.FailureResult(InstanceNotFoundMessage);

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
