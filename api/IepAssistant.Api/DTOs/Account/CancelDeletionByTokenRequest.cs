using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Account;

public class CancelDeletionByTokenRequest
{
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = string.Empty;
}
