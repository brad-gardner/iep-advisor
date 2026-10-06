using System.ComponentModel.DataAnnotations;

namespace IepAssistant.Api.DTOs.Auth;

public class UpdateProfileRequest
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(2)]
    public string? State { get; set; }

    /// <summary>Null/omitted leaves the stored preference unchanged. "en"/"es" (case-insensitive) sets
    /// it; any other value is a 400 (see <see cref="IepAssistant.Services.Implementations.AuthService.UpdateProfileAsync"/>).
    /// Deliberately unconstrained by a data annotation here — the validation needs to produce a
    /// localized message, which happens in the service layer, not a generic ModelState error.</summary>
    public string? PreferredLanguage { get; set; }
}
