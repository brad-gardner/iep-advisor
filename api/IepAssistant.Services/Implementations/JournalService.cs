using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Data.Configurations;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Journal entries are markdown-at-rest: content goes through <see cref="RichTextSanitizer"/> BEFORE the
/// length check so the stored value is what was measured. Access failures and unknown ids collapse to the
/// same "not found" message so a caller can never probe whether an entry or child exists.
/// </summary>
public class JournalService : IJournalService
{
    public const int MaxContentLength = JournalEntryConfiguration.ContentMaxLength;
    public const int DefaultTake = 50;
    public const int MaxTake = 200;

    private const string ChildNotFound = "Child profile not found.";
    private const string EntryNotFound = "Journal entry not found.";

    private readonly ApplicationDbContext _context;
    private readonly IAccessService _access;

    public JournalService(ApplicationDbContext context, IAccessService access)
    {
        _context = context;
        _access = access;
    }

    public async Task<ServiceResult<List<JournalEntryModel>>> GetForChildAsync(int childId, int userId, JournalTag? tag = null, int? take = null, CancellationToken ct = default)
    {
        if (!await _access.HasMinimumRoleAsync(childId, userId, AccessRole.Viewer, ct))
            return ServiceResult<List<JournalEntryModel>>.FailureResult(ChildNotFound);
        if (tag is { } t && !Enum.IsDefined(t))
            return ServiceResult<List<JournalEntryModel>>.FailureResult("Invalid tag.");
        if (take is < 1 or > MaxTake)
            return ServiceResult<List<JournalEntryModel>>.FailureResult($"take must be between 1 and {MaxTake}.");

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
            return ServiceResult<JournalEntryModel>.FailureResult(ChildNotFound);

        var (content, error) = await ValidateAsync(childId, model, ct);
        if (error != null) return ServiceResult<JournalEntryModel>.FailureResult(error);

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
            return ServiceResult<JournalEntryModel>.FailureResult(EntryNotFound);

        var (content, error) = await ValidateAsync(entity.ChildProfileId, model, ct);
        if (error != null) return ServiceResult<JournalEntryModel>.FailureResult(error);

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
            return ServiceResult.FailureResult(EntryNotFound);
        _context.JournalEntries.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return ServiceResult.SuccessResult();
    }

    /// <summary>Returns the sanitized content to persist, or an error. Sanitize first, then measure.</summary>
    private async Task<(string? Content, string? Error)> ValidateAsync(int childId, SaveJournalEntryModel model, CancellationToken ct)
    {
        if (!Enum.IsDefined(model.Tag)) return (null, "Invalid tag.");
        if (string.IsNullOrWhiteSpace(model.ContentMarkdown)) return (null, "Content is required.");

        var content = RichTextSanitizer.Sanitize(model.ContentMarkdown).Trim();
        if (content.Length == 0) return (null, "Content is required.");
        if (content.Length > MaxContentLength) return (null, $"Content must be {MaxContentLength} characters or fewer.");

        if (model.OccurredOn > DateOnly.FromDateTime(DateTime.UtcNow))
            return (null, "Date cannot be in the future.");

        // Every optional link must resolve for THIS child — an id from another child is reported exactly
        // like a nonexistent one (a 400, worded without "not found" so the controller does not map it to 404).
        if (model.LinkedIepDocumentId is { } iepId
            && !await _context.IepDocuments.AsNoTracking().AnyAsync(d => d.Id == iepId && d.ChildProfileId == childId && d.IsActive, ct))
            return (null, "Linked IEP document is not available for this child.");

        if (model.LinkedEtrDocumentId is { } etrId
            && !await _context.EtrDocuments.AsNoTracking().AnyAsync(d => d.Id == etrId && d.ChildProfileId == childId && d.IsActive, ct))
            return (null, "Linked ETR document is not available for this child.");

        // Meetings belong to a SchoolStudent; a parent sees them through an accepted, active ChildLink
        // (same rule as MeetingService.ListForChildAsync). A parent-only child has no links, so no meeting
        // is linkable for it.
        if (model.LinkedMeetingId is { } meetingId
            && !await _context.Meetings.AsNoTracking().AnyAsync(m => m.Id == meetingId
                && _context.ChildLinks.Any(l => l.ChildProfileId == childId && l.IsActive && l.AcceptedAt != null && l.SchoolStudentId == m.SchoolStudentId), ct))
            return (null, "Linked meeting is not available for this child.");

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
