using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using IepAssistant.Domain.Data;
using IepAssistant.Domain.Entities;
using IepAssistant.Services.Interfaces;
using IepAssistant.Services.Models;

namespace IepAssistant.Services.Implementations;

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SubscriptionService> _logger;

    private const int AnalysisLimitPerChild = 5;

    public SubscriptionService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<SubscriptionService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;

        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreateCheckoutSessionAsync(int userId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct)
            ?? throw new InvalidOperationException("User not found");

        // Get or create Stripe Customer
        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = user.Email,
                Name = user.FullName,
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId.ToString()
                }
            }, cancellationToken: ct);

            user.StripeCustomerId = customer.Id;
            await _context.SaveChangesAsync(ct);
        }

        var priceId = _configuration["Stripe:PriceId"]
            ?? throw new InvalidOperationException("Stripe:PriceId not configured");

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(new SessionCreateOptions
        {
            Customer = user.StripeCustomerId,
            Mode = "subscription",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl
        }, cancellationToken: ct);

        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(int userId, string returnUrl, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct)
            ?? throw new InvalidOperationException("User not found");

        if (string.IsNullOrEmpty(user.StripeCustomerId))
            throw new InvalidOperationException("User has no Stripe customer ID");

        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = user.StripeCustomerId,
            ReturnUrl = returnUrl
        }, cancellationToken: ct);

        return session.Url;
    }

    public async Task<SubscriptionStatusModel> GetStatusAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct)
            ?? throw new InvalidOperationException("User not found");

        var result = new SubscriptionStatusModel
        {
            Status = user.SubscriptionStatus,
            ExpiresAt = user.SubscriptionExpiresAt
        };

        // Get all children accessible to this user
        var childAccesses = await _context.ChildAccesses
            .Where(ca => ca.UserId == userId)
            .Include(ca => ca.ChildProfile)
            .Where(ca => ca.ChildProfile.IsActive)
            .ToListAsync(ct);

        // Also include children the user owns directly
        var ownedChildren = await _context.ChildProfiles
            .Where(cp => cp.UserId == userId && cp.IsActive)
            .ToListAsync(ct);

        var allChildIds = childAccesses.Select(ca => ca.ChildProfileId)
            .Union(ownedChildren.Select(c => c.Id))
            .Distinct()
            .ToList();

        var allChildren = childAccesses.Select(ca => ca.ChildProfile)
            .Union(ownedChildren)
            .DistinctBy(c => c.Id)
            .ToList();

        // Get subscription start date for usage window
        var subscriptionStart = GetSubscriptionYearStart(user);

        foreach (var child in allChildren)
        {
            var analysisCount = await _context.UsageRecords
                .CountAsync(ur =>
                    ur.UserId == userId &&
                    ur.ChildProfileId == child.Id &&
                    ur.OperationType == "analysis" &&
                    ur.CreatedAt >= subscriptionStart,
                    ct);

            result.ChildUsage[child.Id] = new ChildUsageModel
            {
                ChildId = child.Id,
                ChildName = $"{child.FirstName} {child.LastName}".Trim(),
                AnalysisCount = analysisCount,
                AnalysisLimit = AnalysisLimitPerChild
            };
        }

        return result;
    }

    public async Task HandleWebhookEventAsync(string json, string signature, CancellationToken ct = default)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"]
            ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");

        var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

        _logger.LogInformation("Processing Stripe webhook event {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionUpdatedAsync(stripeEvent, ct);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(stripeEvent, ct);
                break;

            case "invoice.payment_succeeded":
                await HandlePaymentSucceededAsync(stripeEvent, ct);
                break;

            case "invoice.payment_failed":
                await HandlePaymentFailedAsync(stripeEvent, ct);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    public async Task<bool> HasActiveSubscriptionAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct);
        if (user == null) return false;

        if (user.SubscriptionStatus != "active") return false;

        // Check if subscription has expired (especially for beta codes with no Stripe renewal)
        if (user.SubscriptionExpiresAt.HasValue && user.SubscriptionExpiresAt.Value < DateTime.UtcNow)
        {
            user.SubscriptionStatus = "expired";
            await _context.SaveChangesAsync(ct);
            return false;
        }

        return true;
    }

    public async Task<bool> CanPerformAnalysisAsync(int userId, int childId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct);
        if (user == null || user.SubscriptionStatus != "active")
            return false;

        var subscriptionStart = GetSubscriptionYearStart(user);

        var count = await _context.UsageRecords
            .CountAsync(ur =>
                ur.UserId == userId &&
                ur.ChildProfileId == childId &&
                ur.OperationType == "analysis" &&
                ur.CreatedAt >= subscriptionStart,
                ct);

        return count < AnalysisLimitPerChild;
    }

    public async Task RecordUsageAsync(int userId, int childId, string operationType, CancellationToken ct = default)
    {
        var record = new UsageRecord
        {
            UserId = userId,
            ChildProfileId = childId,
            OperationType = operationType,
            CreatedAt = DateTime.UtcNow
        };

        await _context.UsageRecords.AddAsync(record, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> TryRecordUsageAsync(int userId, int childId, string operationType, int limit, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var user = await _context.Users.FindAsync([userId], ct);
            if (user == null || user.SubscriptionStatus != "active")
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            var subscriptionStart = GetSubscriptionYearStart(user);

            var count = await _context.UsageRecords
                .CountAsync(ur =>
                    ur.UserId == userId &&
                    ur.ChildProfileId == childId &&
                    ur.OperationType == operationType &&
                    ur.CreatedAt >= subscriptionStart,
                    ct);

            if (count >= limit)
            {
                await transaction.RollbackAsync(ct);
                return false;
            }

            var record = new UsageRecord
            {
                UserId = userId,
                ChildProfileId = childId,
                OperationType = operationType,
                CreatedAt = DateTime.UtcNow
            };

            await _context.UsageRecords.AddAsync(record, ct);
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ServiceResult> RedeemBetaCodeAsync(int userId, string code, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([userId], ct);
        if (user == null)
            return ServiceResult.FailureResult("User not found");

        if (user.SubscriptionStatus == "active")
            return ServiceResult.FailureResult("You already have an active subscription");

        // Atomic redemption: only update if code is valid AND not yet redeemed
        var rowsAffected = await _context.Set<BetaInviteCode>()
            .Where(c => c.Code == code && c.IsActive && c.RedeemedByUserId == null
                && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.RedeemedByUserId, userId)
                .SetProperty(c => c.RedeemedAt, DateTime.UtcNow), ct);

        if (rowsAffected == 0)
            return ServiceResult.FailureResult("Invalid invite code.");

        // Grant subscription
        user.SubscriptionStatus = "active";
        user.SubscriptionExpiresAt = DateTime.UtcNow.AddYears(1);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} redeemed beta code {Code}", userId, code);

        return ServiceResult.SuccessResult("Beta code redeemed successfully. Your subscription is now active.");
    }

    public async Task<ServiceResult<List<string>>> GenerateBetaCodesAsync(int count, DateTime? expiresAt, CancellationToken ct = default)
    {
        if (count <= 0 || count > 100)
            return ServiceResult<List<string>>.FailureResult("Count must be between 1 and 100");

        var codes = new List<string>();

        for (var i = 0; i < count; i++)
        {
            var code = GenerateRandomCode(8);
            var betaCode = new BetaInviteCode
            {
                Code = code,
                ExpiresAt = expiresAt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.BetaInviteCodes.AddAsync(betaCode, ct);
            codes.Add(code);
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Generated {Count} beta invite codes", count);

        return ServiceResult<List<string>>.SuccessResult(codes);
    }

    public async Task<IEnumerable<BetaCodeModel>> ListBetaCodesAsync(CancellationToken ct = default)
    {
        var codes = await _context.BetaInviteCodes
            .Include(b => b.RedeemedBy)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        return codes.Select(b => new BetaCodeModel
        {
            Id = b.Id,
            Code = b.Code,
            IsRedeemed = b.RedeemedByUserId != null,
            RedeemedByEmail = b.RedeemedBy?.Email,
            RedeemedAt = b.RedeemedAt,
            ExpiresAt = b.ExpiresAt
        });
    }

    // --- Private helpers ---

    private static DateTime GetSubscriptionYearStart(User user)
    {
        if (user.SubscriptionExpiresAt == null)
            return DateTime.UtcNow.AddYears(-1);

        // Subscription year starts 1 year before expiry
        return user.SubscriptionExpiresAt.Value.AddYears(-1);
    }

    private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        var user = await FindUserByStripeCustomerIdAsync(subscription.CustomerId, ct);
        if (user == null)
        {
            _logger.LogWarning("No user found for Stripe customer {CustomerId}", subscription.CustomerId);
            return;
        }

        user.StripeSubscriptionId = subscription.Id;
        user.SubscriptionStatus = MapStripeStatus(subscription.Status);

        // In Stripe.net v50+, CurrentPeriodEnd is on SubscriptionItem, not Subscription
        var firstItem = subscription.Items?.Data?.FirstOrDefault();
        if (firstItem?.CurrentPeriodEnd != null)
        {
            user.SubscriptionExpiresAt = firstItem.CurrentPeriodEnd;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Updated subscription for user {UserId}: status={Status}", user.Id, user.SubscriptionStatus);
    }

    private async Task HandleSubscriptionDeletedAsync(Event stripeEvent, CancellationToken ct)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        var user = await FindUserByStripeCustomerIdAsync(subscription.CustomerId, ct);
        if (user == null) return;

        user.SubscriptionStatus = "expired";

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Subscription deleted for user {UserId}", user.Id);
    }

    private async Task HandlePaymentSucceededAsync(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        var user = await FindUserByStripeCustomerIdAsync(invoice.CustomerId, ct);
        if (user == null) return;

        user.SubscriptionStatus = "active";

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Payment succeeded for user {UserId}", user.Id);
    }

    private async Task HandlePaymentFailedAsync(Event stripeEvent, CancellationToken ct)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        var user = await FindUserByStripeCustomerIdAsync(invoice.CustomerId, ct);
        if (user == null) return;

        user.SubscriptionStatus = "past_due";

        await _context.SaveChangesAsync(ct);

        _logger.LogWarning("Payment failed for user {UserId}", user.Id);
    }

    private async Task<User?> FindUserByStripeCustomerIdAsync(string customerId, CancellationToken ct)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.StripeCustomerId == customerId, ct);
    }

    private static string MapStripeStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => "active",
            "past_due" => "past_due",
            "canceled" => "canceled",
            "unpaid" => "past_due",
            "incomplete" => "none",
            "incomplete_expired" => "expired",
            "trialing" => "active",
            _ => "none"
        };
    }

    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluded I, O, 0, 1 to avoid confusion
        var bytes = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }
}
