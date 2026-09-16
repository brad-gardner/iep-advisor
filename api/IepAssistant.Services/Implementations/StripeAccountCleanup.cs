using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using IepAssistant.Services.Interfaces;

namespace IepAssistant.Services.Implementations;

/// <inheritdoc cref="IStripeAccountCleanup"/>
public class StripeAccountCleanup : IStripeAccountCleanup
{
    private readonly ILogger<StripeAccountCleanup> _logger;
    private readonly bool _configured;

    public StripeAccountCleanup(IConfiguration configuration, ILogger<StripeAccountCleanup> logger)
    {
        _logger = logger;
        var key = configuration["Stripe:SecretKey"];
        _configured = !string.IsNullOrWhiteSpace(key);
        if (_configured)
            StripeConfiguration.ApiKey = key;
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        EnsureConfigured();
        try
        {
            await new Stripe.SubscriptionService().CancelAsync(subscriptionId, new Stripe.SubscriptionCancelOptions(), cancellationToken: ct);
        }
        catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound || ex.StripeError?.Code == "resource_missing")
        {
            _logger.LogWarning("Stripe subscription {SubscriptionId} already gone while purging an account", subscriptionId);
        }
    }

    public async Task DeleteCustomerAsync(string customerId, CancellationToken ct = default)
    {
        EnsureConfigured();
        try
        {
            await new Stripe.CustomerService().DeleteAsync(customerId, cancellationToken: ct);
        }
        catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound || ex.StripeError?.Code == "resource_missing")
        {
            _logger.LogWarning("Stripe customer {CustomerId} already gone while purging an account", customerId);
        }
    }

    private void EnsureConfigured()
    {
        if (!_configured)
            throw new InvalidOperationException("Stripe is not configured (Stripe:SecretKey); cannot clean up a billed account.");
    }
}
