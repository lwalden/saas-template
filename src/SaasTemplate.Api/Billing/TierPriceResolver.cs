using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Billing;

/// <summary>
/// Maps between subscription tier names and Stripe price IDs.
/// Centralises the price-ID-to-tier resolution that was previously duplicated
/// across BillingEndpoints, StripeWebhookHandler, and the backfill fallback.
/// </summary>
public static class TierPriceResolver
{
    /// <summary>
    /// Resolves a tier name to its monthly Stripe price ID.
    /// Returns null for unknown tiers.
    /// </summary>
    public static string? ResolvePriceId(string tier, IConfiguration config) => tier switch
    {
        SubscriptionTier.Starter => config["STRIPE_PRICE_ID_STARTER"],
        SubscriptionTier.Professional => config["STRIPE_PRICE_ID_PRO"],
        SubscriptionTier.Business => config["STRIPE_PRICE_ID_BUSINESS"],
        _ => null
    };

    /// <summary>
    /// Resolves a Stripe price ID (monthly or annual) back to a tier name.
    /// Returns null for unrecognised price IDs.
    /// </summary>
    public static string? ResolveTierFromPriceId(string? priceId, IConfiguration config)
    {
        if (priceId is null) return null;

        if (priceId == config["STRIPE_PRICE_ID_STARTER"] || priceId == config["STRIPE_PRICE_ID_STARTER_ANNUAL"])
            return SubscriptionTier.Starter;
        if (priceId == config["STRIPE_PRICE_ID_PRO"] || priceId == config["STRIPE_PRICE_ID_PRO_ANNUAL"])
            return SubscriptionTier.Professional;
        if (priceId == config["STRIPE_PRICE_ID_BUSINESS"] || priceId == config["STRIPE_PRICE_ID_BUSINESS_ANNUAL"])
            return SubscriptionTier.Business;

        return null;
    }
}
