using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class PasswordResetService : IPasswordResetService
{
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        IUserRepository userRepository,
        ApplicationDbContext context,
        IEmailService emailService,
        ILogger<PasswordResetService> logger)
    {
        _userRepository = userRepository;
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task InitiateResetAsync(string email, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, ct);
        if (user == null)
        {
            // Silently return — no user enumeration
            return;
        }

        // Generate a 32-byte random token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(tokenBytes);

        // SHA256 hash for storage
        var tokenHash = HashToken(rawToken);

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(ct);

        // Send email with the raw token
        await _emailService.SendPasswordResetEmailAsync(email, rawToken, ct);
    }

    public async Task<ServiceResult> ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                t.UsedAt == null &&
                t.ExpiresAt > DateTime.UtcNow,
                ct);

        if (resetToken == null)
        {
            return ServiceResult.FailureResult("Invalid or expired reset token.");
        }

        var user = resetToken.User;

        // Update password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.SecurityStamp++;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        _userRepository.Update(user);

        // Mark this token as used
        resetToken.UsedAt = DateTime.UtcNow;

        // Invalidate all other tokens for this user
        var otherTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.Id != resetToken.Id && t.UsedAt == null)
            .ToListAsync(ct);

        foreach (var otherToken in otherTokens)
        {
            otherToken.UsedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Password reset completed for user {UserId}", user.Id);

        return ServiceResult.SuccessResult("Password has been reset successfully.");
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
