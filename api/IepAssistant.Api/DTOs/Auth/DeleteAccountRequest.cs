using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class DeleteAccountRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;

    public string? MfaCode { get; set; }
}
