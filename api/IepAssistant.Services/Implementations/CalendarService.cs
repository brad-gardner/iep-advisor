using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>Staff/parent calendar aggregation + ICS export (see <see cref="ICalendarService"/>, plan 4 decision 4).</summary>
public class CalendarService : ICalendarService
{
    private const int FeedPastDays = 30;
    private const int FeedFutureDays = 365;

    private readonly ApplicationDbContext _context;
    private readonly IMeetingService _meetingService;
    private readonly IObligationService _obligationService;
    private readonly IOrgAccessService _orgAccess;
    private readonly IIcsBuilder _icsBuilder;

    public CalendarService(
        ApplicationDbContext context,
        IMeetingService meetingService,
        IObligationService obligationService,
        IOrgAccessService orgAccess,
        IIcsBuilder icsBuilder)
    {
        _context = context;
        _meetingService = meetingService;
        _obligationService = obligationService;
        _orgAccess = orgAccess;
        _icsBuilder = icsBuilder;
    }

    public async Task<ServiceResult<List<CalendarItemModel>>> GetMineAsync(int userId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var meetingsResult = await _meetingService.ListMineAsync(userId, from, to, ct);
        var obligationsResult = await _obligationService.GetMineAsync(userId, null, ct);

        var items = new List<CalendarItemModel>();
        if (meetingsResult.Success)
            items.AddRange(meetingsResult.Data!.Select(m => new CalendarItemModel { Kind = "Meeting", Date = m.StartsAtUtc, Meeting = m }));
        if (obligationsResult.Success)
        {
            items.AddRange(obligationsResult.Data!
                .Where(o => o.DueDate.HasValue)
                .Select(o => new CalendarItemModel { Kind = "Obligation", Date = o.DueDate!.Value, Obligation = o }));
        }

        return ServiceResult<List<CalendarItemModel>>.SuccessResult(items.OrderBy(i => i.Date).ToList());
    }

    public async Task<ServiceResult<CalendarFeedModel>> GetOrCreateFeedAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return ServiceResult<CalendarFeedModel>.FailureResult("User not found.");

        if (string.IsNullOrEmpty(user.CalendarFeedToken))
        {
            user.CalendarFeedToken = NewToken();
            user.CalendarFeedTokenCreatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return ServiceResult<CalendarFeedModel>.SuccessResult(BuildFeedModel(user));
    }

    public async Task<ServiceResult<CalendarFeedModel>> RegenerateFeedAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return ServiceResult<CalendarFeedModel>.FailureResult("User not found.");

