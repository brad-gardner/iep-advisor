namespace IepAssistant.Services.Interfaces;

/// <summary>
/// The two Stripe calls an account purge must make before the local User row goes away: cancel the
/// subscription (so the card on file stops being charged) and delete the customer (so Stripe no longer
/// holds the parent's PII/payment method). Failures throw — a purge must not delete the local record
/// while Stripe still bills.
/// </summary>
public interface IStripeAccountCleanup
{
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
    Task DeleteCustomerAsync(string customerId, CancellationToken ct = default);
}
