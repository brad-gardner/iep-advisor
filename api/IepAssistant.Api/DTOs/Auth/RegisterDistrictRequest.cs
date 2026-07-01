using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

/// <summary>
/// Open (no invite code) self-serve district signup. Creates the first DistrictAdmin and a brand-new
/// District in one transaction. The beta gate is parents-only, an explicit product decision — district
/// signup is intentionally open.
/// </summary>
public class RegisterDistrictRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "District name is required")]
    [MaxLength(200)]
    public string DistrictName { get; set; } = string.Empty;

    /// <summary>Optional two-letter state code (e.g. "OH"). When supplied it must be exactly 2 characters.</summary>
    [StringLength(2, MinimumLength = 2, ErrorMessage = "State code must be exactly 2 characters")]
    public string? StateCode { get; set; }
}
