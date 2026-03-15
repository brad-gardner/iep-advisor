namespace IepAssistant.Domain.Entities;

public class BetaInviteCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public int? RedeemedByUserId { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? RedeemedBy { get; set; }
}
