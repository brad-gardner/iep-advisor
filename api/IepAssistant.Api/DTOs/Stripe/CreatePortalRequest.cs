using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Stripe;

public class CreatePortalRequest
{
    [Required]
    public string ReturnUrl { get; set; } = string.Empty;
}
