using Microsoft.AspNetCore.Identity;

namespace SaasTemplate.Api.Data;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Positive consent flag: true = user has opted in to marketing emails.
    /// Default false = must explicitly opt in (CAN-SPAM / GDPR compliant).
    /// </summary>
    public bool MarketingConsent { get; set; }
    public DateTime? TosAcceptedAt { get; set; }
    public SubscriptionEntity? Subscription { get; set; }
}

public class SubscriptionEntity
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public required string StripeCustomerId { get; set; }
    public required string StripeSubscriptionId { get; set; }
    public required string StripePriceId { get; set; }
    public required string Tier { get; set; }
    public string Status { get; set; } = SubscriptionStatus.Active;
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    /// <summary>Onboarding drip stage: 0=none, 1=welcome sent, 2=day-3 sent, 3=day-7 sent (complete)</summary>
    public int OnboardingStage { get; set; }
}

public static class SubscriptionStatus
{
    public const string Active = "active";
    public const string Trialing = "trialing";
    public const string PastDue = "past_due";
    public const string Cancelled = "cancelled";
}

public static class SubscriptionTier
{
    public const string Starter = "starter";
    public const string Professional = "professional";
    public const string Business = "business";
}
