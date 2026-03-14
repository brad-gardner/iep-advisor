namespace IepAssistant.Domain.Entities;

public class UserRecoveryCode : BaseEntity
{
    public int UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
