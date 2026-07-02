namespace IepAssistant.Domain.Entities;

/// <summary>
/// P4 staff invitation. Mirrors the <c>StudentInvite</c>/<c>ChildLink</c> SHA-token pattern: a 32-byte
/// raw token is emailed, only its SHA-256 hash is stored in <see cref="InviteToken"/>, the invite is
/// email-bound (case-insensitive on <see cref="Email"/>), single-use (token nulled on accept), and
/// expires after 14 days. On accept a new <c>User</c>(Educator) + <c>StaffProfile</c> are created from
/// the invite's <see cref="DistrictId"/>/<see cref="SchoolId"/>/<see cref="OrgRoleId"/> and the invite
/// is atomically claimed (sets <see cref="AcceptedAt"/>/<see cref="AcceptedByUserId"/>, nulls the token).
///
/// Status is derived (no column): pending = <c>AcceptedAt == null &amp;&amp; IsActive &amp;&amp; not expired</c>;
/// expired = pending-window passed; revoked = <c>IsActive == false</c>; accepted = <c>AcceptedAt != null</c>.
/// </summary>
public class StaffInvite : BaseEntity, IAuditableEntity
{
    public string Email { get; set; } = string.Empty;

    public int DistrictId { get; set; }

    /// <summary>Nullable — <c>null</c> when inviting a DistrictAdmin (not bound to a single school).</summary>
    public int? SchoolId { get; set; }

    public int OrgRoleId { get; set; }

    /// <summary>SHA-256 hash of the emailed raw token; nulled once the invite is claimed.</summary>
    public string? InviteToken { get; set; }

    public DateTime InviteExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    /// <summary>The user created when the invite was accepted (set together with <see cref="AcceptedAt"/>).</summary>
    public int? AcceptedByUserId { get; set; }

    public int InvitedByUserId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// UTC timestamp of the one-time "invite expiring soon" reminder emailed to the inviting admin
    /// (P-pilot Phase 3). <c>null</c> = no reminder sent yet. Set by <c>StaffInviteExpiryWorker</c> once a
    /// pending invite enters the 3-day pre-expiry window, and nulled by <c>ResendAsync</c> so an extended
    /// invite re-arms exactly one fresh warning. Idempotency for the reminder is this timestamp alone.
    /// </summary>
    public DateTime? ExpiryReminderSentAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public District District { get; set; } = null!;
    public School? School { get; set; }
    public OrgRole OrgRole { get; set; } = null!;
}
