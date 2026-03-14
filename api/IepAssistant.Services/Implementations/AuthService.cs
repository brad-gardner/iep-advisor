using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

    public async Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
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

    public async Task<ServiceResult> RegisterAsync(RegisterModel model, CancellationToken cancellationToken = default)
    {
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user, cancellationToken);
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
        // TODO: Implement refresh token logic
        throw new NotImplementedException("Refresh token functionality not yet implemented.");
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
            new Claim("LastName", user.LastName)
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

    private static UserModel MapToUserModel(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        State = user.State,
        Role = user.Role
    };
}
