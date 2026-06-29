using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Billing;

/// <summary>
/// Records and queries metered usage per user, anchored to the user's Stripe
/// billing period, and enforces the per-tier <see cref="TierConfig.MonthlyQuota"/>.
/// </summary>
/// <remarks>
/// The canonical "billable unit" extension point is <see cref="RecordUsageAsync"/>:
/// call it once per metered action. Default is one unit on the <c>"default"</c> meter,
/// which is what quota enforcement counts. Products that bill on a different axis pass
/// a different <c>quantity</c>/<c>meter</c> without schema changes.
/// </remarks>
public interface IUsageService
{
    /// <summary>
    /// Records a billable unit of usage for <paramref name="userId"/>.
    /// THIS IS THE BILLABLE-UNIT EXTENSION POINT — call once per metered action.
    /// Cheap: a single insert on the hot path.
    /// </summary>
    /// <param name="userId">Owning user id.</param>
    /// <param name="quantity">Units to record (default 1).</param>
    /// <param name="meter">Logical meter; default <see cref="UsageMeter.Default"/> backs quota checks.</param>
    /// <param name="occurredAt">When the action happened (UTC); defaults to now.</param>
    Task RecordUsageAsync(
        string userId,
        int quantity = 1,
        string meter = UsageMeter.Default,
        DateTime? occurredAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns usage for the current billing period: total units used, the tier limit,
    /// and the period start. Usage is summed over the window
    /// <c>[periodStart, CurrentPeriodEnd)</c> where <c>periodStart = CurrentPeriodEnd.AddMonths(-1)</c>
    /// (mirrors the legacy GetUsage logic). <c>Limit == int.MaxValue</c> means unlimited.
    /// </summary>
    Task<UsageStatus> GetUsageAsync(
        string userId,
        SubscriptionEntity subscription,
        string meter = UsageMeter.Default,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reusable enforcement primitive. Returns whether the user is allowed to perform
    /// another metered action under their tier quota, plus remaining/limit and an
    /// upgrade CTA message when blocked. Unlimited tier (<see cref="int.MaxValue"/>)
    /// always passes. Users with no subscription resolve to the Free tier (quota 0).
    /// </summary>
    Task<QuotaCheckResult> CheckQuotaAsync(
        string userId,
        string meter = UsageMeter.Default,
        CancellationToken cancellationToken = default);
}

/// <summary>Usage snapshot for a billing period. <c>Limit == int.MaxValue</c> ⇒ unlimited.</summary>
public sealed record UsageStatus(int Used, int Limit, DateTime PeriodStart)
{
    public bool IsUnlimited => Limit == int.MaxValue;

    /// <summary>Remaining units, clamped at 0. <see cref="int.MaxValue"/> when unlimited.</summary>
    public int Remaining => IsUnlimited ? int.MaxValue : Math.Max(0, Limit - Used);
}

/// <summary>Result of a quota check. <see cref="Allowed"/> is false once usage has reached the limit.</summary>
public sealed record QuotaCheckResult(bool Allowed, int Used, int Limit, int Remaining, string? Message)
{
    public bool IsUnlimited => Limit == int.MaxValue;
}
