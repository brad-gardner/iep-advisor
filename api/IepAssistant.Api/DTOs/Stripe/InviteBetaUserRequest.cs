using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Stripe;

public class InviteBetaUserRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;
}
