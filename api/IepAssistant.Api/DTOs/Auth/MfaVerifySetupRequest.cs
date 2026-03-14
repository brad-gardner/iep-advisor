using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MfaVerifySetupRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
}
