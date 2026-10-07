using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Localization;
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

    /// <summary>GetForStudentAsync is unbounded career history; cap it so a long-enrolled student's meeting
    /// list stays a single bounded query (todos/050).</summary>
    private const int MaxStudentMeetingHistory = 200;

    private const int MinPlausibleYear = 2000;

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;
    private readonly IAccessService _accessService;
    private readonly INotificationService _notifications;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MeetingService> _logger;
    private readonly IStringLocalizer<Messages> _localizer;
    private readonly IStringLocalizer<Notifications> _notificationsLocalizer;

    public MeetingService(
        ApplicationDbContext context,
        IOrgAccessService orgAccess,
        IAccessService accessService,
        INotificationService notifications,
        IAuditLogger audit,
        ILogger<MeetingService> logger,
        IStringLocalizer<Messages> localizer,
        IStringLocalizer<Notifications> notificationsLocalizer)
    {
        _context = context;
        _orgAccess = orgAccess;
        _accessService = accessService;
        _notifications = notifications;
        _audit = audit;
        _logger = logger;
        _localizer = localizer;
        _notificationsLocalizer = notificationsLocalizer;
    }

    // ----------------------------------------------------------------- Create

    public async Task<ServiceResult<MeetingModel>> CreateAsync(int userId, int studentId, CreateMeetingModel model, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Collaborator, ct))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToSchedule"]);

        var student = await _context.SchoolStudents.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student == null)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Educator.StudentNotFound"]);

        if (model.StartsAtUtc.Year < MinPlausibleYear)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.StartsAtInvalid"]);

        var timeZoneId = string.IsNullOrWhiteSpace(model.TimeZoneId) ? DefaultTimeZoneId : model.TimeZoneId.Trim();
        if (!IsValidTimeZone(timeZoneId))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.InvalidTimeZone"]);

        var duration = model.DurationMinutes ?? DefaultDurationMinutes;
        if (duration < MinDurationMinutes || duration > MaxDurationMinutes)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.DurationRange", MinDurationMinutes, MaxDurationMinutes]);

        if (HasControlCharacters(model.Title))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.TitleInvalidCharacters"]);
        if (HasControlCharacters(model.Location))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.LocationInvalidCharacters"]);
        if (!IsSafeHttpUrl(model.VideoUrl))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.VideoLinkInvalid"]);

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

        var membership = await LoadMembershipContextAsync(studentId, ct);
        var participants = model.Participants ?? await BuildDefaultParticipantInputsAsync(studentId, userId, membership, ct);
        var participantError = await ReplaceParticipantsAsync(meeting, participants, membership, userId, ct);
        if (participantError != null)
        {
            _context.Meetings.Remove(meeting);
            await _context.SaveChangesAsync(ct);
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, participantError);
        }
        await _context.SaveChangesAsync(ct);

        _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
        await NotifyParticipantsAsync(meeting.Id, NotificationKind.MeetingScheduled, ct);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    // ----------------------------------------------------------------- Read

    public async Task<ServiceResult<List<DefaultParticipantModel>>> GetDefaultParticipantsAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Collaborator, ct))
            return ServiceResult<List<DefaultParticipantModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToScheduleMeetings"]);

        var membership = await LoadMembershipContextAsync(studentId, ct);
        var inputs = await BuildDefaultParticipantInputsAsync(studentId, userId, membership, ct);
        var userIds = inputs.Where(i => i.UserId.HasValue).Select(i => i.UserId!.Value).ToList();
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
                    IsFamily = membership.FamilyUserIds.Contains(u.Id),
                    IsStudent = membership.StudentAccountUserId.HasValue && u.Id == membership.StudentAccountUserId.Value
                };
            })
            .OrderBy(m => m.IsStudent).ThenBy(m => m.IsFamily).ThenBy(m => m.DisplayName)
            .ToList();
        return ServiceResult<List<DefaultParticipantModel>>.SuccessResult(result);
    }

    public async Task<ServiceResult<List<MeetingModel>>> GetForStudentAsync(int userId, int studentId, CancellationToken ct = default)
    {
        if (!await _orgAccess.CanActOnStudentAsync(userId, studentId, AccessRole.Viewer, ct))
            return ServiceResult<List<MeetingModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToViewStudentMeetings"]);

        var meetings = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .Include(m => m.SchoolStudent)
            .Where(m => m.SchoolStudentId == studentId)
            .OrderByDescending(m => m.StartsAtUtc)
            .Take(MaxStudentMeetingHistory)
            .ToListAsync(ct);

        var models = await MapMeetingsAsync(meetings, userId, ct);
        return ServiceResult<List<MeetingModel>>.SuccessResult(models);
    }

    public async Task<ServiceResult<MeetingModel>> GetAsync(int userId, int meetingId, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.NotFound"]);

        var isParticipant = meeting.Participants.Any(p => p.UserId == userId);
        if (!isParticipant && !await _orgAccess.CanActOnStudentAsync(userId, meeting.SchoolStudentId, AccessRole.Viewer, ct))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToView"]);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meetingId, userId, ct));
    }

    public async Task<ServiceResult<List<MeetingModel>>> ListMineAsync(int userId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var fromUtc = from.HasValue ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc) : DateTime.UtcNow;
        var toUtc = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : DateTime.UtcNow.AddDays(90);

        var meetings = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .Include(m => m.SchoolStudent)
            .Where(m => m.StartsAtUtc >= fromUtc && m.StartsAtUtc <= toUtc && m.Participants.Any(p => p.UserId == userId))
            .OrderBy(m => m.StartsAtUtc)
            .ToListAsync(ct);

        var models = await MapMeetingsAsync(meetings, userId, ct);
        return ServiceResult<List<MeetingModel>>.SuccessResult(models);
    }

    public async Task<ServiceResult<List<MeetingModel>>> ListForChildAsync(int parentUserId, int childProfileId, CancellationToken ct = default)
    {
        if (!await _accessService.HasMinimumRoleAsync(childProfileId, parentUserId, AccessRole.Viewer, ct))
            return ServiceResult<List<MeetingModel>>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToViewChildMeetings"]);

        var studentIds = await _context.ChildLinks.AsNoTracking()
            .Where(l => l.ChildProfileId == childProfileId && l.IsActive && l.AcceptedAt != null)
            .Select(l => l.SchoolStudentId)
            .Distinct()
            .ToListAsync(ct);
        if (studentIds.Count == 0)
            return ServiceResult<List<MeetingModel>>.SuccessResult(new List<MeetingModel>());

        var meetings = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .Include(m => m.SchoolStudent)
            .Where(m => studentIds.Contains(m.SchoolStudentId))
            .OrderBy(m => m.StartsAtUtc)
            .ToListAsync(ct);

        var models = await MapMeetingsAsync(meetings, parentUserId, ct);
        return ServiceResult<List<MeetingModel>>.SuccessResult(models);
    }

    // ----------------------------------------------------------------- Update / cancel / status / attendance

    public async Task<ServiceResult<MeetingModel>> UpdateAsync(int userId, int meetingId, UpdateMeetingModel model, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.MeetingNotFound"]);
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToUpdate"]);
        if (meeting.Status == MeetingStatus.Cancelled)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.CannotUpdateCancelled"]);

        if (model.StartsAtUtc.HasValue && model.StartsAtUtc.Value.Year < MinPlausibleYear)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.StartsAtInvalid"]);
        if (HasControlCharacters(model.Title))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.TitleInvalidCharacters"]);
        if (HasControlCharacters(model.Location))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.LocationInvalidCharacters"]);
        if (!IsSafeHttpUrl(model.VideoUrl))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.VideoLinkInvalid"]);

        var scheduleChanged = false;

        if (model.StartsAtUtc.HasValue)
        {
            var utc = DateTime.SpecifyKind(model.StartsAtUtc.Value, DateTimeKind.Utc);
            if (utc != meeting.StartsAtUtc) { meeting.StartsAtUtc = utc; scheduleChanged = true; }
        }
        if (model.DurationMinutes.HasValue)
        {
            if (model.DurationMinutes.Value < MinDurationMinutes || model.DurationMinutes.Value > MaxDurationMinutes)
                return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.DurationRange", MinDurationMinutes, MaxDurationMinutes]);
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
                return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.InvalidTimeZone"]);
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
            var membership = await LoadMembershipContextAsync(meeting.SchoolStudentId, ct);
            var participantError = await ReplaceParticipantsAsync(meeting, model.Participants, membership, meeting.CreatedByUserId, ct);
            if (participantError != null)
                return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, participantError);
            await _context.SaveChangesAsync(ct);
        }

        _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
        if (scheduleChanged)
            await NotifyParticipantsAsync(meeting.Id, NotificationKind.MeetingUpdated, ct);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingModel>> CancelAsync(int userId, int meetingId, string? reason, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.MeetingNotFound"]);
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToCancel"]);

        if (meeting.Status != MeetingStatus.Cancelled)
        {
            meeting.Status = MeetingStatus.Cancelled;
            meeting.Sequence++;
            meeting.CancelReason = NormalizeOptional(reason);
            meeting.UpdatedById = userId;
            await _context.SaveChangesAsync(ct);

            _audit.Record(AuditAction.Edit, userId, "Meeting", meeting.Id);
            await NotifyParticipantsAsync(meeting.Id, NotificationKind.MeetingCancelled, ct);
        }

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingModel>> SetStatusAsync(int userId, int meetingId, MeetingStatus status, CancellationToken ct = default)
    {
        if (status is not (MeetingStatus.Scheduled or MeetingStatus.Held or MeetingStatus.Continued))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.InvalidStatusValue"]);

        var meeting = await _context.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.MeetingNotFound"]);
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToUpdate"]);
        if (meeting.Status == MeetingStatus.Cancelled)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.CannotChangeStatusCancelled"]);

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
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.MeetingNotFound"]);
        if (!await CanManageAsync(userId, meeting, ct))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToUpdate"]);
        if (meeting.Status is not (MeetingStatus.Held or MeetingStatus.Continued))
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Validation, _localizer["Meetings.AttendanceOnlyHeld"]);

        foreach (var item in items)
        {
            var participant = meeting.Participants.FirstOrDefault(p => p.Id == item.ParticipantId);
            if (participant == null)
                return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.ParticipantNotFound"]);

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
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.NotFound, _localizer["Meetings.NotFound"]);

        var participant = meeting.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant == null)
            return ServiceResult<MeetingModel>.FailureResult(ServiceErrorKind.Forbidden, _localizer["Meetings.NoPermissionToRespond"]);

        participant.InviteStatus = status;
        participant.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<MeetingModel>.SuccessResult(await LoadMeetingModelAsync(meeting.Id, userId, ct));
    }

    public async Task<ServiceResult<MeetingRsvpPreviewModel>> GetByRsvpTokenAsync(string token, CancellationToken ct = default)
    {
        var (participant, error) = await FindValidRsvpParticipantAsync(token, ct);
        if (error != null)
            return ServiceResult<MeetingRsvpPreviewModel>.FailureResult(ServiceErrorKind.Validation, error);

        var summary = await LoadMeetingSummaryAsync(participant!.MeetingId, ct);
        return ServiceResult<MeetingRsvpPreviewModel>.SuccessResult(new MeetingRsvpPreviewModel
        {
            Meeting = summary,
            Status = participant.InviteStatus
        });
    }

    public async Task<ServiceResult<MeetingRsvpPreviewModel>> RsvpByTokenAsync(string token, InviteStatus status, CancellationToken ct = default)
    {
        var (participant, error) = await FindValidRsvpParticipantAsync(token, ct);
        if (error != null)
            return ServiceResult<MeetingRsvpPreviewModel>.FailureResult(ServiceErrorKind.Validation, error);

        participant!.InviteStatus = status;
        await _context.SaveChangesAsync(ct);

        var summary = await LoadMeetingSummaryAsync(participant.MeetingId, ct);
        return ServiceResult<MeetingRsvpPreviewModel>.SuccessResult(new MeetingRsvpPreviewModel
        {
            Meeting = summary,
            Status = participant.InviteStatus
        });
    }

    private async Task<(MeetingParticipant? Participant, string? Error)> FindValidRsvpParticipantAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (null, _localizer["Meetings.InvalidRsvpLink"]);

        var participant = await _context.MeetingParticipants
            .Include(p => p.Meeting)
            .FirstOrDefaultAsync(p => p.RsvpToken == token, ct);
        if (participant == null)
            return (null, _localizer["Meetings.InvalidRsvpLink"]);
        if (participant.Meeting.StartsAtUtc <= DateTime.UtcNow)
            return (null, _localizer["Meetings.RsvpWindowClosed"]);

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

    /// <summary>The facts "who counts as family/student/district-admin for this student" — computed once
    /// per logical operation (create, or an explicit participant replacement) and threaded through, instead
    /// of each of <see cref="BuildDefaultParticipantInputsAsync"/>/<see cref="GetDefaultParticipantsAsync"/>/
    /// <see cref="ReplaceParticipantsAsync"/> independently re-querying the identical rows (todos/071 #3).</summary>
    private sealed class MembershipContext
    {
        public HashSet<int> FamilyUserIds { get; init; } = new();
        public int? StudentAccountUserId { get; init; }
        public HashSet<int> DistrictAdminUserIds { get; init; } = new();
    }

    private async Task<MembershipContext> LoadMembershipContextAsync(int studentId, CancellationToken ct)
    {
        var familyUserIds = await LoadFamilyUserIdsAsync(studentId, ct);
        var studentAccountUserId = await _context.StudentProfiles.AsNoTracking()
            .Where(sp => sp.SchoolStudentId == studentId)
            .Select(sp => (int?)sp.UserId)
            .FirstOrDefaultAsync(ct);
        var districtAdminUserIds = await _context.StaffProfiles.AsNoTracking()
            .Where(p => p.IsActive && p.OrgRoleId == OrgRoleIds.DistrictAdmin)
            .Select(p => p.UserId)
            .ToListAsync(ct);

        return new MembershipContext
        {
            FamilyUserIds = familyUserIds.ToHashSet(),
            StudentAccountUserId = studentAccountUserId,
            DistrictAdminUserIds = districtAdminUserIds.ToHashSet()
        };
    }

    /// <summary>Default roster: active team members (their TeamRole), accepted family links (isFamily),
    /// the student's own account if one exists (isStudent). The creator is added separately by
    /// <see cref="ReplaceParticipantsAsync"/>. DistrictAdmins are excluded unless they are the creator.</summary>
    private async Task<List<ParticipantInputModel>> BuildDefaultParticipantInputsAsync(int studentId, int creatorUserId, MembershipContext membership, CancellationToken ct)
    {
        var teamMembers = await _context.StudentTeamMembers.AsNoTracking()
            .Where(m => m.SchoolStudentId == studentId && m.IsActive)
            .Select(m => new { m.UserId, m.TeamRole })
            .ToListAsync(ct);

        var byUserId = new Dictionary<int, ParticipantInputModel>();
        foreach (var m in teamMembers)
        {
            if (membership.DistrictAdminUserIds.Contains(m.UserId) && m.UserId != creatorUserId)
                continue;
            byUserId[m.UserId] = new ParticipantInputModel { UserId = m.UserId, TeamRole = m.TeamRole, IsRequired = true };
        }

        foreach (var uid in membership.FamilyUserIds)
        {
            if (membership.DistrictAdminUserIds.Contains(uid) && uid != creatorUserId)
                continue;
            if (!byUserId.ContainsKey(uid))
                byUserId[uid] = new ParticipantInputModel { UserId = uid, TeamRole = TeamRole.Other, IsRequired = true };
        }

        if (membership.StudentAccountUserId.HasValue && !byUserId.ContainsKey(membership.StudentAccountUserId.Value)
            && !(membership.DistrictAdminUserIds.Contains(membership.StudentAccountUserId.Value) && membership.StudentAccountUserId.Value != creatorUserId))
        {
            byUserId[membership.StudentAccountUserId.Value] = new ParticipantInputModel { UserId = membership.StudentAccountUserId.Value, TeamRole = TeamRole.Other, IsRequired = true };
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
    private async Task<string?> ReplaceParticipantsAsync(Meeting meeting, List<ParticipantInputModel> inputs, MembershipContext membership, int creatorUserId, CancellationToken ct)
    {
        var normalized = new List<ParticipantInputModel>(inputs);
        if (!normalized.Any(i => i.UserId == creatorUserId))
            normalized.Add(new ParticipantInputModel { UserId = creatorUserId, TeamRole = TeamRole.Other, IsRequired = true });

        foreach (var input in normalized)
        {
            if (input.UserId == null && string.IsNullOrWhiteSpace(input.ExternalEmail))
                return _localizer["Meetings.ParticipantUserOrEmailRequired"];
            if (HasControlCharacters(input.ExternalName) || HasControlCharacters(input.ExternalEmail))
                return _localizer["Meetings.ParticipantInvalidCharacters"];
            if (!string.IsNullOrWhiteSpace(input.ExternalEmail) && !IsValidEmail(input.ExternalEmail))
                return _localizer["Meetings.ParticipantEmailInvalid"];
        }
        var dupUserIds = normalized.Where(i => i.UserId != null).GroupBy(i => i.UserId!.Value).Where(g => g.Count() > 1);
        if (dupUserIds.Any())
            return _localizer["Meetings.ParticipantDuplicate"];

        var familyUserIds = membership.FamilyUserIds;
        var studentAccountUserId = membership.StudentAccountUserId;

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

    private async Task NotifyParticipantsAsync(int meetingId, NotificationKind kind, CancellationToken ct)
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
        var linkPath = $"/meetings/{meeting.Id}";
        // Include the sequence so a later reschedule (which bumps sequence) is a fresh dedup key even
        // within 24h of a previous notification for the same meeting.
        var dedupKey = $"meeting-{meeting.Id}-{meeting.Sequence}-{kind}";

        // Multilingual plan (2026-10-06) phase 4: one resx key pair per kind (never a word substituted
        // into one template) because "scheduled"/"updated"/"cancelled" require different Spanish past
        // participles (feminine agreement with "reunión"), not a literal translation of the English word.
        var (titleKey, bodyKey) = kind switch
        {
            NotificationKind.MeetingScheduled => ("Notifications.MeetingScheduled.Title", "Notifications.MeetingScheduled.Body"),
            NotificationKind.MeetingCancelled => ("Notifications.MeetingCancelled.Title", "Notifications.MeetingCancelled.Body"),
            _ => ("Notifications.MeetingUpdated.Title", "Notifications.MeetingUpdated.Body")
        };

        (string Title, string Body) BuildText(string lang)
        {
            // Explicit per-recipient-language date, never the ambient CurrentCulture (CultureScope
            // deliberately leaves it alone) — see LocalizedDateFormat's doc comment. Shared with
            // EmailService's meeting invitation/update/cancellation emails so the bell/email notification
            // and the email never render this meeting's date-time two different ways.
            var formattedDate = LocalizedDateFormat.MeetingDateTime(meeting.StartsAtUtc, lang);
            var title = _notificationsLocalizer[titleKey, meeting.Title];
            var body = _notificationsLocalizer[bodyKey, meeting.Title, studentName, formattedDate, meeting.TimeZoneId];
            return (title, body);
        }

        try
        {
            await _notifications.NotifyAsync(userIds, kind, BuildText, linkPath, dedupKey, emailImmediately: true, ct);
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

    /// <summary>Minimal, non-participant projection for the anonymous token RSVP endpoints — see
    /// <see cref="MeetingSummaryModel"/> (todos/048).</summary>
    private Task<MeetingSummaryModel> LoadMeetingSummaryAsync(int meetingId, CancellationToken ct) =>
        _context.Meetings.AsNoTracking()
            .Where(m => m.Id == meetingId)
            .Select(m => new MeetingSummaryModel
            {
                Id = m.Id,
                StudentFirstName = m.SchoolStudent.FirstName,
                Type = m.Type,
                Title = m.Title,
                StartsAtUtc = m.StartsAtUtc,
                TimeZoneId = m.TimeZoneId,
                DurationMinutes = m.DurationMinutes,
                Location = m.Location,
                Status = m.Status
            })
            .FirstAsync(ct);

    /// <summary>Maps a batch of already-loaded meetings (Participants.User + SchoolStudent included) to
    /// <see cref="MeetingModel"/>s, resolving <c>CanManage</c> with the viewer's staff context fetched once
    /// and the lead case manager for every distinct student batch-loaded in one query — instead of
    /// <see cref="CanManageAsync"/>'s per-meeting <c>GetStaffContextAsync</c> + <c>SchoolStudents</c> lookup
    /// (todos/050, todos/052).</summary>
    private async Task<List<MeetingModel>> MapMeetingsAsync(List<Meeting> meetings, int viewerUserId, CancellationToken ct)
    {
        if (meetings.Count == 0)
            return new List<MeetingModel>();

        var staffCtx = await _orgAccess.GetStaffContextAsync(viewerUserId, ct);
        var isAdmin = staffCtx != null && OrgRoleIds.IsAdmin(staffCtx.OrgRoleId);

        var studentIds = meetings.Select(m => m.SchoolStudentId).Distinct().ToList();
        var caseManagerByStudent = await _context.SchoolStudents.AsNoTracking()
            .Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, s.CaseManagerUserId })
            .ToDictionaryAsync(s => s.Id, s => s.CaseManagerUserId, ct);

        var models = new List<MeetingModel>(meetings.Count);
        foreach (var meeting in meetings)
        {
            bool canManage;
            if (meeting.CreatedByUserId == viewerUserId)
                canManage = true;
            else if (isAdmin && await _orgAccess.CanActOnStudentAsync(viewerUserId, meeting.SchoolStudentId, AccessRole.Viewer, ct))
                canManage = true;
            else
                canManage = caseManagerByStudent.TryGetValue(meeting.SchoolStudentId, out var leadId) && leadId.HasValue && leadId.Value == viewerUserId;

            models.Add(MapMeeting(meeting, viewerUserId, canManage));
        }
        return models;
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

    // ----------------------------------------------------------------- Input validation (todos/065)

    private static readonly EmailAddressAttribute EmailValidator = new();

    /// <summary>Only absolute http/https links are accepted for the video call — the web renders it as an href.</summary>
    private static bool IsSafeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool HasControlCharacters(string? value) => value != null && value.Any(char.IsControl);

    private static bool IsValidEmail(string value) => EmailValidator.IsValid(value);
}
