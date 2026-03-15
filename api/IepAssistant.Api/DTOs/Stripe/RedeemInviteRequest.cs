using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Stripe;

public class RedeemInviteRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;
}
