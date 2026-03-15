using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Share;

public class AcceptInviteRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
