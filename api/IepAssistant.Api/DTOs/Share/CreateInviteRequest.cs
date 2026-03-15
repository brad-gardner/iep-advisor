using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Share;

public class CreateInviteRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}
