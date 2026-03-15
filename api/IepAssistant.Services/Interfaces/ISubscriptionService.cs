using IepAssistant.Services.Models;

namespace IepAssistant.Services.Interfaces;

public interface ISubscriptionService
{
    Task<string> CreateCheckoutSessionAsync(int userId, string successUrl, string cancelUrl, CancellationToken ct = default);
    Task<string> CreatePortalSessionAsync(int userId, string returnUrl, CancellationToken ct = default);
    Task<SubscriptionStatusModel> GetStatusAsync(int userId, CancellationToken ct = default);
    Task HandleWebhookEventAsync(string json, string signature, CancellationToken ct = default);
    Task<bool> HasActiveSubscriptionAsync(int userId, CancellationToken ct = default);
    Task<bool> CanPerformAnalysisAsync(int userId, int childId, CancellationToken ct = default);
    Task RecordUsageAsync(int userId, int childId, string operationType, CancellationToken ct = default);
    Task<bool> TryRecordUsageAsync(int userId, int childId, string operationType, int limit, CancellationToken ct = default);
    Task<ServiceResult> RedeemBetaCodeAsync(int userId, string code, CancellationToken ct = default);
    Task<ServiceResult<List<string>>> GenerateBetaCodesAsync(int count, DateTime? expiresAt, CancellationToken ct = default);
    Task<IEnumerable<BetaCodeModel>> ListBetaCodesAsync(CancellationToken ct = default);
}
