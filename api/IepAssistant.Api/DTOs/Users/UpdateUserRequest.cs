using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Users;

public class UpdateUserRequest
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(2)]
    public string? State { get; set; }

    [MaxLength(50)]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }
}
