using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Security;

/// <summary>
/// Mints the standard auth JWT for a <see cref="User"/>. Extracted so flows that create-and-sign-in a
/// user in one step (staff invite accept, register-district) can reuse the exact same claim set as
/// login — including the <c>SecurityStamp</c> claim the per-request middleware validates. Depends only
/// on <see cref="IConfiguration"/> so it stays trivially constructible in service unit tests.
/// </summary>
public sealed class JwtTokenFactory
{
    private readonly IConfiguration _configuration;

    public JwtTokenFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public AuthResult CreateAuthResult(User user)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _configuration["Jwt:Issuer"] ?? "IepAssistant.Api";
        var audience = _configuration["Jwt:Audience"] ?? "IepAssistant.Client";
        var expiryDays = int.Parse(_configuration["Jwt:ExpiryInDays"] ?? "7");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Identical claim set to AuthService.GenerateJwtToken (kept in sync intentionally).
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("FirstName", user.FirstName),
            new Claim("LastName", user.LastName),
            new Claim("SecurityStamp", user.SecurityStamp.ToString())
        };

        var expiresAt = DateTime.UtcNow.AddDays(expiryDays);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new AuthResult
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expiresAt,
            User = new UserModel
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                State = user.State,
                Role = user.Role.ToString(),
                IsActive = user.IsActive,
                OnboardingCompleted = user.OnboardingCompletedAt.HasValue,
                CreatedAt = user.CreatedAt
            }
        };
    }

    /// <summary>
    /// Mints a 5-minute "MFA pending" token — identical to <c>AuthService.GenerateMfaPendingToken</c>
    /// (kept in sync intentionally, same pattern as <see cref="CreateAuthResult"/> vs.
    /// <c>AuthService.GenerateJwtToken</c>). Used by the magic-link consume flow so an MFA-enrolled user
    /// completes sign-in through the exact same <c>/api/auth/mfa/verify</c> step a password login would.
    /// </summary>
    public string CreateMfaPendingToken(User user)
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
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
