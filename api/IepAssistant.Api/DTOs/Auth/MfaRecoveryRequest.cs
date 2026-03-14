using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MfaRecoveryRequest
{
    [Required]
    public string MfaPendingToken { get; set; } = string.Empty;

    [Required]
    public string RecoveryCode { get; set; } = string.Empty;
}
