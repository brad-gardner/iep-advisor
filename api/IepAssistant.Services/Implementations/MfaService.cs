using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class MfaService : IMfaService
{
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _context;
    private readonly ITotpService _totpService;
    private readonly MfaSecretProtector _protector;
    private readonly byte[] _hmacKey;

    public MfaService(
        IUserRepository userRepository,
        ApplicationDbContext context,
        ITotpService totpService,
        MfaSecretProtector protector,
        IConfiguration configuration)
    {
        _userRepository = userRepository;
        _context = context;
        _totpService = totpService;
        _protector = protector;

        var keyString = configuration["Security:EncryptionKey"]
            ?? throw new InvalidOperationException("Security:EncryptionKey not configured");
        _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(keyString + ".RecoveryCodes"));
    }

    public async Task<MfaSetupResult> SetupAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found");

        var secret = _totpService.GenerateSecret();
        user.MfaSecret = _protector.Protect(secret);
        user.MfaEnabled = false;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(ct);

        var otpauthUri = $"otpauth://totp/IEP%20Advisor:{Uri.EscapeDataString(user.Email)}?secret={secret}&issuer=IEP%20Advisor&algorithm=SHA1&digits=6&period=30";

        return new MfaSetupResult
        {
            OtpauthUri = otpauthUri,
            ManualEntryKey = secret
        };
    }

    public async Task<ServiceResult<List<string>>> VerifySetupAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found");

        if (string.IsNullOrEmpty(user.MfaSecret))
            return ServiceResult<List<string>>.FailureResult("MFA setup has not been initiated.");

        if (user.MfaEnabled)
            return ServiceResult<List<string>>.FailureResult("MFA is already enabled.");

        var secret = _protector.Unprotect(user.MfaSecret);
        if (!_totpService.ValidateCode(secret, code))
            return ServiceResult<List<string>>.FailureResult("Invalid verification code.");

        user.MfaEnabled = true;
        user.LastTotpTimestamp = _totpService.GetTimestamp();
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;
        user.SecurityStamp++;

        // Delete existing recovery codes for this user
        var existingCodes = await _context.UserRecoveryCodes
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
        _context.UserRecoveryCodes.RemoveRange(existingCodes);

        // Generate 10 recovery codes
        var plaintextCodes = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var codeBytes = RandomNumberGenerator.GetBytes(10);
            var recoveryCode = Convert.ToHexString(codeBytes).ToLowerInvariant();
            // Format as xxxx-xxxx-xxxx-xxxx-xxxx
            recoveryCode = string.Join("-",
                Enumerable.Range(0, 5).Select(j => recoveryCode.Substring(j * 4, 4)));
            plaintextCodes.Add(recoveryCode);

            var hash = HmacHash(recoveryCode);
            _context.UserRecoveryCodes.Add(new UserRecoveryCode
            {
                UserId = userId,
                CodeHash = hash,
                CreatedAt = DateTime.UtcNow
            });
        }

        _userRepository.Update(user);
        await _context.SaveChangesAsync(ct);

        return ServiceResult<List<string>>.SuccessResult(plaintextCodes, "MFA enabled successfully.");
    }

    public async Task<bool> ValidateCodeAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.MfaEnabled || string.IsNullOrEmpty(user.MfaSecret))
            return false;

        // Check MFA lockout
        if (user.MfaLockedUntil.HasValue && user.MfaLockedUntil.Value > DateTime.UtcNow)
            return false;

        var secret = _protector.Unprotect(user.MfaSecret);
        if (!_totpService.ValidateCode(secret, code))
        {
            user.MfaFailedAttempts++;
            if (user.MfaFailedAttempts >= 5)
            {
                user.MfaLockedUntil = DateTime.UtcNow.AddMinutes(15);
                user.MfaFailedAttempts = 0;
            }
            _userRepository.Update(user);
            await _context.SaveChangesAsync(ct);
            return false;
        }

        // Replay prevention
        var currentTimestamp = _totpService.GetTimestamp();
        if (user.LastTotpTimestamp.HasValue && currentTimestamp <= user.LastTotpTimestamp.Value)
        {
            return false;
        }

        user.LastTotpTimestamp = currentTimestamp;
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;
        _userRepository.Update(user);
        await _context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> ValidateRecoveryCodeAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !user.MfaEnabled)
            return false;

        var normalizedCode = code.Trim().ToLowerInvariant();
        var codeHash = HmacHash(normalizedCode);

        var unusedCodes = await _context.UserRecoveryCodes
            .Where(r => r.UserId == userId && r.UsedAt == null)
            .ToListAsync(ct);

        foreach (var recoveryCode in unusedCodes)
        {
            if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(recoveryCode.CodeHash),
                Encoding.UTF8.GetBytes(codeHash)))
            {
                recoveryCode.UsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
                return true;
            }
        }

        return false;
    }

    public async Task<ServiceResult> DisableAsync(int userId, string password, string totpCode, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
            return ServiceResult.FailureResult("User not found.");

        if (!user.MfaEnabled)
            return ServiceResult.FailureResult("MFA is not enabled.");

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return ServiceResult.FailureResult("Invalid password.");

        // Verify TOTP code
        if (string.IsNullOrEmpty(user.MfaSecret))
            return ServiceResult.FailureResult("MFA secret not found.");

        var secret = _protector.Unprotect(user.MfaSecret);
        if (!_totpService.ValidateCode(secret, totpCode))
            return ServiceResult.FailureResult("Invalid TOTP code.");

        // Disable MFA
        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.MfaFailedAttempts = 0;
        user.MfaLockedUntil = null;
        user.LastTotpTimestamp = null;
        user.SecurityStamp++;

        // Delete recovery codes
        var codes = await _context.UserRecoveryCodes
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
        _context.UserRecoveryCodes.RemoveRange(codes);

        _userRepository.Update(user);
        await _context.SaveChangesAsync(ct);

        return ServiceResult.SuccessResult("MFA disabled successfully.");
    }

    private string HmacHash(string input)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}
