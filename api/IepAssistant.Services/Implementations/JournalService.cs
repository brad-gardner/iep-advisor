using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Data.Configurations;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Journal entries are markdown-at-rest: content goes through <see cref="RichTextSanitizer"/> BEFORE the
/// length check so the stored value is what was measured. Access failures and unknown ids collapse to the
/// same "not found" message (and <see cref="ServiceErrorKind.NotFound"/>, never Forbidden) so a caller can
/// never probe whether an entry or child exists.
/// </summary>
public class JournalService : IJournalService
{
    public const int MaxContentLength = JournalEntryConfiguration.ContentMaxLength;
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;
    private readonly IStringLocalizer<Messages> _localizer;

    public JournalService(ApplicationDbContext context, IAccessService access, IStringLocalizer<Messages> localizer)
    {
        _context = context;
        _access = access;
        _localizer = localizer;
    }

    private LocalizedString ChildNotFound => _localizer["Children.NotFound"];
    private LocalizedString EntryNotFound => _localizer["Journal.EntryNotFound"];

    public async Task<ServiceResult<List<JournalEntryModel>>> GetForChildAsync(int childId, int userId, JournalTag? tag = null, int? take = null, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
            return ServiceResult<List<JournalEntryModel>>.FailureResult(ServiceErrorKind.NotFound, ChildNotFound);
        if (tag is { } t && !Enum.IsDefined(t))
            return ServiceResult<List<JournalEntryModel>>.FailureResult(ServiceErrorKind.Validation, _localizer["Journal.InvalidTag"]);
        if (take is < 1 or > MaxTake)
            return ServiceResult<List<JournalEntryModel>>.FailureResult(ServiceErrorKind.Validation, _localizer["Journal.TakeOutOfRange", MaxTake]);

        var query = _context.JournalEntries.AsNoTracking().Where(j => j.ChildProfileId == childId);
        if (tag != null) query = query.Where(j => j.Tag == tag.Value);

        var items = await query
            .OrderByDescending(j => j.OccurredOn).ThenByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id)
            .Take(take ?? DefaultTake)
            .ToListAsync(ct);
        return ServiceResult<List<JournalEntryModel>>.SuccessResult(items.Select(Map).ToList());
    }

    public async Task<ServiceResult<JournalEntryModel>> CreateAsync(int childId, int userId, SaveJournalEntryModel model, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<JournalEntryModel>.FailureResult(ServiceErrorKind.NotFound, ChildNotFound);

        var (content, error) = await ValidateAsync(childId, model, ct);
        if (error != null) return ServiceResult<JournalEntryModel>.FailureResult(ServiceErrorKind.Validation, error);

        var entity = new JournalEntry
        {
            ChildProfileId = childId,
            OccurredOn = model.OccurredOn,
            Tag = model.Tag,
            ContentMarkdown = content!,
            LinkedIepDocumentId = model.LinkedIepDocumentId,
            LinkedEtrDocumentId = model.LinkedEtrDocumentId,
            LinkedMeetingId = model.LinkedMeetingId,
            CreatedById = userId,
            UpdatedById = userId
        };
        _context.JournalEntries.Add(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult<JournalEntryModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult<JournalEntryModel>> UpdateAsync(int entryId, int userId, SaveJournalEntryModel model, CancellationToken ct = default)
    {
        var entity = await _context.JournalEntries.FirstOrDefaultAsync(j => j.Id == entryId, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult<JournalEntryModel>.FailureResult(ServiceErrorKind.NotFound, EntryNotFound);

        var (content, error) = await ValidateAsync(entity.ChildProfileId, model, ct);
        if (error != null) return ServiceResult<JournalEntryModel>.FailureResult(ServiceErrorKind.Validation, error);

        entity.OccurredOn = model.OccurredOn;
        entity.Tag = model.Tag;
        entity.ContentMarkdown = content!;
        entity.LinkedIepDocumentId = model.LinkedIepDocumentId;
        entity.LinkedEtrDocumentId = model.LinkedEtrDocumentId;
        entity.LinkedMeetingId = model.LinkedMeetingId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedById = userId;
        await _context.SaveChangesAsync(ct);
        return ServiceResult<JournalEntryModel>.SuccessResult(Map(entity));
    }

    public async Task<ServiceResult> DeleteAsync(int entryId, int userId, CancellationToken ct = default)
    {
        var entity = await _context.JournalEntries.FirstOrDefaultAsync(j => j.Id == entryId, ct);
        if (entity == null || !await _access.HasMinimumRoleAsync(entity.ChildProfileId, userId, AccessRole.Collaborator, ct))
            return ServiceResult.FailureResult(ServiceErrorKind.NotFound, EntryNotFound);
        _context.JournalEntries.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    /// <summary>Returns the sanitized content to persist, or an error. Sanitize first, then measure.</summary>
    private async Task<(string? Content, string? Error)> ValidateAsync(int childId, SaveJournalEntryModel model, CancellationToken ct)
    {
        if (!Enum.IsDefined(model.Tag)) return (null, _localizer["Journal.InvalidTag"]);
        if (string.IsNullOrWhiteSpace(model.ContentMarkdown)) return (null, _localizer["Journal.ContentRequired"]);

        var content = RichTextSanitizer.Sanitize(model.ContentMarkdown).Trim();
        if (content.Length == 0) return (null, _localizer["Journal.ContentRequired"]);
        if (content.Length > MaxContentLength) return (null, _localizer["Journal.ContentTooLong", MaxContentLength]);

        if (model.OccurredOn > DateOnly.FromDateTime(DateTime.UtcNow))
            return (null, _localizer["Journal.DateInFuture"]);

        // Every optional link must resolve for THIS child — an id from another child is reported exactly
        // like a nonexistent one (a 400 via ErrorKind.Validation, never ErrorKind.NotFound, so the
        // controller never maps it to 404).
        if (model.LinkedIepDocumentId is { } iepId
            && !await _context.IepDocuments.AsNoTracking().AnyAsync(d => d.Id == iepId && d.ChildProfileId == childId && d.IsActive, ct))
            return (null, _localizer["Journal.LinkedIepUnavailable"]);

        if (model.LinkedEtrDocumentId is { } etrId
            && !await _context.EtrDocuments.AsNoTracking().AnyAsync(d => d.Id == etrId && d.ChildProfileId == childId && d.IsActive, ct))
            return (null, _localizer["Journal.LinkedEtrUnavailable"]);

        // Meetings belong to a SchoolStudent; a parent sees them through an accepted, active ChildLink
        // (same rule as MeetingService.ListForChildAsync). A parent-only child has no links, so no meeting
        // is linkable for it.
        if (model.LinkedMeetingId is { } meetingId
            && !await _context.Meetings.AsNoTracking().AnyAsync(m => m.Id == meetingId
                && _context.ChildLinks.Any(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null && l.SchoolStudentId == m.SchoolStudentId), ct))
            return (null, _localizer["Journal.LinkedMeetingUnavailable"]);

        return (content, null);
    }

    private static JournalEntryModel Map(JournalEntry j) => new()
    {
        Id = j.Id,
        ChildProfileId = j.ChildProfileId,
        OccurredOn = j.OccurredOn,
        Tag = j.Tag,
        ContentMarkdown = j.ContentMarkdown,
        LinkedIepDocumentId = j.LinkedIepDocumentId,
        LinkedEtrDocumentId = j.LinkedEtrDocumentId,
        LinkedMeetingId = j.LinkedMeetingId,
        CreatedAt = j.CreatedAt,
        UpdatedAt = j.UpdatedAt,
        CreatedById = j.CreatedById
    };
}
