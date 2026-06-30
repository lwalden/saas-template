using Microsoft.EntityFrameworkCore;
using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Billing;

/// <summary>
/// Default <see cref="IUsageService"/> backed by the <see cref="UsageEvent"/> table.
/// Recording is a single cheap insert; counting sums quantities over the current
/// billing-period window so usage "resets" when the Stripe period rolls over.
/// </summary>
public sealed class UsageService : IUsageService
{
    private readonly AppDbContext _db;

    public UsageService(AppDbContext db) => _db = db;

    public async Task RecordUsageAsync(
        string userId,
        int quantity = 1,
        string meter = UsageMeter.Default,
        DateTime? occurredAt = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("userId is required.", nameof(userId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be positive.");

        var now = DateTime.UtcNow;
        _db.UsageEvents.Add(new UsageEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Meter = string.IsNullOrWhiteSpace(meter) ? UsageMeter.Default : meter,
            Quantity = quantity,
            OccurredAt = occurredAt ?? now,
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);

        // OPTIONAL Stripe metered-price reporting hook would go here, behind a config
        // toggle that is OFF by default and never fires under Testing. Intentionally
        // omitted: this environment blocks egress to api.stripe.com. See FEAT-07 §5.
    }

    public async Task<UsageStatus> GetUsageAsync(
        string userId,
        SubscriptionEntity subscription,
        string meter = UsageMeter.Default,
        CancellationToken cancellationToken = default)
    {
        var limit = TierLimits.ForTier(subscription.Tier).MonthlyQuota;
        var (periodStart, periodEnd) = PeriodWindow(subscription);

        var used = await CountAsync(userId, meter, periodStart, periodEnd, cancellationToken);
        return new UsageStatus(used, limit, periodStart);
    }

    public async Task<QuotaCheckResult> CheckQuotaAsync(
        string userId,
        string meter = UsageMeter.Default,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _db.Subscriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        var limit = TierLimits.ForTier(subscription?.Tier).MonthlyQuota;

        // Unlimited tier: always allowed, no need to count.
        if (limit == int.MaxValue)
            return new QuotaCheckResult(Allowed: true, Used: 0, Limit: limit, Remaining: int.MaxValue, Message: null);

        var (periodStart, periodEnd) = PeriodWindow(subscription);
        var used = await CountAsync(userId, meter, periodStart, periodEnd, cancellationToken);

        var remaining = Math.Max(0, limit - used);
        var allowed = used < limit;
        var message = allowed
            ? null
            : limit == 0
                ? "Your current plan does not include this feature. Upgrade to a paid plan to continue."
                : $"You've used all {limit} of your monthly quota. Upgrade your plan for a higher limit.";

        return new QuotaCheckResult(allowed, used, limit, remaining, message);
    }

    private async Task<int> CountAsync(string userId, string meter, DateTime periodStart, DateTime? periodEnd, CancellationToken ct)
    {
        var q = _db.UsageEvents
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.Meter == meter && u.OccurredAt >= periodStart);

        if (periodEnd.HasValue)
            q = q.Where(u => u.OccurredAt < periodEnd.Value);

        // Nullable cast so an empty set sums to null → coalesced to 0.
        return await q.SumAsync(u => (int?)u.Quantity, ct) ?? 0;
    }

    /// <summary>
    /// Computes the current billing-period window. Mirrors the legacy GetUsage logic:
    /// periodStart = CurrentPeriodEnd.AddMonths(-1), periodEnd = CurrentPeriodEnd.
    /// When CurrentPeriodEnd is unknown, fall back to the start of the current month
    /// with an open-ended window (matches the previous behaviour).
    /// </summary>
    public static (DateTime PeriodStart, DateTime? PeriodEnd) PeriodWindow(SubscriptionEntity? subscription)
    {
        if (subscription?.CurrentPeriodEnd is DateTime end)
            return (end.AddMonths(-1), end);

        var now = DateTime.UtcNow;
        return (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), null);
    }
}
