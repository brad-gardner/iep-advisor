using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MfaVerifyRequest
{
    [Required]
    public string MfaPendingToken { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
