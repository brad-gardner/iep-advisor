namespace IepAssistant.Services.Models;

/// <summary>
/// Outcome of <c>IMagicLinkService.ConsumeAsync</c>. Mirrors <see cref="LoginResult"/>'s shape so a
/// consumed magic link produces the same wire response as a password login: <see cref="RequiresMfa"/> +
/// <see cref="MfaPendingToken"/> when the user has MFA enrolled (identical next step —
/// <c>POST /api/auth/mfa/verify</c>), a full <see cref="AuthResult"/> on an outright success, or a
/// distinct <see cref="MfaSetupRequired"/> flag when the district requires MFA for magic-link sign-in
/// and the user has not yet enrolled — no login flow in this codebase forces MFA enrollment today (see
/// <c>JwtTokenFactory.CreateMfaPendingToken</c> doc), so this is a new, clearly-labeled state rather than
/// a reuse of an existing one.
/// </summary>
public class MagicLinkConsumeResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public bool RequiresMfa { get; init; }
    public string? MfaPendingToken { get; init; }
    public bool MfaSetupRequired { get; init; }
    public AuthResult? AuthResult { get; init; }

    public static MagicLinkConsumeResult Failure(string message) => new() { Success = false, Message = message };
    public static MagicLinkConsumeResult MfaRequired(string mfaPendingToken) => new() { Success = true, RequiresMfa = true, MfaPendingToken = mfaPendingToken };
    public static MagicLinkConsumeResult MfaSetupRequiredResult() => new() { Success = true, MfaSetupRequired = true };
    public static MagicLinkConsumeResult Ok(AuthResult authResult) => new() { Success = true, AuthResult = authResult };
}
