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
    public string FullName => $"{FirstName} {LastName}";
}

public class RegisterModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
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
