using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Share;

public class CreateInviteRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^(viewer|collaborator)$", ErrorMessage = "Role must be 'viewer' or 'collaborator'")]
    public string Role { get; set; } = string.Empty;
}
