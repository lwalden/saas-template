using Microsoft.EntityFrameworkCore;
using SaasTemplate.Api.Billing;
using SaasTemplate.Api.Data;
using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

/// <summary>
/// FEAT-07: unit tests for <see cref="UsageService"/> — counting within a period,
/// period rollover, quota enforcement, and the unlimited tier.
/// </summary>
public class UsageServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UsageService _sut;

    public UsageServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _sut = new UsageService(_db);
    }

    private SubscriptionEntity Seed(string tier, DateTime? currentPeriodEnd)
    {
        var userId = Guid.NewGuid().ToString();
        _db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.com",
            Email = $"{userId}@example.com",
            NormalizedUserName = $"{userId}@EXAMPLE.COM".ToUpperInvariant(),
            NormalizedEmail = $"{userId}@EXAMPLE.COM".ToUpperInvariant()
        });
        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StripeCustomerId = $"cus_{userId[..8]}",
            StripeSubscriptionId = $"sub_{userId[..8]}",
            StripePriceId = "price_x",
            Tier = tier,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = currentPeriodEnd,
            CreatedAt = DateTime.UtcNow
        };
        _db.Subscriptions.Add(sub);
        _db.SaveChanges();
        return sub;
    }

    [Fact]
    public async Task RecordUsage_then_GetUsage_counts_within_period()
    {
        var end = DateTime.UtcNow.AddDays(10);
        var sub = Seed(SubscriptionTier.Starter, end);

        await _sut.RecordUsageAsync(sub.UserId);
        await _sut.RecordUsageAsync(sub.UserId, quantity: 3);

        var usage = await _sut.GetUsageAsync(sub.UserId, sub);

        Assert.Equal(4, usage.Used);
        Assert.Equal(100, usage.Limit); // Starter quota
        Assert.Equal(end.AddMonths(-1), usage.PeriodStart);
        Assert.False(usage.IsUnlimited);
    }

    [Fact]
    public async Task GetUsage_ignores_events_outside_the_billing_period()
    {
        var end = DateTime.UtcNow.AddDays(5);
        var sub = Seed(SubscriptionTier.Starter, end);
        var periodStart = end.AddMonths(-1);

        // Inside the window
        await _sut.RecordUsageAsync(sub.UserId, occurredAt: periodStart.AddDays(1));
        // Before the window (previous period) — must NOT count
        await _sut.RecordUsageAsync(sub.UserId, occurredAt: periodStart.AddDays(-2));
        // After the window (future period) — must NOT count
        await _sut.RecordUsageAsync(sub.UserId, occurredAt: end.AddDays(1));

        var usage = await _sut.GetUsageAsync(sub.UserId, sub);

        Assert.Equal(1, usage.Used);
    }

    [Fact]
    public async Task GetUsage_isolates_usage_per_user()
    {
        var end = DateTime.UtcNow.AddDays(10);
        var a = Seed(SubscriptionTier.Starter, end);
        var b = Seed(SubscriptionTier.Starter, end);

        await _sut.RecordUsageAsync(a.UserId, quantity: 5);
        await _sut.RecordUsageAsync(b.UserId, quantity: 2);

        Assert.Equal(5, (await _sut.GetUsageAsync(a.UserId, a)).Used);
        Assert.Equal(2, (await _sut.GetUsageAsync(b.UserId, b)).Used);
    }

    [Fact]
    public async Task CheckQuota_allows_when_under_limit()
    {
        var sub = Seed(SubscriptionTier.Starter, DateTime.UtcNow.AddDays(10));
        await _sut.RecordUsageAsync(sub.UserId, quantity: 10);

        var result = await _sut.CheckQuotaAsync(sub.UserId);

        Assert.True(result.Allowed);
        Assert.Equal(10, result.Used);
        Assert.Equal(100, result.Limit);
        Assert.Equal(90, result.Remaining);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task CheckQuota_blocks_when_at_or_over_limit_with_upgrade_cta()
    {
        var sub = Seed(SubscriptionTier.Starter, DateTime.UtcNow.AddDays(10));
        await _sut.RecordUsageAsync(sub.UserId, quantity: 100); // exactly at limit

        var result = await _sut.CheckQuotaAsync(sub.UserId);

        Assert.False(result.Allowed);
        Assert.Equal(0, result.Remaining);
        Assert.NotNull(result.Message);
        Assert.Contains("Upgrade", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckQuota_unlimited_tier_never_blocks()
    {
        var sub = Seed(SubscriptionTier.Business, DateTime.UtcNow.AddDays(10)); // int.MaxValue quota
        await _sut.RecordUsageAsync(sub.UserId, quantity: 1_000_000);

        var result = await _sut.CheckQuotaAsync(sub.UserId);

        Assert.True(result.Allowed);
        Assert.True(result.IsUnlimited);
        Assert.Equal(int.MaxValue, result.Limit);

        var usage = await _sut.GetUsageAsync(sub.UserId, sub);
        Assert.True(usage.IsUnlimited);
    }

    [Fact]
    public async Task CheckQuota_free_tier_blocks_immediately()
    {
        // No subscription → resolves to Free tier (quota 0) → blocked from the first action.
        var userId = Guid.NewGuid().ToString();

        var result = await _sut.CheckQuotaAsync(userId);

        Assert.False(result.Allowed);
        Assert.Equal(0, result.Limit);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task CheckQuota_counts_only_default_meter()
    {
        var sub = Seed(SubscriptionTier.Starter, DateTime.UtcNow.AddDays(10));
        await _sut.RecordUsageAsync(sub.UserId, quantity: 50, meter: "other");

        var result = await _sut.CheckQuotaAsync(sub.UserId);

        Assert.Equal(0, result.Used); // "other" meter doesn't count against the default quota
        Assert.True(result.Allowed);
    }

    public void Dispose() => _db.Dispose();
}
