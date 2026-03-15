using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Stripe;

public class GenerateBetaCodesRequest
{
    [Required]
    [Range(1, 100)]
    public int Count { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
