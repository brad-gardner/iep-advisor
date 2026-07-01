namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Phase 3 pre-expiry reminder logic for pending staff invites, extracted from the hosted worker so the
/// scan + per-invite decision can be unit-tested with a deterministic <c>utcNow</c> and no timer. Warns the
/// INVITING ADMIN only, exactly once, ~3 days (72h) before a still-pending invite expires. All correctness
/// rules (candidate window, race guard, active-inviter/active-school skips, best-effort stamping) live here;
/// <c>StaffInviteExpiryWorker</c> only owns the timer and per-invite DI scoping.
/// </summary>
public interface IStaffInviteExpiryService
{
    /// <summary>
    /// Returns the ids of invites that currently sit in the pre-expiry reminder window and have not yet been
    /// reminded: <c>utcNow &lt; InviteExpiresAt &lt;= utcNow + 72h</c>, pending (<c>AcceptedAt == null &amp;&amp;
    /// IsActive</c>), and <c>ExpiryReminderSentAt == null</c>. Already-expired invites are excluded, so a
    /// first-deploy backlog of expired invites is never emailed. Active-inviter / active-school filtering is
    /// deliberately deferred to <see cref="ProcessInviteAsync"/> so those skips can be logged per invite.
    /// </summary>
    Task<IReadOnlyList<int>> FindExpiringInviteIdsAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>
    /// Re-verifies a single candidate against the window + pending + un-reminded conditions (race guard vs a
    /// concurrent resend), skips+logs when the inviting admin's StaffProfile is inactive or the invite's
    /// school is deactivated, sends the reminder to the inviter's own email, then stamps
    /// <c>ExpiryReminderSentAt</c> via a guarded UPDATE that can't clobber a concurrent resend's re-arm.
    /// Safe to call in its own DI scope with a fresh DbContext.
    /// </summary>
    Task ProcessInviteAsync(int inviteId, DateTime utcNow, CancellationToken ct = default);

    /// <summary>
    /// Convenience batch entrypoint (used by tests and callable directly): scans for candidates then processes
    /// each with per-invite try/catch isolation so one failure never aborts the batch. The hosted worker uses
    /// the finer-grained <see cref="FindExpiringInviteIdsAsync"/> + <see cref="ProcessInviteAsync"/> pair so
    /// each invite runs in its own scope, but both paths share the same per-invite decision logic.
    /// </summary>
    Task ProcessExpiringInvitesAsync(DateTime utcNow, CancellationToken ct = default);
}
