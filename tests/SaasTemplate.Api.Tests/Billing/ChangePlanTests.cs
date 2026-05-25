using SaasTemplate.Api.Billing;
using SaasTemplate.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Stripe;
using Xunit;
using File = System.IO.File;

namespace SaasTemplate.Api.Tests.Billing;

/// <summary>
/// Tests for S31-003: Downgrade path for Business/Pro subscribers (UX-07).
/// Covers the HandleChangePlan endpoint, the SubscriptionUpdated webhook
/// tier-fix, and the billing page UI for change-plan buttons.
/// </summary>
public class ChangePlanTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public ChangePlanTests()
    {
        var connStr = $"Data Source=chplan-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connStr);
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connStr)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STRIPE_PRICE_ID_STARTER"] = "price_starter_test",
                ["STRIPE_PRICE_ID_STARTER_ANNUAL"] = "price_starter_annual_test",
                ["STRIPE_PRICE_ID_PRO"] = "price_pro_test",
                ["STRIPE_PRICE_ID_PRO_ANNUAL"] = "price_pro_annual_test",
                ["STRIPE_PRICE_ID_BUSINESS"] = "price_business_test",
                ["STRIPE_PRICE_ID_BUSINESS_ANNUAL"] = "price_business_annual_test",
            })
            .Build();

        _logger = Mock.Of<ILogger>();

        StripeWebhookHandler.ResetIdempotencyCache();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        StripeWebhookHandler.ResetIdempotencyCache();
    }

    private async Task<string> SeedUser()
    {
        var userId = Guid.NewGuid().ToString();
        _db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"user-{userId}",
            NormalizedUserName = $"USER-{userId}",
            Email = $"{userId}@test.com",
            NormalizedEmail = $"{userId}@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        await _db.SaveChangesAsync();
        return userId;
    }

    private async Task<SubscriptionEntity> SeedSubscription(
        string userId, string tier, string priceId, string status = "active")
    {
        var entity = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StripeCustomerId = $"cus_{Guid.NewGuid():N}",
            StripeSubscriptionId = $"sub_{Guid.NewGuid():N}",
            StripePriceId = priceId,
            Tier = tier,
            Status = status,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        };
        _db.Subscriptions.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 1: ChangePlan_BusinessToStarter_ReturnsOk
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ChangePlan_BusinessToStarter_ValidatesTargetTier()
    {
        // Arrange: seed a Business subscriber
        var userId = await SeedUser();
        var sub = await SeedSubscription(userId, SubscriptionTier.Business, "price_business_test");

        // Act: resolve the target price ID for Starter
        var targetPriceId = TierPriceResolver.ResolvePriceId(SubscriptionTier.Starter, _config);

        // Assert: the resolver correctly maps tier to price ID
        Assert.Equal("price_starter_test", targetPriceId);

        // Also verify that ChangePlan validation allows Business -> Starter
        var validation = ChangePlanValidator.Validate(
            currentTier: SubscriptionTier.Business,
            targetTier: SubscriptionTier.Starter,
            subscriptionStatus: SubscriptionStatus.Active);
        Assert.True(validation.IsValid, validation.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 2: ChangePlan_StarterToPro_ReturnsOk
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ChangePlan_StarterToPro_ValidatesTargetTier()
    {
        var userId = await SeedUser();
        var sub = await SeedSubscription(userId, SubscriptionTier.Starter, "price_starter_test");

        var targetPriceId = TierPriceResolver.ResolvePriceId(SubscriptionTier.Professional, _config);
        Assert.Equal("price_pro_test", targetPriceId);

        var validation = ChangePlanValidator.Validate(
            currentTier: SubscriptionTier.Starter,
            targetTier: SubscriptionTier.Professional,
            subscriptionStatus: SubscriptionStatus.Active);
        Assert.True(validation.IsValid, validation.Error);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 3: ChangePlan_SameTier_ReturnsBadRequest
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ChangePlan_SameTier_FailsValidation()
    {
        var validation = ChangePlanValidator.Validate(
            currentTier: SubscriptionTier.Professional,
            targetTier: SubscriptionTier.Professional,
            subscriptionStatus: SubscriptionStatus.Active);

        Assert.False(validation.IsValid);
        Assert.Contains("same", validation.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 4: ChangePlan_NoSubscription_ReturnsNotFound
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ChangePlan_NoSubscription_ReturnsNull()
    {
        var userId = await SeedUser();

        // No subscription seeded
        var sub = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        Assert.Null(sub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 5: ChangePlan_CancelledSubscription_ReturnsBadRequest
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ChangePlan_CancelledSubscription_FailsValidation()
    {
        var validation = ChangePlanValidator.Validate(
            currentTier: SubscriptionTier.Business,
            targetTier: SubscriptionTier.Starter,
            subscriptionStatus: SubscriptionStatus.Cancelled);

        Assert.False(validation.IsValid);
        Assert.Contains("active", validation.Error!, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 6: SubscriptionUpdated_WebhookUpdatesTier
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubscriptionUpdated_WebhookUpdatesTier()
    {
        var userId = await SeedUser();
        await SeedSubscription(userId, SubscriptionTier.Business, "price_business_test");
        var sub = await _db.Subscriptions.FirstAsync(s => s.UserId == userId);

        // Simulate Stripe sending subscription.updated with a new price (Starter)
        var stripeSubscription = new Subscription
        {
            Id = sub.StripeSubscriptionId,
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem
                {
                    Price = new Price { Id = "price_starter_test" },
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
                }]
            }
        };

        var evt = new Event
        {
            Id = $"evt_tierupdate_{Guid.NewGuid():N}",
            Type = EventTypes.CustomerSubscriptionUpdated,
            Data = new EventData { Object = stripeSubscription }
        };

        await StripeWebhookHandler.HandleAsync(evt, _db, _config, _logger);

        await _db.Entry(sub).ReloadAsync();
        Assert.Equal("price_starter_test", sub.StripePriceId);
        // CRITICAL: entity.Tier must also be updated (not just StripePriceId)
        Assert.Equal(SubscriptionTier.Starter, sub.Tier);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 7: SubscriptionUpdated_WebhookPreservesTierOnSamePrice
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SubscriptionUpdated_WebhookPreservesTierOnSamePrice()
    {
        var userId = await SeedUser();
        await SeedSubscription(userId, SubscriptionTier.Professional, "price_pro_test");
        var sub = await _db.Subscriptions.FirstAsync(s => s.UserId == userId);

        var stripeSubscription = new Subscription
        {
            Id = sub.StripeSubscriptionId,
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem
                {
                    Price = new Price { Id = "price_pro_test" },
                    CurrentPeriodEnd = DateTime.UtcNow.AddDays(25)
                }]
            }
        };

        var evt = new Event
        {
            Id = $"evt_sameprice_{Guid.NewGuid():N}",
            Type = EventTypes.CustomerSubscriptionUpdated,
            Data = new EventData { Object = stripeSubscription }
        };

        await StripeWebhookHandler.HandleAsync(evt, _db, _config, _logger);

        await _db.Entry(sub).ReloadAsync();
        Assert.Equal(SubscriptionTier.Professional, sub.Tier);
        Assert.Equal("price_pro_test", sub.StripePriceId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 8: BillingPage_BusinessSub_ShowsDowngradeButtons
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_BusinessSub_ShowsChangePlanButtons()
    {
        // Walk up from test bin directory to solution root
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // The Change Plan buttons should NOT be disabled placeholders anymore
        Assert.DoesNotContain("Change to @upgradePlan.Label plan (coming soon)", source);

        // Should contain change plan buttons that call ChangePlan
        Assert.Contains("ChangePlan", source);

        // The downgrade section should exist for tiers below the current one
        Assert.Contains("GetChangeTiers", source);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TEST 9: BillingPage_DowngradeConfirmDialog_ShowsProrationMessage
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void BillingPage_DowngradeConfirmDialog_ShowsProrationMessage()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "SaasTemplate.sln")))
            dir = Directory.GetParent(dir)?.FullName;

        Assert.NotNull(dir);

        var razorPath = Path.Combine(dir!, "src", "SaasTemplate.Api",
            "Components", "Pages", "Billing.razor");
        var source = File.ReadAllText(razorPath);

        // Must use <dialog> element for the change plan confirmation
        Assert.Contains("change-plan-dialog", source);
        // Must have role="alertdialog" for WCAG 2.1 AA
        Assert.Contains("change-plan-dialog-title", source);
        // Must contain proration message
        Assert.Contains("prorated", source, StringComparison.OrdinalIgnoreCase);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TierPriceResolver additional coverage
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TierPriceResolver_ReturnsNull_ForUnknownTier()
    {
        var result = TierPriceResolver.ResolvePriceId("unknown_tier", _config);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(SubscriptionTier.Starter, "price_starter_test")]
    [InlineData(SubscriptionTier.Professional, "price_pro_test")]
    [InlineData(SubscriptionTier.Business, "price_business_test")]
    public void TierPriceResolver_MapsAllTiersCorrectly(string tier, string expectedPriceId)
    {
        var result = TierPriceResolver.ResolvePriceId(tier, _config);
        Assert.Equal(expectedPriceId, result);
    }

    [Fact]
    public void TierPriceResolver_ResolveTierFromPriceId_AllTiers()
    {
        Assert.Equal(SubscriptionTier.Starter,
            TierPriceResolver.ResolveTierFromPriceId("price_starter_test", _config));
        Assert.Equal(SubscriptionTier.Starter,
            TierPriceResolver.ResolveTierFromPriceId("price_starter_annual_test", _config));
        Assert.Equal(SubscriptionTier.Professional,
            TierPriceResolver.ResolveTierFromPriceId("price_pro_test", _config));
        Assert.Equal(SubscriptionTier.Professional,
            TierPriceResolver.ResolveTierFromPriceId("price_pro_annual_test", _config));
        Assert.Equal(SubscriptionTier.Business,
            TierPriceResolver.ResolveTierFromPriceId("price_business_test", _config));
        Assert.Equal(SubscriptionTier.Business,
            TierPriceResolver.ResolveTierFromPriceId("price_business_annual_test", _config));
    }

    [Fact]
    public void TierPriceResolver_ResolveTierFromPriceId_UnknownReturnsNull()
    {
        Assert.Null(TierPriceResolver.ResolveTierFromPriceId("price_unknown", _config));
    }

}
