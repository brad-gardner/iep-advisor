namespace IepAssistant.Domain.Entities;

public class User : BaseEntity, IAuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? State { get; set; }
    public UserRole Role { get; set; } = UserRole.Parent;
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; } = false;
    public string? MfaSecret { get; set; }
    public int MfaFailedAttempts { get; set; } = 0;
    public DateTime? MfaLockedUntil { get; set; }
    public long? LastTotpTimestamp { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public int SecurityStamp { get; set; } = 0;
    public DateTime? OnboardingCompletedAt { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string SubscriptionStatus { get; set; } = "none"; // none, active, past_due, canceled, expired
    public DateTime? SubscriptionExpiresAt { get; set; }
    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>32 hex chars, unique when set (plan 4, decision 4). Revocable token backing the
    /// per-user ICS calendar subscription feed; null until the user first requests a feed URL.</summary>
    public string? CalendarFeedToken { get; set; }

    /// <summary>When <see cref="CalendarFeedToken"/> was last issued/regenerated — drives
    /// <c>CalendarFeedDto.createdAt</c>. Not part of the plan-4 contract's field list; added so the feed
    /// endpoints can report an accurate creation time without reusing the general-purpose
    /// <see cref="UpdatedAt"/> (which any unrelated profile edit would also bump).</summary>
    public DateTime? CalendarFeedTokenCreatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
