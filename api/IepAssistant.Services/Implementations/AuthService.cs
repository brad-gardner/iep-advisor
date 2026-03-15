using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;

    public AuthService(
        IConfiguration configuration,
        IUserRepository userRepository,
        ApplicationDbContext context)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _context = context;
    }

    public async Task<LoginResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        // Check password lockout
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Increment failed attempts
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 10)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
                user.FailedLoginAttempts = 0;
            }
            _userRepository.Update(user);
            await _context.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Reset failed attempts on successful password
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        // If MFA enabled, return pending token
        if (user.MfaEnabled)
        {
            var pendingToken = GenerateMfaPendingToken(user);
            return new LoginResult
            {
                RequiresMfa = true,
                MfaPendingToken = pendingToken
            };
        }

        // No MFA — return full auth result
        var token = GenerateJwtToken(user);
        var expiryDays = int.Parse(_configuration["Jwt:ExpiryInDays"] ?? "7");

        return new LoginResult
        {
            RequiresMfa = false,
            AuthResult = new AuthResult
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
                User = MapToUserModel(user)
            }
        };
    }

    public async Task<ServiceResult> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default)
    {
        // Validate invite code (closed beta — registration requires a valid code)
        var inviteCode = await _context.Set<BetaInviteCode>()
            .FirstOrDefaultAsync(c => c.Code == model.InviteCode && c.IsActive
                && c.RedeemedByUserId == null
                && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow), cancellationToken);

        if (inviteCode == null)
            return ServiceResult.FailureResult("Invalid or expired invite code.");

        var existingUser = await _userRepository.GetByEmailAsync(model.Email, cancellationToken);
        if (existingUser != null)
            return ServiceResult.FailureResult("Email is already registered.");

        var user = new User
        {
            Email = model.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
            FirstName = model.FirstName,
            LastName = model.LastName,
            Role = "User",
            IsActive = true,
            SubscriptionStatus = "active",
            SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Redeem the invite code
        inviteCode.RedeemedByUserId = user.Id;
        inviteCode.RedeemedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("User registered successfully.");
    }

    public async Task<UserModel?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user == null ? null : MapToUserModel(user);
    }

    public async Task<ServiceResult> UpdateProfileAsync(int userId, UpdateProfileModel model, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            return ServiceResult.FailureResult("User not found.");

        if (model.FirstName != null)
            user.FirstName = model.FirstName;

        if (model.LastName != null)
            user.LastName = model.LastName;

        if (model.State != null)
            user.State = model.State;

        user.UpdatedAt = DateTime.UtcNow;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Profile updated successfully.");
    }

    public Task<AuthResult?> RefreshTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Refresh token functionality not yet implemented.");
    }

    public async Task<ServiceResult> CompleteOnboardingAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            return ServiceResult.FailureResult("User not found.");

        user.OnboardingCompletedAt = DateTime.UtcNow;
        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        return ServiceResult.SuccessResult("Onboarding completed.");
    }

    public int? ValidateMfaPendingToken(string token)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "IepAssistant.Api";
        var audience = _configuration["Jwt:Audience"] ?? "IepAssistant.Client";

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ClockSkew = TimeSpan.Zero
            }, out _);

            var tokenType = principal.FindFirst("token_type")?.Value;
            if (tokenType != "mfa_pending")
                return null;

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim != null && int.TryParse(userIdClaim, out var userId))
                return userId;

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AuthResult?> CompleteMfaLoginAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        var token = GenerateJwtToken(user);
        var expiryDays = int.Parse(_configuration["Jwt:ExpiryInDays"] ?? "7");

        return new AuthResult
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            User = MapToUserModel(user)
        };
    }

    private string GenerateJwtToken(User user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "IepAssistant.Api";
        var audience = _configuration["Jwt:Audience"] ?? "IepAssistant.Client";
        var expiryDays = int.Parse(_configuration["Jwt:ExpiryInDays"] ?? "7");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            new Claim("SecurityStamp", user.SecurityStamp.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateMfaPendingToken(User user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "IepAssistant.Api";
        var audience = _configuration["Jwt:Audience"] ?? "IepAssistant.Client";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("token_type", "mfa_pending")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static UserModel MapToUserModel(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        State = user.State,
        Role = user.Role,
        IsActive = user.IsActive,
        OnboardingCompleted = user.OnboardingCompletedAt.HasValue,
        CreatedAt = user.CreatedAt
    };
}