        user.CalendarFeedToken = NewToken();
        user.CalendarFeedTokenCreatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return ServiceResult<CalendarFeedModel>.SuccessResult(BuildFeedModel(user));
    }

    public async Task<ServiceResult<byte[]>> GetFeedByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ServiceResult<byte[]>.FailureResult("Invalid calendar feed link.");

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.CalendarFeedToken == token, ct);
        if (user == null)
            return ServiceResult<byte[]>.FailureResult("Invalid calendar feed link.");

        var now = DateTime.UtcNow;
        var meetings = await _context.Meetings.AsNoTracking()
            .Where(m => (m.Status == MeetingStatus.Scheduled || m.Status == MeetingStatus.Held)
                     && m.StartsAtUtc >= now.AddDays(-FeedPastDays) && m.StartsAtUtc <= now.AddDays(FeedFutureDays)
                     && m.Participants.Any(p => p.UserId == user.Id))
            .Select(m => new { m.Id, m.Title, m.StartsAtUtc, m.TimeZoneId, m.DurationMinutes, m.Location, m.VideoUrl, m.Sequence, StudentName = m.SchoolStudent.FirstName + " " + m.SchoolStudent.LastName })
            .ToListAsync(ct);

        var events = meetings.Select(m => new IcsFeedEvent
        {
            Uid = $"meeting-{m.Id}@iep-advisor",
            Summary = $"{m.Title} — {m.StudentName}".Trim(),
            Description = string.IsNullOrWhiteSpace(m.VideoUrl) ? null : $"Video: {m.VideoUrl}",
            Location = m.Location,
            StartsAtUtc = m.StartsAtUtc,
            DurationMinutes = m.DurationMinutes,
            TimeZoneId = m.TimeZoneId,
            Sequence = m.Sequence
        }).ToList();

        var obligationsResult = await _obligationService.GetMineAsync(user.Id, null, ct);
        if (obligationsResult.Success)
        {
            events.AddRange(obligationsResult.Data!
                .Where(o => o.DueDate.HasValue)
                .Select(o => new IcsFeedEvent
                {
                    Uid = $"obligation-{o.Kind}-{o.SchoolStudentId}@iep-advisor",
                    Summary = $"{o.Kind} due — {o.StudentName}",
                    Description = o.SourceLabel,
                    AllDayDate = DateOnly.FromDateTime(o.DueDate!.Value)
                }));
        }

        var ics = _icsBuilder.BuildFeed($"{user.FirstName} {user.LastName} — IEP Advisor".Trim(), events);
        return ServiceResult<byte[]>.SuccessResult(ics);
    }

    public async Task<ServiceResult<byte[]>> GetMeetingIcsAsync(int userId, int meetingId, CancellationToken ct = default)
    {
        var meeting = await _context.Meetings.AsNoTracking()
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .Include(m => m.SchoolStudent)
            .Include(m => m.CreatedByUser)
            .FirstOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting == null)
            return ServiceResult<byte[]>.FailureResult("Meeting not found.");

        if (!await AuthorizeMeetingReadAsync(userId, meeting, ct))
            return ServiceResult<byte[]>.FailureResult("You do not have permission to view this meeting.");

        var input = new IcsMeetingInput
        {
            MeetingId = meeting.Id,
            Title = meeting.Title,
            StartsAtUtc = meeting.StartsAtUtc,
            TimeZoneId = meeting.TimeZoneId,
            DurationMinutes = meeting.DurationMinutes,
            Location = meeting.Location,
            VideoUrl = meeting.VideoUrl,
            Notes = meeting.Notes,
            Sequence = meeting.Sequence,
            IsCancelled = meeting.Status == MeetingStatus.Cancelled,
            OrganizerName = meeting.CreatedByUser != null ? $"{meeting.CreatedByUser.FirstName} {meeting.CreatedByUser.LastName}".Trim() : "Organizer",
            OrganizerEmail = meeting.CreatedByUser?.Email ?? "no-reply@iep-advisor.com",
            Attendees = meeting.Participants.Select(p => new IcsAttendee
            {
                Name = p.User != null ? $"{p.User.FirstName} {p.User.LastName}".Trim() : p.ExternalName,
                Email = p.User != null ? p.User.Email : (p.ExternalEmail ?? string.Empty),
                IsRequired = p.IsRequired,
                InviteStatus = p.InviteStatus
            }).Where(a => !string.IsNullOrWhiteSpace(a.Email)).ToList()
        };

        var ics = _icsBuilder.BuildMeetingEvent(input, meeting.Status == MeetingStatus.Cancelled ? "CANCEL" : "REQUEST");
        return ServiceResult<byte[]>.SuccessResult(ics);
    }

    private async Task<bool> AuthorizeMeetingReadAsync(int userId, Meeting meeting, CancellationToken ct)
    {
        if (meeting.Participants.Any(p => p.UserId == userId))
            return true;
        return await _orgAccess.CanActOnStudentAsync(userId, meeting.SchoolStudentId, AccessRole.Viewer, ct);
    }

    private static CalendarFeedModel BuildFeedModel(User user) => new()
    {
        Token = user.CalendarFeedToken!,
        CreatedAt = user.CalendarFeedTokenCreatedAt ?? DateTime.UtcNow
    };

    private static string NewToken() => Guid.NewGuid().ToString("N");
}
