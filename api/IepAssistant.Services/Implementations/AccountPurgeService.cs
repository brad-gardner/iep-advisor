using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Interfaces;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <summary>
/// See <see cref="IAccountPurgeService"/> (pilot-gates plan, phase 2, decision 3).
///
/// <para><b>Parent path:</b> deletes every <see cref="ChildProfile"/> the user owns and everything
/// scoped to it (documents + their blobs, analyses, advocacy goals, meeting-prep, usage records, access
/// grants), plus family-authored content tied to the user directly (deletes <see cref="ParentDraftNote"/>
/// rows outright; reassigns <see cref="DraftResponse"/>/<see cref="DraftAcknowledgement"/> rows to a
/// system "Deleted User" sentinel account rather than deleting them, since those belong to a shared
/// draft the SCHOOL owns), then deletes the <see cref="User"/> row itself. Never touches
/// <see cref="SchoolStudent"/>, <see cref="Meeting"/>, <see cref="DocumentInstance"/>, or any other
/// school-owned record — only the rows this user's own parent-side upload flow created.</para>
///
/// <para><b>Staff (and any other non-parent) path:</b> deactivates <see cref="StaffProfile"/> (if any),
/// removes team memberships/school-student access grants, and anonymises the <see cref="User"/> row IN
/// PLACE — the row (and its Id) is kept, so every school record that references it by id (versions,
/// meetings, decisions, signatures) keeps working exactly as before, just against an anonymised name.
/// This also sidesteps the parent path's whole "can this Id be safely deleted" problem: nothing here
/// requires touching a foreign key at all.</para>
/// </summary>
public class AccountPurgeService : IAccountPurgeService
{
    private const string DeletedUserSentinelEmail = "deleted-user@system.invalid";

    private readonly ApplicationDbContext _context;
    private readonly IBlobStorageService _blobStorage;
    private readonly IAuditLogger _auditLogger;
    private readonly IStripeAccountCleanup _stripe;
    private readonly ILogger<AccountPurgeService> _logger;

    public AccountPurgeService(
        ApplicationDbContext context,
        IBlobStorageService blobStorage,
        IAuditLogger auditLogger,
        IStripeAccountCleanup stripe,
        ILogger<AccountPurgeService> logger)
    {
        _context = context;
        _blobStorage = blobStorage;
        _auditLogger = auditLogger;
        _stripe = stripe;
        _logger = logger;
    }

