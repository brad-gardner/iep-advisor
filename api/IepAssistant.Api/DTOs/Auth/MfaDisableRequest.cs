using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MfaDisableRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
