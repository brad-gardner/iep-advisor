namespace IepAssistant.Services.Interfaces;

public interface ITotpService
{
    string GenerateSecret();
    string GenerateCode(string base32Secret, DateTimeOffset? timestamp = null);
    bool ValidateCode(string base32Secret, string code, int driftSteps = 1);
    long GetTimestamp(DateTimeOffset? timestamp = null);
}
