using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class MfaVerifyRequest
{
    [Required]
    [MaxLength(500)]
    public string MfaPendingToken { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}
