using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Domain.Repositories;
using IepAssistant.Services.Implementations;
using IepAssistant.Services.Localization;
using Xunit;

namespace IepAssistant.Services.Tests;

/// <summary>
/// Multilingual plan (2026-10-06) phase 1, P3 fix: <see cref="PasswordResetService.InitiateResetAsync"/>
/// passes the account's saved <c>PreferredLanguage</c> straight through to
/// <see cref="IEmailService.SendPasswordResetEmailAsync"/> when present, and — this flow runs before
/// anyone is signed in, so there is no account-side signal otherwise — falls back to the CURRENT
/// request's resolved UI culture (the requester's own browser Accept-Language) when it is null, rather
/// than hard-coding English.
/// </summary>
public sealed class PasswordResetServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly CapturingEmailService _email = new();

    public PasswordResetServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
    }

    private async Task<string> SeedUserAsync(string? preferredLanguage)
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        _context.Users.Add(new User
        {
            Email = email,
            PasswordHash = "x",
            FirstName = "Pat",
            LastName = "Parent",
            Role = UserRole.Parent,
            IsActive = true,
            PreferredLanguage = preferredLanguage
        });
        await _context.SaveChangesAsync();
        return email;
    }

    private PasswordResetService CreateService()
        => new(new UserRepository(_context), _context, _email, NullLogger<PasswordResetService>.Instance, TestSupport.TestLocalizers.Messages());

    [Fact]
    public async Task InitiateResetAsync_UserHasPreferredLanguage_PassesItThrough_RegardlessOfAmbientUiCulture()
    {
        var email = await SeedUserAsync("es");
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en"); // ambient request culture disagrees

            await CreateService().InitiateResetAsync(email);

            Assert.Equal("es", _email.LastLanguage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Fact]
    public async Task InitiateResetAsync_UserHasNoPreferredLanguage_FallsBackToCurrentRequestUiCulture()
    {
        var email = await SeedUserAsync(null);
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es");

            await CreateService().InitiateResetAsync(email);

            Assert.Equal("es", _email.LastLanguage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Fact]
    public async Task InitiateResetAsync_UserHasNoPreferredLanguage_AmbientUiCultureEnglish_PassesEnglish()
    {
        var email = await SeedUserAsync(null);
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");

            await CreateService().InitiateResetAsync(email);

            Assert.Equal("en", _email.LastLanguage);
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    // ----------------------------------------------------------------- multilingual plan (2026-10-06)
    // phase 2, carry-over P3: ResetPasswordAsync's message renders in the UI culture — English under
    // "en", Spanish under "es". Previously hard-coded English, silently overriding AuthController's
    // own localized fallback (<c>result.Message ?? _localizer["AuthApi.PasswordResetFailed"]</c>).

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_UnderEnglishCulture_MessageIsEnglish()
    {
        using var _ = CultureScope.For("en");
        var result = await CreateService().ResetPasswordAsync("not-a-real-token", "NewPassword1!");

        Assert.False(result.Success);
        Assert.Equal("Invalid or expired reset token.", result.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_UnderSpanishCulture_MessageIsSpanish()
    {
        using var _ = CultureScope.For("es");
        var result = await CreateService().ResetPasswordAsync("not-a-real-token", "NewPassword1!");

        Assert.False(result.Success);
        Assert.Equal("El enlace para restablecer la contraseña no es válido o ha vencido.", result.Message);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>Captures the language passed to the password-reset email.</summary>
    private sealed class CapturingEmailService : TestSupport.TestEmailServiceBase
    {
        public string? LastLanguage { get; private set; }

        public override Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string? language = null, CancellationToken ct = default)
        {
            LastLanguage = language;
            return Task.CompletedTask;
        }
    }
}
