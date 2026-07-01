using Microsoft.EntityFrameworkCore;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// Read-side of the FERPA access audit trail (Phase 2, district-admin pilot readiness). See
/// <see cref="IAuditLogQueryService"/>. Kept intentionally separate from the write-only
/// <c>AuditLogger</c>/<c>AccessAuditLogWorker</c> path: this class only reads.
/// </summary>
public class AuditLogQueryService : IAuditLogQueryService
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    // The exact ResourceType strings written at the AuditLogger.Record call sites across the codebase.
    private const string ResourceStudent = "SchoolStudent";
    private const string ResourceDraft = "IepDraft";
    private const string ResourceVersion = "IepVersion";

    private readonly ApplicationDbContext _context;
    private readonly IOrgAccessService _orgAccess;

    public AuditLogQueryService(ApplicationDbContext context, IOrgAccessService orgAccess)
    {
        _context = context;
        _orgAccess = orgAccess;
    }

    public async Task<ServiceResult<AuditLogPageModel>> QueryAsync(int userId, AuditLogQuery filters, CancellationToken ct = default)
    {
        // ------- Authorization (actor scope) -------
        var ctx = await _orgAccess.GetStaffContextAsync(userId, ct);
        if (ctx == null || ctx.OrgRoleId == OrgRoleIds.Teacher)
            return ServiceResult<AuditLogPageModel>.FailureResult("You do not have permission to view the activity log.");

        var isDistrictAdmin = ctx.OrgRoleId == OrgRoleIds.DistrictAdmin;

        // A SchoolAdmin with no school binding has nothing to oversee — valid empty page (mirrors the
        // dashboard's handling of the same shouldn't-happen case rather than matching district admins).
        if (!isDistrictAdmin && ctx.SchoolId == null)
            return ServiceResult<AuditLogPageModel>.SuccessResult(new AuditLogPageModel());

        // ------- Filter validation (→ 400) -------
        var pageSize = filters.PageSize ?? DefaultPageSize;
        if (pageSize <= 0)
            return ServiceResult<AuditLogPageModel>.FailureResult("Page size must be greater than zero.");
        if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        if (filters.Cursor is < 0)
            return ServiceResult<AuditLogPageModel>.FailureResult("Cursor must not be negative.");

        AuditAction? action = null;
        if (!string.IsNullOrWhiteSpace(filters.Action))
        {
            if (!Enum.TryParse<AuditAction>(filters.Action.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(typeof(AuditAction), parsed))
                return ServiceResult<AuditLogPageModel>.FailureResult($"Invalid action '{filters.Action}'.");
            action = parsed;
        }

        // ------- Actor scope: StaffProfiles in the caller's district (INCLUDING deactivated staff).
        // SchoolAdmin is confined to own-school actors, which also drops district-admin actors
        // (SchoolId == null) from a SchoolAdmin's view. -------
        //
        // SECURITY INVARIANT: audit rows are scoped by the actor's CURRENT StaffProfile district/school —
        // AccessAuditLog itself records no district/school of its own. This is safe TODAY only because:
        //   (a) a user cannot hold StaffProfiles in two districts — StaffInviteService.AcceptAsync
        //       rejects invites for any already-registered email, so an actor belongs to one district; and
        //   (b) there is no school-reassignment feature, so a staff member's audit history never spans
        //       schools (their current SchoolId matches every row they ever wrote).
        // WARNING: if either invariant is relaxed (multi-district staff, or moving a StaffProfile between
        // schools), this query would over-disclose another scope's student PII, because past rows would be
        // re-scoped to the actor's NEW district/school. The fix is to stamp DistrictId/SchoolId onto
        // AccessAuditLog at write time and scope reads by that stamped value instead of the live profile.
        var actorQuery = _context.StaffProfiles.AsNoTracking()
            .Where(p => p.DistrictId == ctx.DistrictId);
        if (!isDistrictAdmin)
            actorQuery = actorQuery.Where(p => p.SchoolId == ctx.SchoolId);
        if (filters.StaffUserId is int staffUserId)
            actorQuery = actorQuery.Where(p => p.UserId == staffUserId);

        // Keep the actor set as a server-side correlated subquery (EF emits
        // `WHERE a.ActorUserId IN (SELECT p.UserId FROM StaffProfiles p WHERE …)`) rather than
        // materializing every district staff id into a client-side IN(...) list — that would scale with
        // the whole district (parameter-limit / plan-quality risk on SQL Server) instead of with the page.
        var query = _context.AccessAuditLogs.AsNoTracking()
            .Where(a => actorQuery.Select(p => p.UserId).Contains(a.ActorUserId));

        // ------- Filters -------
        if (action != null)
            query = query.Where(a => a.Action == action.Value);
        if (filters.FromUtc is DateTime from)
            query = query.Where(a => a.CreatedAt >= from);
        if (filters.ToUtc is DateTime to)
            query = query.Where(a => a.CreatedAt <= to); // inclusive of that instant

        // Student filter = resource-ID expansion into (ResourceType, ResourceId) pairs.
        if (filters.StudentId is int studentId)
        {
            var draftIds = await _context.IepDrafts.AsNoTracking()
                .Where(d => d.SchoolStudentId == studentId)
                .Select(d => d.Id)
                .ToListAsync(ct);
            // Versions carry SchoolStudentId directly, so seek them off the existing
            // (SchoolStudentId, VersionNumber) index rather than scanning the unindexed SourceDraftId
            // via the draft-id set. Independent of draftIds by design.
            var versionIds = await _context.IepVersions.AsNoTracking()
                .Where(v => v.SchoolStudentId == studentId)
                .Select(v => v.Id)
                .ToListAsync(ct);

            query = query.Where(a =>
                (a.ResourceType == ResourceStudent && a.ResourceId == studentId)
                || (a.ResourceType == ResourceDraft && draftIds.Contains(a.ResourceId))
                || (a.ResourceType == ResourceVersion && versionIds.Contains(a.ResourceId)));
        }

        // ------- Keyset pagination (Id DESC; cursor = last seen Id). Fetch one extra to know if a
        // next page exists without a second round-trip. -------
        if (filters.Cursor is int cursor)
            query = query.Where(a => a.Id < cursor);

        var rows = await query
            .OrderByDescending(a => a.Id)
            .Take(pageSize + 1)
            .Select(a => new RawRow
            {
                Id = a.Id,
                Action = a.Action,
                ActorUserId = a.ActorUserId,
                ResourceType = a.ResourceType,
                ResourceId = a.ResourceId,
                RecipientUserId = a.RecipientUserId,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(ct);

        int? nextCursor = null;
        if (rows.Count > pageSize)
        {
            nextCursor = rows[pageSize - 1].Id; // last Id of the trimmed page
            rows = rows.Take(pageSize).ToList();
        }

        // ------- Batch enrichment (no N+1): per-page dictionaries -------
        var entries = await EnrichAsync(rows, ct);

        return ServiceResult<AuditLogPageModel>.SuccessResult(new AuditLogPageModel
        {
            Entries = entries,
            NextCursor = nextCursor
        });
    }

    /// <summary>
    /// Resolves actor names, recipient names, and resource display names for a page in a fixed number of
    /// queries. Every reference falls back to a stable string ("Former staff member", "Deleted student",
    /// "Draft #N", "Version #N", "Unknown user") — a missing reference never drops the row or throws.
    /// </summary>
    private async Task<List<AuditLogEntryModel>> EnrichAsync(List<RawRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return new List<AuditLogEntryModel>();

        // 1) User names for actors + Share recipients.
        var userIds = rows.Select(r => r.ActorUserId)
            .Concat(rows.Where(r => r.RecipientUserId != null).Select(r => r.RecipientUserId!.Value))
            .Distinct()
            .ToList();
        var userNames = await _context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => FormatName(u.FirstName, u.LastName), ct);

        // 2) Draft → student and version → student joins for resource display names.
        var draftResourceIds = rows.Where(r => r.ResourceType == ResourceDraft).Select(r => r.ResourceId).Distinct().ToList();
        var versionResourceIds = rows.Where(r => r.ResourceType == ResourceVersion).Select(r => r.ResourceId).Distinct().ToList();

        var draftStudentByDraftId = draftResourceIds.Count == 0
            ? new Dictionary<int, int>()
            : await _context.IepDrafts.AsNoTracking()
                .Where(d => draftResourceIds.Contains(d.Id))
                .Select(d => new { d.Id, d.SchoolStudentId })
                .ToDictionaryAsync(d => d.Id, d => d.SchoolStudentId, ct);

        var versionStudentByVersionId = versionResourceIds.Count == 0
            ? new Dictionary<int, int>()
            : await _context.IepVersions.AsNoTracking()
                .Where(v => versionResourceIds.Contains(v.Id))
                .Select(v => new { v.Id, v.SchoolStudentId })
                .ToDictionaryAsync(v => v.Id, v => v.SchoolStudentId, ct);

        // 3) All student ids we need names for: direct SchoolStudent rows + drafts' + versions' students.
        var studentIds = rows.Where(r => r.ResourceType == ResourceStudent).Select(r => r.ResourceId)
            .Concat(draftStudentByDraftId.Values)
            .Concat(versionStudentByVersionId.Values)
            .Distinct()
            .ToList();
        var studentNames = studentIds.Count == 0
            ? new Dictionary<int, string>()
            : await _context.SchoolStudents.AsNoTracking()
                .Where(s => studentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.FirstName, s.LastName })
                .ToDictionaryAsync(s => s.Id, s => FormatName(s.FirstName, s.LastName), ct);

        var entries = new List<AuditLogEntryModel>(rows.Count);
        foreach (var r in rows)
        {
            entries.Add(new AuditLogEntryModel
            {
                Id = r.Id,
                Action = r.Action.ToString(),
                ActorUserId = r.ActorUserId,
                ActorName = userNames.TryGetValue(r.ActorUserId, out var actorName) ? actorName : "Former staff member",
                ResourceType = r.ResourceType,
                ResourceId = r.ResourceId,
                ResourceDisplayName = ResolveResourceDisplay(r, draftStudentByDraftId, versionStudentByVersionId, studentNames),
                RecipientUserId = r.RecipientUserId,
                RecipientName = r.RecipientUserId == null
                    ? null
                    : userNames.TryGetValue(r.RecipientUserId.Value, out var recipientName) ? recipientName : "Unknown user",
                CreatedAt = r.CreatedAt
            });
        }
        return entries;
    }

    private static string ResolveResourceDisplay(
        RawRow r,
        IReadOnlyDictionary<int, int> draftStudentByDraftId,
        IReadOnlyDictionary<int, int> versionStudentByVersionId,
        IReadOnlyDictionary<int, string> studentNames)
    {
        switch (r.ResourceType)
        {
            case ResourceStudent:
                return studentNames.TryGetValue(r.ResourceId, out var directName) ? directName : "Deleted student";

            case ResourceDraft:
                if (draftStudentByDraftId.TryGetValue(r.ResourceId, out var draftStudentId)
                    && studentNames.TryGetValue(draftStudentId, out var draftStudentName))
                    return $"IEP draft for {draftStudentName}";
                return $"Draft #{r.ResourceId}";

            case ResourceVersion:
                if (versionStudentByVersionId.TryGetValue(r.ResourceId, out var versionStudentId)
                    && studentNames.TryGetValue(versionStudentId, out var versionStudentName))
                    return $"IEP version for {versionStudentName}";
                return $"Version #{r.ResourceId}";

            default:
                // Unknown/future resource type: never throw, surface a stable identifier.
                return $"{r.ResourceType} #{r.ResourceId}";
        }
    }

    private static string FormatName(string? firstName, string? lastName)
    {
        var name = $"{firstName} {lastName}".Trim();
        return string.IsNullOrEmpty(name) ? "Unknown user" : name;
    }

    /// <summary>Lightweight projection of the audit row before enrichment.</summary>
    private sealed class RawRow
    {
        public int Id { get; init; }
        public AuditAction Action { get; init; }
        public int ActorUserId { get; init; }
        public string ResourceType { get; init; } = string.Empty;
        public int ResourceId { get; init; }
        public int? RecipientUserId { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
