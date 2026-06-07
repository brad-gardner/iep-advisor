namespace IepAssistant.Services.Models;

public class AuthResult
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserModel User { get; set; } = null!;
}

public class UserModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? State { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool OnboardingCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FullName => $"{FirstName} {LastName}";
}

public class RegisterModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
}

public class RegisterDistrictModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DistrictName { get; set; } = string.Empty;
    public string? StateCode { get; set; }
}

/// <summary>
/// Outcome of <c>RegisterDistrictAsync</c>. On success carries an <see cref="AuthResult"/> (JWT + user)
/// so the frontend can auto-login exactly as it does after a normal login; on failure carries a message.
/// </summary>
public class RegisterDistrictResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public AuthResult? AuthResult { get; init; }

    public static RegisterDistrictResult Failure(string message) => new() { Success = false, Message = message };
    public static RegisterDistrictResult Ok(AuthResult auth) => new() { Success = true, AuthResult = auth };
}

public class UpdateUserModel
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? State { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateProfileModel
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? State { get; set; }
}

public class MfaSetupResult
{
    public string OtpauthUri { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
}

public class LoginResult
{
    public bool RequiresMfa { get; set; }
    public string? MfaPendingToken { get; set; }
    public AuthResult? AuthResult { get; set; }
}
