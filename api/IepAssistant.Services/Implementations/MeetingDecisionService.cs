using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Structured decisions captured live during a meeting, and their surfacing as proposed edits on a draft
/// (see <see cref="IMeetingDecisionService"/>, plan 7, decision 3). A decision is never auto-applied — the
/// editor lists it as a candidate; a human makes the edit themselves and then calls
/// <see cref="MarkAppliedAsync"/>.
///
/// <para>The contract names an "InProgress" meeting status as also eligible to record decisions; the
/// modeled <see cref="MeetingStatus"/> enum (plan 4) has no such state, so this service accepts
/// <see cref="MeetingStatus.Held"/> and <see cref="MeetingStatus.Continued"/> only — the two states that
/// exist and represent "the meeting is happening or has happened."</para>
/// </summary>
public class MeetingDecisionService : IMeetingDecisionService
{
    private const string MeetingNotFoundMessage = "Meeting not found.";
    private const string DecisionNotFoundMessage = "Decision not found.";
    private const string InstanceNotFoundMessage = "Document not found.";
    private const string PermissionMessage = "You do not have permission to access this meeting.";
    private const string NotRecordableMessage = "Decisions can only be recorded once the meeting is Held or Continued.";
    private const string TextRequiredMessage = "Decision text is required.";
    private const int MaxTextLength = 2000;
    private const int MaxTargetLabelLength = 500;
    private const int ProposedEditWindowDays = 60;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;

    public MeetingDecisionService(ApplicationDbContext context, IOrgAccessService orgAccess)
    {
        _context = context;
        _orgAccess = orgAccess;
    }

    public async Task<ServiceResult<List<MeetingDecisionModel>>> GetForMeetingAsync(int userId, int meetingId, CancellationToken ct = default)
    {
        var meeting = await LoadMeetingHeaderAsync(meetingId, ct);
        if (meeting == null)
            return ServiceResult<List<MeetingDecisionModel>>.FailureResult(MeetingNotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, meeting.SchoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<List<MeetingDecisionModel>>.FailureResult(PermissionMessage);

        var rows = await MapQuery(_context.MeetingDecisions.AsNoTracking().Where(d => d.MeetingId == meetingId))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        return ServiceResult<List<MeetingDecisionModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<MeetingDecisionModel>> CreateAsync(int userId, int meetingId, CreateMeetingDecisionModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Text))
            return ServiceResult<MeetingDecisionModel>.FailureResult(TextRequiredMessage);
        var text = model.Text.Trim();
        if (text.Length > MaxTextLength)
            return ServiceResult<MeetingDecisionModel>.FailureResult($"Decision text must be {MaxTextLength} characters or fewer.");

        var meeting = await LoadMeetingHeaderAsync(meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingDecisionModel>.FailureResult(MeetingNotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, meeting.SchoolStudentId, AccessRole.Collaborator, ct))
            return ServiceResult<MeetingDecisionModel>.FailureResult(PermissionMessage);
        if (meeting.Status is not (MeetingStatus.Held or MeetingStatus.Continued))
            return ServiceResult<MeetingDecisionModel>.FailureResult(NotRecordableMessage);

        var targetLabel = string.IsNullOrWhiteSpace(model.TargetLabel) ? null : model.TargetLabel.Trim();
        if (targetLabel != null && targetLabel.Length > MaxTargetLabelLength)
            targetLabel = targetLabel[..MaxTargetLabelLength];

        var decision = new MeetingDecision
        {
            MeetingId = meetingId,
            TargetFieldKey = model.TargetFieldKey,
            TargetRowId = string.IsNullOrWhiteSpace(model.TargetRowId) ? null : model.TargetRowId.Trim(),
            TargetLabel = targetLabel,
            Text = text,
            Outcome = model.Outcome,
            RecordedByUserId = userId,
            CreatedById = userId,
            UpdatedById = userId
        };
        await _context.MeetingDecisions.AddAsync(decision, ct);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<MeetingDecisionModel>.SuccessResult(await MapOneAsync(decision.Id, ct));
    }