    public async Task PurgeAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null)
            return; // already purged by a previous cycle

        // Re-verify eligibility against a race with a cancellation (signed link or authenticated
        // endpoint) that landed between the worker's scan and this row being processed.
        var cutoff = DateTime.UtcNow.AddDays(-AccountService.DeletionGraceDays);
        if (user.DeletionRequestedAt == null || user.DeletionRequestedAt > cutoff)
            return;

        if (user.Role == UserRole.Parent)
            await PurgeParentAsync(user, ct);
        else
            await AnonymizeInPlaceAsync(user, ct);
    }

    private async Task PurgeParentAsync(User user, CancellationToken ct)
    {
        // Billing first, and blocking: the local row must never disappear while Stripe still holds the
        // card and keeps charging it. A Stripe failure throws, the worker logs it, and the purge is retried
        // on the next cycle with the user still intact.
        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId))
        {
            await _stripe.CancelSubscriptionAsync(user.StripeSubscriptionId, ct);
            user.StripeSubscriptionId = null;
            await _context.SaveChangesAsync(ct);
        }
        if (!string.IsNullOrWhiteSpace(user.StripeCustomerId))
        {
            await _stripe.DeleteCustomerAsync(user.StripeCustomerId, ct);
            user.StripeCustomerId = null;
            await _context.SaveChangesAsync(ct);
        }

        var children = await _context.ChildProfiles.Where(c => c.UserId == user.Id).ToListAsync(ct);
        var childIds = children.Select(c => c.Id).ToList();

        if (childIds.Count > 0)
        {
            await DeleteChildBlobsAsync(childIds, ct);

            // Break ChildProfile -> IepDocument (self-referencing "current document") before deleting documents.
            foreach (var child in children)
                child.CurrentIepDocumentId = null;
            await _context.SaveChangesAsync(ct);

            var iepDocIds = await _context.IepDocuments.Where(d => childIds.Contains(d.ChildProfileId)).Select(d => d.Id).ToListAsync(ct);
            var iepSectionIds = await _context.IepSections.Where(s => iepDocIds.Contains(s.IepDocumentId)).Select(s => s.Id).ToListAsync(ct);
            _context.Goals.RemoveRange(_context.Goals.Where(g => iepSectionIds.Contains(g.IepSectionId)));
            _context.IepSections.RemoveRange(_context.IepSections.Where(s => iepDocIds.Contains(s.IepDocumentId)));
            _context.IepAnalyses.RemoveRange(_context.IepAnalyses.Where(a => iepDocIds.Contains(a.IepDocumentId)));
            _context.IepDocuments.RemoveRange(_context.IepDocuments.Where(d => childIds.Contains(d.ChildProfileId)));

            var etrDocIds = await _context.EtrDocuments.Where(d => childIds.Contains(d.ChildProfileId)).Select(d => d.Id).ToListAsync(ct);
            _context.EtrSections.RemoveRange(_context.EtrSections.Where(s => etrDocIds.Contains(s.EtrDocumentId)));
            _context.EtrAnalyses.RemoveRange(_context.EtrAnalyses.Where(a => etrDocIds.Contains(a.EtrDocumentId)));
            _context.EtrDocuments.RemoveRange(_context.EtrDocuments.Where(d => childIds.Contains(d.ChildProfileId)));

            var progressReportIds = await _context.ProgressReports.Where(r => childIds.Contains(r.ChildProfileId)).Select(r => r.Id).ToListAsync(ct);
            _context.ProgressReportAnalyses.RemoveRange(_context.ProgressReportAnalyses.Where(a => progressReportIds.Contains(a.ProgressReportId)));
            _context.ProgressReports.RemoveRange(_context.ProgressReports.Where(r => childIds.Contains(r.ChildProfileId)));

            var runIds = await _context.AnalysisRuns.Where(r => childIds.Contains(r.ChildProfileId)).Select(r => r.Id).ToListAsync(ct);
            _context.AnalysisRunSections.RemoveRange(_context.AnalysisRunSections.Where(s => runIds.Contains(s.AnalysisRunId)));
            _context.AnalysisRunSources.RemoveRange(_context.AnalysisRunSources.Where(s => runIds.Contains(s.AnalysisRunId)));
            _context.AnalysisRuns.RemoveRange(_context.AnalysisRuns.Where(r => childIds.Contains(r.ChildProfileId)));

            _context.ParentAdvocacyGoals.RemoveRange(_context.ParentAdvocacyGoals.Where(g => childIds.Contains(g.ChildProfileId)));
            _context.ParentContributions.RemoveRange(_context.ParentContributions.Where(c => childIds.Contains(c.ChildProfileId)));
            _context.JournalEntries.RemoveRange(_context.JournalEntries.Where(j => childIds.Contains(j.ChildProfileId)));
            // Advocate threads on owned children (any parent's): messages first so the delete never leans on
            // the DB cascade, then the threads.
            _context.AdvocateMessages.RemoveRange(_context.AdvocateMessages.Where(m => childIds.Contains(m.Thread.ChildProfileId)));
            _context.AdvocateThreads.RemoveRange(_context.AdvocateThreads.Where(t => childIds.Contains(t.ChildProfileId)));
            _context.MeetingPrepChecklists.RemoveRange(_context.MeetingPrepChecklists.Where(c => childIds.Contains(c.ChildProfileId)));
            _context.ChildAccesses.RemoveRange(_context.ChildAccesses.Where(a => childIds.Contains(a.ChildProfileId)));
            _context.ChildLinks.RemoveRange(_context.ChildLinks.Where(l => l.ChildProfileId != null && childIds.Contains(l.ChildProfileId.Value)));

            // UsageRecord.ChildProfileId is Restrict, not Cascade — every row referencing an owned
            // child MUST be gone before the ChildProfile delete below, or that delete fails its FK
            // check. (UsageRecord.User is Restrict too but is fully covered by the UserId-scoped
            // delete further down, since every such row's ChildProfileId — when set at all — points
            // at a child this same user owns.)
            _context.UsageRecords.RemoveRange(_context.UsageRecords.Where(u => childIds.Contains(u.ChildProfileId ?? -1)));
            await _context.SaveChangesAsync(ct);

            _context.ChildProfiles.RemoveRange(children);
            await _context.SaveChangesAsync(ct);
        }

        // Usage records billed to this user directly (the owner side of TryReserveUsageAsync) — a
        // superset of the child-scoped ones above, covering any not tied to a still-existing child.
        _context.UsageRecords.RemoveRange(_context.UsageRecords.Where(u => u.UserId == user.Id));

        // Advocate threads this parent started on a child they do NOT own (co-parent) — AdvocateThread.ParentUser
        // is Restrict, so these must go before the User row.
        _context.AdvocateMessages.RemoveRange(_context.AdvocateMessages.Where(m => m.Thread.ParentUserId == user.Id));
        _context.AdvocateThreads.RemoveRange(_context.AdvocateThreads.Where(t => t.ParentUserId == user.Id));

        // Access this parent holds on a child they do NOT own (co-parent / invited viewer).
        _context.ChildAccesses.RemoveRange(_context.ChildAccesses.Where(a => a.UserId == user.Id));

        // A beta code this user redeemed must not keep pointing at them (Restrict FK) — the redemption
        // record (and its RedeemedAt) is kept; only the dangling reference is cleared.
        var redeemedCodes = await _context.BetaInviteCodes.Where(b => b.RedeemedByUserId == user.Id).ToListAsync(ct);
        foreach (var code in redeemedCodes)
            code.RedeemedByUserId = null;

        await _context.SaveChangesAsync(ct);

        // Family-authored content on a SCHOOL-owned SharedDraftRevision: never deleted (the notes
        // exception below is the one the contract calls out by name), just detached from this identity.
        var sentinel = await GetOrCreateDeletedUserSentinelAsync(ct);

        var responses = await _context.DraftResponses.Where(r => r.ParentUserId == user.Id).ToListAsync(ct);
        foreach (var response in responses)
            response.ParentUserId = sentinel.Id;

        _context.ParentDraftNotes.RemoveRange(_context.ParentDraftNotes.Where(n => n.ParentUserId == user.Id));

        var acknowledgements = await _context.DraftAcknowledgements.Where(a => a.UserId == user.Id).ToListAsync(ct);
        foreach (var ack in acknowledgements)
            ack.UserId = sentinel.Id;

        await _context.SaveChangesAsync(ct);

        // Meeting-scheduling artifacts (school-owned Meeting rows are never touched) referencing this
        // user directly.
        var participants = await _context.MeetingParticipants.Where(p => p.UserId == user.Id).ToListAsync(ct);
        foreach (var participant in participants)
        {
            participant.UserId = null;
            participant.ExternalName ??= "Deleted user";
        }
        _context.MeetingReminders.RemoveRange(_context.MeetingReminders.Where(r => r.UserId == user.Id));
        _context.SchoolStudentAccesses.RemoveRange(_context.SchoolStudentAccesses.Where(a => a.UserId == user.Id));
        await _context.SaveChangesAsync(ct);

        // Notification rows cascade-delete with the User row below; nothing to do here.

        _auditLogger.Record(AuditAction.Delete, actorUserId: user.Id, resourceType: "User", resourceId: user.Id);

        await RemoveCredentialArtifactsAsync(user.Id, ct);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(ct);

        _logger.LogWarning("Purged parent account {UserId} ({ChildCount} child profile(s) deleted)", user.Id, childIds.Count);
    }

    /// <summary>Staff (and, defensively, any other non-parent role): deactivate/anonymise in place,
    /// never delete the row — see class remarks.</summary>
    private async Task AnonymizeInPlaceAsync(User user, CancellationToken ct)
    {
        var staffProfile = await _context.StaffProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (staffProfile != null)
        {
            staffProfile.IsActive = false;
            _context.StudentTeamMembers.RemoveRange(_context.StudentTeamMembers.Where(m => m.UserId == user.Id));
            _context.SchoolStudentAccesses.RemoveRange(_context.SchoolStudentAccesses.Where(a => a.UserId == user.Id));
        }

        user.FirstName = "Former";
        user.LastName = "Staff";
        user.Email = $"deleted+{user.Id}@invalid.local";
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;
        user.LastTotpTimestamp = null;
        user.IsActive = false;
        user.SecurityStamp++;
        await RemoveCredentialArtifactsAsync(user.Id, ct);

        // No row is deleted, so nothing would ever stop the hourly scan from re-selecting this user —
        // clearing DeletionRequestedAt is what marks the purge complete (IsActive=false + the
        // deleted+{id}@invalid.local email together mean "already purged" without a dedicated column).
        user.DeletionRequestedAt = null;

        _auditLogger.Record(AuditAction.Delete, actorUserId: user.Id, resourceType: "User", resourceId: user.Id);

        await _context.SaveChangesAsync(ct);

        _logger.LogWarning("Anonymised staff/non-parent account {UserId}", user.Id);
    }

    private async Task DeleteChildBlobsAsync(List<int> childIds, CancellationToken ct)
    {
        var blobUris = new List<string>();
        blobUris.AddRange(await _context.IepDocuments.Where(d => childIds.Contains(d.ChildProfileId) && d.BlobUri != null).Select(d => d.BlobUri!).ToListAsync(ct));
        blobUris.AddRange(await _context.EtrDocuments.Where(d => childIds.Contains(d.ChildProfileId) && d.BlobUri != null).Select(d => d.BlobUri!).ToListAsync(ct));
        blobUris.AddRange(await _context.ProgressReports.Where(r => childIds.Contains(r.ChildProfileId) && r.BlobUri != null).Select(r => r.BlobUri!).ToListAsync(ct));

        foreach (var blobUri in blobUris)
        {
            try
            {
                await _blobStorage.DeleteAsync(blobUri, ct);
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Already gone (e.g. a retried purge after a mid-run crash) — that is the outcome we want.
            }
            catch (Exception ex)
            {
                // Anything else must NOT be swallowed: the rows that reference this blob are deleted
                // right after this loop, and without them nothing could ever find the blob again. Throwing
                // leaves the user intact so the hourly worker retries the whole purge.
                _logger.LogError(ex, "Failed to delete blob {BlobUri} while purging a parent account; purge will be retried", blobUri);
                throw;
            }
        }
    }

    /// <summary>MFA recovery codes, password-reset tokens and magic-link tokens are credentials too —
    /// an anonymised or purged account keeps none of them.</summary>
    private async Task RemoveCredentialArtifactsAsync(int userId, CancellationToken ct)
    {
        _context.UserRecoveryCodes.RemoveRange(await _context.UserRecoveryCodes.Where(c => c.UserId == userId).ToListAsync(ct));
        _context.PasswordResetTokens.RemoveRange(await _context.PasswordResetTokens.Where(t => t.UserId == userId).ToListAsync(ct));
        _context.MagicLinkTokens.RemoveRange(await _context.MagicLinkTokens.Where(t => t.UserId == userId).ToListAsync(ct));
    }

    private async Task<User> GetOrCreateDeletedUserSentinelAsync(CancellationToken ct)
    {
        var sentinel = await _context.Users.FirstOrDefaultAsync(u => u.Email == DeletedUserSentinelEmail, ct);
        if (sentinel != null)
            return sentinel;

        sentinel = new User
        {
            Email = DeletedUserSentinelEmail,
            FirstName = "Deleted",
            LastName = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N")),
            Role = UserRole.Parent,
            IsActive = false
        };
        _context.Users.Add(sentinel);
        await _context.SaveChangesAsync(ct);
        return sentinel;
    }
}
