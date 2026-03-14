using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string resetToken, CancellationToken ct = default)
    {
        var frontendUrl = _configuration["App:FrontendUrl"] ?? "http://localhost:5173";
        var resetUrl = $"{frontendUrl}/reset-password?token={resetToken}";

        // TODO: Integrate Azure Communication Services for production email
        _logger.LogInformation("Password reset requested for {Email}. Reset URL: {ResetUrl}", toEmail, resetUrl);

        return Task.CompletedTask;
    }
}
