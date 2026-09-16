using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Meeting scheduling/lifecycle (see <see cref="IMeetingService"/>, plan 4 decision 1). Default
/// participants come from the student's active team, accepted family links and the student's own account
/// (see <see cref="BuildDefaultParticipantInputsAsync"/>); the caller is always ensured a participant.
/// Any change to start/duration/location/video bumps <see cref="Meeting.Sequence"/> and notifies every
/// participant who has a <see cref="User"/> via <see cref="INotificationService"/>.
/// </summary>
public class MeetingService : IMeetingService
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 480;
    public const int DefaultDurationMinutes = 60;
    public const string DefaultTimeZoneId = "America/New_York";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly INotificationService _notifications;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MeetingService> _logger;

    public MeetingService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IAccessService accessService,
        INotificationService notifications,
        IAuditLogger audit,
        ILogger<MeetingService> logger)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _notifications = notifications;
        _audit = audit;
        _logger = logger;
    }

    // ----------------------------------------------------------------- Create

    public async Task<ServiceResult<MeetingModel>> CreateAsync(int userId, int studentId, CreateMeetingModel model, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Collaborator, ct))
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to schedule a meeting for this student.");

        var student = await _context.SchoolStudents.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return ServiceResult<MeetingModel>.FailureResult("Student not found.");

        var timeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? DefaultTimeZoneId : model.TimeZoneId.Trim();
        if (!IsValidTimeZone(timeZoneId))
            return ServiceResult<MeetingModel>.FailureResult("Invalid time zone.");

        var duration = model.DurationMinutes ?? DefaultDurationMinutes;
        if (duration < MinDurationMinutes || duration > MaxDurationMinutes)
            return ServiceResult<MeetingModel>.FailureResult($"Duration must be between {MinDurationMinutes} and {MaxDurationMinutes} minutes.");

        var title = string.IsNullOrWhiteSpace(model.Title) ? $"{model.Type.ToDisplay()} Meeting" : model.Title.Trim();

        var meeting = new Meeting
        {
            SchoolStudentId = studentId,
            Type = model.Type,
            Title = title,
            StartsAtUtc = DateTime.SpecifyKind(model.StartsAtUtc, DateTimeKind.Utc),
            TimeZoneId = timeZoneId,
            DurationMinutes = duration,
            Location = NormalizeOptional(model.Location),
            VideoUrl = NormalizeOptional(model.VideoUrl),
            Status = MeetingStatus.Scheduled,
            DocumentInstanceId = model.DocumentInstanceId,
            Notes = NormalizeOptional(model.Notes),
            Sequence = 0,
            CreatedByUserId = userId,
            CreatedById = userId,
            UpdatedById = userId
        };

        await _context.Meetings.AddAsync(meeting, ct);
        await _context.SaveChangesAsync(ct);

        var participants = model.Participants ?? await BuildDefaultParticipantInputsAsync(studentId, userId, ct);
        var participantError = await ReplaceParticipantsAsync(meeting, participants, studentId, userId, ct);
        if (participantError != null)
        {
            _context.Meetings.Remove(meeting);
            await _context.SaveChangesAsync(ct);
            return ServiceResult<MeetingModel>.FailureResult(participantError);
        }
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
        await NotifyParticipantsAsync(meeting.Id, NotificationKind.MeetingScheduled, "scheduled", ct);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    // ----------------------------------------------------------------- Read

    public async Task<ServiceResult<List<DefaultParticipantModel>>> GetDefaultParticipantsAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Collaborator, ct))
            return ServiceResult<List<DefaultParticipantModel>>.FailureResult("You do not have permission to schedule meetings for this student.");

        var inputs = await BuildDefaultParticipantInputsAsync(studentId, userId, ct);
        var userIds = inputs.Where(i => i.UserId.HasValue).Select(i => i.UserId!.Value).ToList();
        var familyUserIds = (await LoadFamilyUserIdsAsync(studentId, ct)).ToHashSet();
        var studentAccountUserId = await _context.StudentProfiles.AsNoTracking()
            .Where(sp => sp.SchoolStudentId == studentId)
            .Select(sp => (int?)sp.UserId)
            .FirstOrDefaultAsync(ct);
        var users = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);

        var result = inputs
            .Where(i => i.UserId.HasValue && users.ContainsKey(i.UserId.Value))
            .Select(i =>
            {
                var u = users[i.UserId!.Value];
                return new DefaultParticipantModel
                {
                    UserId = u.Id,
                    DisplayName = $"{u.FirstName} {u.LastName}".Trim(),
                    Email = u.Email,
                    TeamRole = i.TeamRole,
                    IsFamily = familyUserIds.Contains(u.Id),
                    IsStudent = studentAccountUserId.HasValue && u.Id == studentAccountUserId.Value
                };
            })
            .OrderBy(m => m.IsStudent).ThenBy(m => m.IsFamily).ThenBy(m => m.DisplayName)
            .ToList();
        return ServiceResult<List<DefaultParticipantModel>>.SuccessResult(result);
    }

    public async Task<ServiceResult<List<MeetingModel>>> GetForStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<MeetingModel>>.FailureResult("You do not have permission to view this student's meetings.");

        var ids = await _context.Meetings.AsNoTracking()
            .Where(m => m.SchoolStudentId == studentId)
            .OrderByDescending(m => m.StartsAtUtc)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var models = new List<MeetingModel>(ids.Count);
        foreach (var id in ids)
            models.Add(await LoadMeetingModelAsync(id, userId, ct));
        return ServiceResult<List<MeetingModel>>.SuccessResult(models);
    }

    public async Task<ServiceResult<MeetingModel>> GetAsync(int userId, int meetingId, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult("Meeting not found.");

        var isParticipant = meeting.Participants.Any(p => p.UserId == userId);
        if (!isParticipant && !await _orgAccess.CanActOnStudentAsync(userId, meeting.SchoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to view this meeting.");

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meetingId, userId, ct));
    }

    public async Task<ServiceResult<List<MeetingModel>>> ListMineAsync(int userId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromUtc = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : DateTime.UtcNow;
        var toUtc = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(90);

        var ids = await _context.Meetings.AsNoTracking()
            .Where(m => m.StartsAtUtc >= fromUtc && m.StartsAtUtc <= toUtc && m.Participants.Any(p => p.UserId == userId))
            .OrderBy(m => m.StartsAtUtc)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var models = new List<MeetingModel>(ids.Count);
        foreach (var id in ids)
            models.Add(await LoadMeetingModelAsync(id, userId, ct));
        return ServiceResult<List<MeetingModel>>.SuccessResult(models);
    }

    public async Task<ServiceResult<List<MeetingModel>>> ListForChildAsync(int parentUserId, int childProfileId, CancellationToken ct = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childProfileId, parentUserId, AccessRole.Viewer, ct))
            return ServiceResult<List<MeetingModel>>.FailureResult("You do not have permission to view this child's meetings.");

        var studentIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == childProfileId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId)
            .Distinct()
            .ToListAsync(ct);
        if (studentIds.Count == 0)
            return ServiceResult<List<MeetingModel>>.SuccessResult(new List<MeetingModel>());

        var ids = await _context.Meetings.AsNoTracking()
            .Where(m => studentIds.Contains(m.SchoolStudentId))
            .OrderBy(m => m.StartsAtUtc)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var models = new List<MeetingModel>(ids.Count);
        foreach (var id in ids)
            models.Add(await LoadMeetingModelAsync(id, parentUserId, ct));
        return ServiceResult<List<MeetingModel>>.SuccessResult(models);
    }

    // ----------------------------------------------------------------- Update / cancel / status / attendance

    public async Task<ServiceResult<MeetingModel>> UpdateAsync(int userId, int meetingId, UpdateMeetingModel model, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult("Meeting not found.");
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to update this meeting.");
        if (meeting.Status == MeetingStatus.Cancelled)
            return ServiceResult<MeetingModel>.FailureResult("Cannot update a cancelled meeting.");

        var scheduleChanged = false;

        if (model.StartsAtUtc.HasValue)
        {
            var utc = DateTime.SpecifyKind(model.StartsAtUtc.Value, DateTimeKind.Utc);
            if (utc != meeting.StartsAtUtc) { meeting.StartsAtUtc = utc; scheduleChanged = true; }
        }
        if (model.DurationMinutes.HasValue)
        {
            if (model.DurationMinutes.Value < MinDurationMinutes || model.DurationMinutes.Value > MaxDurationMinutes)
                return ServiceResult<MeetingModel>.FailureResult($"Duration must be between {MinDurationMinutes} and {MaxDurationMinutes} minutes.");
            if (model.DurationMinutes.Value != meeting.DurationMinutes) { meeting.DurationMinutes = model.DurationMinutes.Value; scheduleChanged = true; }
        }
        if (model.Location != null)
        {
            var loc = NormalizeOptional(model.Location);
            if (loc != meeting.Location) { meeting.Location = loc; scheduleChanged = true; }
        }
        if (model.VideoUrl != null)
        {
            var url = NormalizeOptional(model.VideoUrl);
            if (url != meeting.VideoUrl) { meeting.VideoUrl = url; scheduleChanged = true; }
        }
        if (model.Type.HasValue)
            meeting.Type = model.Type.Value;
        if (!string.IsNullOrWhiteSpace(model.Title))
            meeting.Title = model.Title.Trim();
        if (!string.IsNullOrWhiteSpace(model.TimeZoneId))
        {
            if (!IsValidTimeZone(model.TimeZoneId))
                return ServiceResult<MeetingModel>.FailureResult("Invalid time zone.");
            meeting.TimeZoneId = model.TimeZoneId.Trim();
        }
        if (model.DocumentInstanceId.HasValue)
            meeting.DocumentInstanceId = model.DocumentInstanceId;
        if (model.Notes != null)
            meeting.Notes = NormalizeOptional(model.Notes);

        if (scheduleChanged)
            meeting.Sequence++;
        meeting.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        if (model.Participants != null)
        {
            var participantError = await ReplaceParticipantsAsync(meeting, model.Participants, meeting.SchoolStudentId, meeting.CreatedByUserId, ct);
            if (participantError != null)
                return ServiceResult<MeetingModel>.FailureResult(participantError);
            await _context.SaveChangesAsync(ct);
        }

        _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
        if (scheduleChanged)
            await NotifyParticipantsAsync(meeting.Id, NotificationKind.MeetingUpdated, "updated", ct);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingModel>> CancelAsync(int userId, int meetingId, string? reason, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult("Meeting not found.");
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to cancel this meeting.");

        if (meeting.Status != MeetingStatus.Cancelled)
        {
            meeting.Status = MeetingStatus.Cancelled;
            meeting.Sequence++;
            meeting.CancelReason = NormalizeOptional(reason);
            meeting.UpdatedById = userId;
            await _context.SaveChangesAsync(ct);

            _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
            await NotifyParticipantsAsync(meeting.Id, NotificationKind.MeetingCancelled, "cancelled", ct);
        }

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingModel>> SetStatusAsync(int userId, int meetingId, MeetingStatus status, CancellationToken ct = default)
    {
        if (status is not (MeetingStatus.Scheduled or MeetingStatus.Held or MeetingStatus.Continued))
            return ServiceResult<MeetingModel>.FailureResult("Status must be Scheduled, Held, or Continued.");

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult("Meeting not found.");
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to update this meeting.");
        if (meeting.Status == MeetingStatus.Cancelled)
            return ServiceResult<MeetingModel>.FailureResult("Cannot change the status of a cancelled meeting.");

        meeting.Status = status;
        meeting.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingModel>> RecordAttendanceAsync(int userId, int meetingId, List<AttendanceItemModel> items, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult("Meeting not found.");
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to update this meeting.");
        if (meeting.Status is not (MeetingStatus.Held or MeetingStatus.Continued))
            return ServiceResult<MeetingModel>.FailureResult("Attendance can only be recorded for a held meeting.");

        foreach (var item in items)
        {
            var participant = meeting.Participants.FirstOrDefault(p => p.Id == item.ParticipantId);
            if (participant == null)
                return ServiceResult<MeetingModel>.FailureResult("Participant not found on this meeting.");

            participant.Attended = item.Attended;
            participant.UpdatedById = userId;
            if (item.Attended)
            {
                participant.ExcusedAt = null;
                participant.ExcusalNote = null;
            }
            else
            {
                participant.ExcusedAt ??= DateTime.UtcNow;
                if (item.ExcusalNote != null)
                    participant.ExcusalNote = NormalizeOptional(item.ExcusalNote);
            }
        }

        meeting.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    // ----------------------------------------------------------------- RSVP

    public async Task<ServiceResult<MeetingModel>> RsvpAsync(int userId, int meetingId, InviteStatus status, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.Include(m => m.Participants).FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult("Meeting not found.");

        var participant = meeting.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant == null)
            return ServiceResult<MeetingModel>.FailureResult("You do not have permission to respond to this meeting.");

        participant.InviteStatus = status;
        participant.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingRsvpPreviewModel>> GetByRsvpTokenAsync(string token, CancellationToken ct = default)
    {
        var (participant, error) = await FindValidRsvpParticipantAsync(token, ct);
        if (error != null)
            return ServiceResult<MeetingRsvpPreviewModel>.FailureResult(error);

        var meetingModel = await LoadMeetingModelAsync(participant!.MeetingId, participant.UserId ?? 0, ct);
        return ServiceResult<MeetingRsvpPreviewModel>.SuccessResult(new MeetingRsvpPreviewModel
        {
            Meeting = meetingModel,
            Status = participant.InviteStatus
        });
    }

    public async Task<ServiceResult<MeetingRsvpPreviewModel>> RsvpByTokenAsync(string token, InviteStatus status, CancellationToken ct = default)
    {
        var (participant, error) = await FindValidRsvpParticipantAsync(token, ct);
        if (error != null)
            return ServiceResult<MeetingRsvpPreviewModel>.FailureResult(error);

        participant!.InviteStatus = status;
        await _context.SaveChangesAsync(ct);

        var meetingModel = await LoadMeetingModelAsync(participant.MeetingId, participant.UserId ?? 0, ct);
        return ServiceResult<MeetingRsvpPreviewModel>.SuccessResult(new MeetingRsvpPreviewModel
        {
            Meeting = meetingModel,
            Status = participant.InviteStatus
        });
    }

    private async Task<(MeetingParticipant? Participant, string? Error)> FindValidRsvpParticipantAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (null, "Invalid RSVP link.");

        var participant = await _context.MeetingParticipants
            .Include(p => p.Meeting)
            .FirstOrDefaultAsync(p => p.RsvpToken == token, ct);
        if (participant == null)
            return (null, "Invalid RSVP link.");
        if (participant.Meeting.StartsAtUtc <= DateTime.UtcNow)
            return (null, "This meeting has already started; RSVP is no longer available.");

        return (participant, null);
    }

    // ----------------------------------------------------------------- Authorization helpers

    private async Task<bool> CanManageAsync(int userId, Meeting meeting, CancellationToken ct)
    {
        if (meeting.CreatedByUserId == userId)
            return true;

        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx != null && OrgRoleIds.IsAdmin(ctx.OrgRoleId)
            && await _orgAccess.CanActOnStudentAsync(userId, meeting.SchoolStudentId, AccessRole.Viewer, ct))
            return true;

        var leadUserId = await _context.SchoolStudents.AsNoTracking()
            .Where(s => s.Id == meeting.SchoolStudentId)
            .Select(s => s.CaseManagerUserId)
            .FirstOrDefaultAsync(ct);
        return leadUserId.HasValue && leadUserId.Value == userId;
    }

    // ----------------------------------------------------------------- Participants

    /// <summary>Default roster: active team members (their TeamRole), accepted family links (isFamily),
    /// the student's own account if one exists (isStudent). The creator is added separately by
    /// <see cref="ReplaceParticipantsAsync"/>. DistrictAdmins are excluded unless they are the creator.</summary>
    private async Task<List<ParticipantInputModel>> BuildDefaultParticipantInputsAsync(int studentId, int creatorUserId, CancellationToken ct)
    {
        var teamMembers = await _context.StudentTeamMembers.AsNoTracking()
            .Where(m => m.SchoolStudentId == studentId && m.IsActive)
            .Select(m => new { m.UserId, m.TeamRole })
            .ToListAsync(ct);

        var districtAdminUserIds = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.IsActive && p.OrgRoleId == OrgRoleIds.DistrictAdmin)
            .Select(p => p.UserId)
            .ToListAsync(ct);
        var districtAdminSet = districtAdminUserIds.ToHashSet();

        var byUserId = new Dictionary<int, ParticipantInputModel>();
        foreach (var m in teamMembers)
        {
            if (districtAdminSet.Contains(m.UserId) && m.UserId != creatorUserId)
                continue;
            byUserId[m.UserId] = new ParticipantInputModel { UserId = m.UserId, TeamRole = m.TeamRole, IsRequired = true };
        }

        var familyUserIds = await LoadFamilyUserIdsAsync(studentId, ct);
        foreach (var uid in familyUserIds)
        {
            if (districtAdminSet.Contains(uid) && uid != creatorUserId)
                continue;
            if (!byUserId.ContainsKey(uid))
                byUserId[uid] = new ParticipantInputModel { UserId = uid, TeamRole = TeamRole.Other, IsRequired = true };
        }

        var studentAccountUserId = await _context.StudentProfiles.AsNoTracking()
            .Where(sp => sp.SchoolStudentId == studentId)
            .Select(sp => (int?)sp.UserId)
            .FirstOrDefaultAsync(ct);
        if (studentAccountUserId.HasValue && !byUserId.ContainsKey(studentAccountUserId.Value)
            && !(districtAdminSet.Contains(studentAccountUserId.Value) && studentAccountUserId.Value != creatorUserId))
        {
            byUserId[studentAccountUserId.Value] = new ParticipantInputModel { UserId = studentAccountUserId.Value, TeamRole = TeamRole.Other, IsRequired = true };
        }

        return byUserId.Values.ToList();
    }

    private async Task<List<int>> LoadFamilyUserIdsAsync(int studentId, CancellationToken ct)
    {
        var childProfileIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.SchoolStudentId == studentId && l.IsActive && l.AcceptedAt != null && l.ChildProfileId != null)
            .Select(l => l.ChildProfileId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (childProfileIds.Count == 0)
            return new List<int>();

        return await _context.ChildAccesses.AsNoTracking()
            .Where(ca => childProfileIds.Contains(ca.ChildProfileId) && ca.IsActive && ca.AcceptedAt != null && ca.UserId != null)
            .Select(ca => ca.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Replaces the meeting's participant roster with <paramref name="inputs"/> (plus the creator, always).
    /// Existing rows matched by user id (or external email, for a non-user invitee) keep their
    /// RsvpToken/InviteStatus/attendance; unmatched existing rows are removed; new rows get a fresh token.
    /// Returns a user-facing error, or null on success.
    /// </summary>
    private async Task<string?> ReplaceParticipantsAsync(Meeting meeting, List<ParticipantInputModel> inputs, int studentId, int creatorUserId, CancellationToken ct)
    {
        var normalized = new List<ParticipantInputModel>(inputs);
        if (!normalized.Any(i => i.UserId == creatorUserId))
            normalized.Add(new ParticipantInputModel { UserId = creatorUserId, TeamRole = TeamRole.Other, IsRequired = true });

        foreach (var input in normalized)
        {
            if (input.UserId == null && string.IsNullOrWhiteSpace(input.ExternalEmail))
                return "Each participant must have either a user or an external email address.";
        }
        var dupUserIds = normalized.Where(i => i.UserId != null).GroupBy(i => i.UserId!.Value).Where(g => g.Count() > 1);
        if (dupUserIds.Any())
            return "A participant was listed more than once.";

        var familyUserIds = (await LoadFamilyUserIdsAsync(studentId, ct)).ToHashSet();
        var studentAccountUserId = await _context.StudentProfiles.AsNoTracking()
            .Where(sp => sp.SchoolStudentId == studentId)
            .Select(sp => (int?)sp.UserId)
            .FirstOrDefaultAsync(ct);

        var existing = await _context.MeetingParticipants
            .Where(p => p.MeetingId == meeting.Id)
            .ToListAsync(ct);

        var keep = new List<MeetingParticipant>();
        foreach (var input in normalized)
        {
            var match = input.UserId != null
                ? existing.FirstOrDefault(p => p.UserId == input.UserId)
                : existing.FirstOrDefault(p => p.UserId == null && string.Equals(p.ExternalEmail, input.ExternalEmail, StringComparison.OrdinalIgnoreCase));

            var isFamily = input.UserId != null && familyUserIds.Contains(input.UserId.Value);
            var isStudent = input.UserId != null && studentAccountUserId.HasValue && input.UserId.Value == studentAccountUserId.Value;

            if (match != null)
            {
                match.TeamRole = input.TeamRole;
                match.IsRequired = input.IsRequired ?? true;
                match.IsFamily = isFamily;
                match.IsStudent = isStudent;
                match.UpdatedById = creatorUserId;
                keep.Add(match);
            }
            else
            {
                var created = new MeetingParticipant
                {
                    MeetingId = meeting.Id,
                    UserId = input.UserId,
                    ExternalName = NormalizeOptional(input.ExternalName),
                    ExternalEmail = NormalizeOptional(input.ExternalEmail),
                    TeamRole = input.TeamRole,
                    IsRequired = input.IsRequired ?? true,
                    InviteStatus = InviteStatus.Pending,
                    IsFamily = isFamily,
                    IsStudent = isStudent,
                    RsvpToken = NewRsvpToken(),
                    CreatedById = creatorUserId,
                    UpdatedById = creatorUserId
                };
                await _context.MeetingParticipants.AddAsync(created, ct);
                keep.Add(created);
            }
        }

        foreach (var stale in existing.Where(p => !keep.Contains(p)))
            _context.MeetingParticipants.Remove(stale);

        return null;
    }

    private static string NewRsvpToken() => Guid.NewGuid().ToString("N");

    // ----------------------------------------------------------------- Notifications

    private async Task NotifyParticipantsAsync(int meetingId, NotificationKind kind, string changeWord, CancellationToken ct)
    {
        var meeting = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants)
            .Include(m => m.SchoolStudent)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return;

        var userIds = meeting.Participants.Where(p => p.UserId != null).Select(p => p.UserId!.Value).Distinct().ToList();
        if (userIds.Count == 0)
            return;

        var studentName = $"{meeting.SchoolStudent.FirstName} {meeting.SchoolStudent.LastName}".Trim();
        var title = $"Meeting {changeWord}: {meeting.Title}";
        var body = $"{meeting.Title} for {studentName} on {meeting.StartsAtUtc:MMMM d, yyyy 'at' h:mm tt} ({meeting.TimeZoneId}) was {changeWord}.";
        var linkPath = $"/meetings/{meeting.Id}";
        // Include the sequence so a later reschedule (which bumps sequence) is a fresh dedup key even
        // within 24h of a previous notification for the same meeting.
        var dedupKey = $"meeting-{meeting.Id}-{meeting.Sequence}-{kind}";

        try
        {
            await _notifications.NotifyAsync(userIds, kind, title, body, linkPath, dedupKey, emailImmediately: true, ct);
        }
        catch (Exception ex)
        {
            // Notification failure must never roll back or fail the meeting mutation that already committed.
            _logger.LogError(ex, "Failed to queue {Kind} notifications for meeting {MeetingId}", kind, meetingId);
        }
    }

    // ----------------------------------------------------------------- Mapping

    private async Task<MeetingModel> LoadMeetingModelAsync(int meetingId, int viewerUserId, CancellationToken ct)
    {
        var meeting = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .Include(m => m.SchoolStudent)
            .FirstAsync(m => m.Id == meetingId, ct);
        var canManage = await CanManageAsync(viewerUserId, meeting, ct);
        return MapMeeting(meeting, viewerUserId, canManage);
    }

    private static MeetingModel MapMeeting(Meeting m, int viewerUserId, bool canManage) => new()
    {
        Id = m.Id,
        SchoolStudentId = m.SchoolStudentId,
        StudentName = $"{m.SchoolStudent.FirstName} {m.SchoolStudent.LastName}".Trim(),
        Type = m.Type,
        Title = m.Title,
        StartsAtUtc = m.StartsAtUtc,
        TimeZoneId = m.TimeZoneId,
        DurationMinutes = m.DurationMinutes,
        Location = m.Location,
        VideoUrl = m.VideoUrl,
        Status = m.Status,
        DocumentInstanceId = m.DocumentInstanceId,
        Notes = m.Notes,
        Sequence = m.Sequence,
        CreatedByUserId = m.CreatedByUserId,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Participants = m.Participants.OrderBy(p => p.Id).Select(MapParticipant).ToList(),
        MyInviteStatus = m.Participants.FirstOrDefault(p => p.UserId == viewerUserId)?.InviteStatus,
        CanManage = canManage
    };

    private static MeetingParticipantModel MapParticipant(MeetingParticipant p) => new()
    {
        Id = p.Id,
        UserId = p.UserId,
        ExternalName = p.ExternalName,
        ExternalEmail = p.ExternalEmail,
        DisplayName = p.User != null ? $"{p.User.FirstName} {p.User.LastName}".Trim() : (p.ExternalName ?? p.ExternalEmail ?? "Participant"),
        TeamRole = p.TeamRole,
        IsRequired = p.IsRequired,
        InviteStatus = p.InviteStatus,
        Attended = p.Attended,
        ExcusedAt = p.ExcusedAt,
        ExcusalNote = p.ExcusalNote,
        IsFamily = p.IsFamily,
        IsStudent = p.IsStudent
    };

    private static bool IsValidTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
