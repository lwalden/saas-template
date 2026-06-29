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

/// <summary>
/// Append-only record of a security- or account-relevant action, for compliance
/// (SOC 2 / GDPR), security investigations, and customer-facing activity views.
/// Written via <c>IAuditLogger</c>; never updated or deleted in the normal flow.
/// </summary>
public class AuditEvent
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Acting user id, or null for anonymous/system actions.</summary>
    public string? UserId { get; set; }

    /// <summary>Denormalised actor email so the log stays readable if the user is later renamed/deleted.</summary>
    public string? Email { get; set; }

    /// <summary>Dotted action name, e.g. <c>auth.login.succeeded</c> (see <see cref="AuditAction"/>).</summary>
    public required string Action { get; set; }

    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Optional JSON blob of extra context. Never store secrets here.</summary>
    public string? Metadata { get; set; }
}

/// <summary>
/// A single recorded billable unit of usage for a user. Append-only: one row per
/// metered action (see <c>IUsageService.RecordUsageAsync</c>). Usage for a billing
/// period is computed by summing <see cref="Quantity"/> over rows whose
/// <see cref="OccurredAt"/> falls in the period window.
/// </summary>
/// <remarks>
/// The "billable unit" is whatever a row represents — by default one event with
/// <see cref="Quantity"/> 1 under the <c>"default"</c> <see cref="Meter"/>. Products
/// that bill on a different axis (seats, API calls, scans) record with a different
/// quantity/meter without changing this schema.
/// </remarks>
public class UsageEvent
{
    public Guid Id { get; set; }

    /// <summary>Owning user id.</summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Logical meter name. Lets one product track multiple billable axes
    /// (e.g. "default", "scans", "api_calls"). Quota enforcement counts the
    /// default meter unless told otherwise.
    /// </summary>
    public string Meter { get; set; } = UsageMeter.Default;

    /// <summary>How many billable units this row represents. Usually 1.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>When the usage occurred (UTC). Anchored to the Stripe billing period for counting.</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Row creation time (UTC). Mirrors the convention used by other entities.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Canonical usage meter names. The "default" meter backs quota enforcement.</summary>
public static class UsageMeter
{
    public const string Default = "default";
}

/// <summary>Canonical audit action names. Keep stable — they are queried and reported on.</summary>
public static class AuditAction
{
    public const string UserRegistered = "auth.user.registered";
    public const string LoginSucceeded = "auth.login.succeeded";
    public const string LoginFailed = "auth.login.failed";
    public const string PasswordResetRequested = "auth.password_reset.requested";
    public const string PasswordResetCompleted = "auth.password_reset.completed";
    public const string EmailVerificationSent = "auth.email_verification.sent";
    public const string EmailVerified = "auth.email_verification.verified";
}