    public async Task<ServiceResult<MeetingDecisionModel>> UpdateAsync(int userId, int decisionId, UpdateMeetingDecisionModel model, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model.Text))
            return ServiceResult<MeetingDecisionModel>.FailureResult(TextRequiredMessage);
        var text = model.Text.Trim();
        if (text.Length > MaxTextLength)
            return ServiceResult<MeetingDecisionModel>.FailureResult($"Decision text must be {MaxTextLength} characters or fewer.");

        var (decision, error) = await LoadForWriteAsync(userId, decisionId, ct);
        if (error != null)
            return ServiceResult<MeetingDecisionModel>.FailureResult(error);

        decision!.Text = text;
        decision.Outcome = model.Outcome;
        decision.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<MeetingDecisionModel>.SuccessResult(await MapOneAsync(decision.Id, ct));
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int decisionId, CancellationToken ct = default)
    {
        var (decision, error) = await LoadForWriteAsync(userId, decisionId, ct);
        if (error != null)
            return ServiceResult.FailureResult(error);

        _context.MeetingDecisions.Remove(decision!);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    public async Task<ServiceResult<List<ProposedEditModel>>> GetProposedEditsForInstanceAsync(int userId, int instanceId, CancellationToken ct = default)
    {
        var schoolStudentId = await _context.DocumentInstances.AsNoTracking()
            .Where(i => i.Id == instanceId)
            .Select(i => (int?)i.SchoolStudentId)
            .FirstOrDefaultAsync(ct);
        if (schoolStudentId == null)
            return ServiceResult<List<ProposedEditModel>>.FailureResult(InstanceNotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId.Value, AccessRole.Viewer, ct))
            return ServiceResult<List<ProposedEditModel>>.FailureResult(PermissionMessage);

        var cutoff = DateTime.UtcNow.AddDays(-ProposedEditWindowDays);
        var meetingIds = await _context.Meetings.AsNoTracking()
            .Where(m => m.DocumentInstanceId == instanceId
                        || (m.SchoolStudentId == schoolStudentId.Value && m.StartsAtUtc >= cutoff))
            .Select(m => m.Id)
            .ToListAsync(ct);

        var rows = await (
            from d in _context.MeetingDecisions.AsNoTracking()
            join m in _context.Meetings.AsNoTracking() on d.MeetingId equals m.Id
            where meetingIds.Contains(d.MeetingId)
            orderby d.CreatedAt descending
            select new ProposedEditModel
            {
                DecisionId = d.Id,
                MeetingId = m.Id,
                MeetingTitle = m.Title,
                TargetFieldKey = d.TargetFieldKey,
                TargetRowId = d.TargetRowId,
                TargetLabel = d.TargetLabel,
                Text = d.Text,
                Outcome = d.Outcome,
                RecordedAt = d.CreatedAt,
                AppliedAt = d.AppliedAt
            }).ToListAsync(ct);

        return ServiceResult<List<ProposedEditModel>>.SuccessResult(rows);
    }

    public async Task<ServiceResult<ProposedEditModel>> MarkAppliedAsync(int userId, int decisionId, CancellationToken ct = default)
    {
        var (decision, error) = await LoadForWriteAsync(userId, decisionId, ct);
        if (error != null)
            return ServiceResult<ProposedEditModel>.FailureResult(error);

        decision!.AppliedAt = DateTime.UtcNow;
        decision.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        var meetingTitle = await _context.Meetings.AsNoTracking()
            .Where(m => m.Id == decision.MeetingId)
            .Select(m => m.Title)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        return ServiceResult<ProposedEditModel>.SuccessResult(new ProposedEditModel
        {
            DecisionId = decision.Id,
            MeetingId = decision.MeetingId,
            MeetingTitle = meetingTitle,
            TargetFieldKey = decision.TargetFieldKey,
            TargetRowId = decision.TargetRowId,
            TargetLabel = decision.TargetLabel,
            Text = decision.Text,
            Outcome = decision.Outcome,
            RecordedAt = decision.CreatedAt,
            AppliedAt = decision.AppliedAt
        });
    }

    // ---------------------------------------------------------------- Loading + mapping

    private sealed record MeetingHeader(int Id, int SchoolStudentId, MeetingStatus Status);

    private async Task<MeetingHeader?> LoadMeetingHeaderAsync(int meetingId, CancellationToken ct) =>
        await _context.Meetings.AsNoTracking()
            .Where(m => m.Id == meetingId)
            .Select(m => new MeetingHeader(m.Id, m.SchoolStudentId, m.Status))
            .FirstOrDefaultAsync(ct);

    private async Task<(MeetingDecision? Decision, string? Error)> LoadForWriteAsync(int userId, int decisionId, CancellationToken ct)
    {
        var decision = await _context.MeetingDecisions.FirstOrDefaultAsync(d => d.Id == decisionId, ct);
        if (decision == null)
            return (null, DecisionNotFoundMessage);

        var schoolStudentId = await _context.Meetings.AsNoTracking()
            .Where(m => m.Id == decision.MeetingId)
            .Select(m => (int?)m.SchoolStudentId)
            .FirstOrDefaultAsync(ct);
        if (schoolStudentId == null)
            return (null, MeetingNotFoundMessage);
        if (!await _orgAccess.CanActOnStudentAsync(userId, schoolStudentId.Value, AccessRole.Collaborator, ct))
            return (null, PermissionMessage);

        return (decision, null);
    }

    private static IQueryable<MeetingDecisionModel> MapQuery(IQueryable<MeetingDecision> query) =>
        query.Select(d => new MeetingDecisionModel
        {
            Id = d.Id,
            MeetingId = d.MeetingId,
            TargetFieldKey = d.TargetFieldKey,
            TargetRowId = d.TargetRowId,
            TargetLabel = d.TargetLabel,
            Text = d.Text,
            Outcome = d.Outcome,
            RecordedByUserId = d.RecordedByUserId,
            RecordedByName = null,
            CreatedAt = d.CreatedAt,
            AppliedAt = d.AppliedAt
        });

    private async Task<MeetingDecisionModel> MapOneAsync(int decisionId, CancellationToken ct)
    {
        var model = await MapQuery(_context.MeetingDecisions.AsNoTracking().Where(d => d.Id == decisionId)).FirstAsync(ct);
        model.RecordedByName = await _context.Users.AsNoTracking()
            .Where(u => u.Id == model.RecordedByUserId)
            .Select(u => (u.FirstName + " " + u.LastName).Trim())
            .FirstOrDefaultAsync(ct);
        return model;
    }
}
