using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Stripe;

public class CreateCheckoutRequest
{
    [Required]
    public string SuccessUrl { get; set; } = string.Empty;

    [Required]
    public string CancelUrl { get; set; } = string.Empty;
}
