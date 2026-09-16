namespace IepAssistant.Domain.Entities;

/// <summary>
/// A single-use, short-lived sign-in token for the magic-link flow (pilot-gates plan, phase 3, C11
/// adoption slice). Mirrors the SHA-token pattern used by <see cref="StaffInvite"/>/<see cref="ChildLink"/>:
/// a 32-byte raw token is emailed and only its SHA-256 hash is stored in <see cref="TokenHash"/>.
/// Single-use is enforced by <see cref="UsedAt"/> (set, not deleted, on consume — kept for
/// audit/troubleshooting); expires 15 minutes after issue.
/// </summary>
public class MagicLinkToken : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>SHA-256 hash (hex or base64, see <c>InviteTokenHelper.Hash</c>) of the emailed raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
