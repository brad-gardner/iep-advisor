namespace IepAssistant.Domain.Entities;

public class User : BaseEntity, IAuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; } = false;
    public string? MfaSecret { get; set; }
    public int MfaFailedAttempts { get; set; } = 0;
    public DateTime? MfaLockedUntil { get; set; }
    public long? LastTotpTimestamp { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }
    public int SecurityStamp { get; set; } = 0;
    public DateTime? DeletionRequestedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
