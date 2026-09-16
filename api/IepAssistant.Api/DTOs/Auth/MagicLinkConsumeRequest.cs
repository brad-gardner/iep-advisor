using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MagicLinkConsumeRequest
{
    [Required(ErrorMessage = "Token is required")]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;
}
