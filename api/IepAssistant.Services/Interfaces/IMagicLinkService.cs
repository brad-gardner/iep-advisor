using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

/// <summary>
/// Magic-link sign-in for staff invited as RelatedServiceProvider/GeneralEducator (pilot-gates plan,
/// phase 3, C11 adoption slice, decision 9).
/// </summary>
public interface IMagicLinkService
{
    /// <summary>
    /// Always "succeeds" from the caller's point of view (the controller returns 202 regardless) — no
    /// email enumeration. Internally a no-op unless <paramref name="email"/> belongs to an active user
    /// with an active StaffProfile whose OrgRole is RelatedServiceProvider/GeneralEducator in a district
    /// with <c>MagicLinkEnabled</c>, and that user has not exceeded 5 requests in the last 15 minutes.
    /// </summary>
    Task RequestAsync(string email, CancellationToken ct = default);

    /// <summary>Single-use token consume. Returns the same response shape as a password login on
    /// success; see <see cref="MagicLinkConsumeResult"/> for the MFA branches.</summary>
    Task<MagicLinkConsumeResult> ConsumeAsync(string token, CancellationToken ct = default);
}
