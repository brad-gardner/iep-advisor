using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Educator;

public class InviteParentRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string ParentEmail { get; set; } = string.Empty;
}

public class ChildLinkDto
{
    public int Id { get; set; }
    public int SchoolStudentId { get; set; }
    public int? ChildProfileId { get; set; }
    public string? InviteEmail { get; set; }
    public bool IsActive { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? LinkedAt { get; set; }
    public DateTime? InviteExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
