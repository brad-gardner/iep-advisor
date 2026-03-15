namespace IepAssistant.Api.DTOs.Share;

public class ChildAccessDto
{
    public int Id { get; set; }
    public int ChildProfileId { get; set; }
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserName { get; set; }
    public string? InviteEmail { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? AcceptedAt { get; set; }
    public bool IsPending { get; set; }
    public DateTime CreatedAt { get; set; }
}
