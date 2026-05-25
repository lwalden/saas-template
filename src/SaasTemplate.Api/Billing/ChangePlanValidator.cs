using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Billing;

/// <summary>
/// Validates a plan change request before calling Stripe.
/// Returns a validation result with an error message if invalid.
/// </summary>
public static class ChangePlanValidator
{
    public static ChangePlanValidation Validate(
        string currentTier, string targetTier, string subscriptionStatus)
    {
        if (subscriptionStatus is not (SubscriptionStatus.Active or SubscriptionStatus.Trialing))
            return ChangePlanValidation.Fail("Subscription must be active to change plans.");

        if (string.Equals(currentTier, targetTier, StringComparison.OrdinalIgnoreCase))
            return ChangePlanValidation.Fail("You are already on the same plan.");

        // Validate target tier is a known tier
        if (targetTier is not (SubscriptionTier.Starter or SubscriptionTier.Professional or SubscriptionTier.Business))
            return ChangePlanValidation.Fail($"Unknown target tier: {targetTier}");

        return ChangePlanValidation.Ok();
    }
}

public sealed record ChangePlanValidation(bool IsValid, string? Error)
{
    public static ChangePlanValidation Ok() => new(true, null);
    public static ChangePlanValidation Fail(string error) => new(false, error);
}
