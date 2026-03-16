using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MfaRecoveryRequest
{
    [Required]
    [MaxLength(500)]
    public string MfaPendingToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string RecoveryCode { get; set; } = string.Empty;
}
