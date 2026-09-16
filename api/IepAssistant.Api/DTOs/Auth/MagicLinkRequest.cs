using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MagicLinkRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}
