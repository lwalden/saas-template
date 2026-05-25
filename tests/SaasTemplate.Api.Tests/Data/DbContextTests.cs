using SaasTemplate.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SaasTemplate.Api.Tests.Data;

public class DbContextTests : IDisposable
{
    private readonly AppDbContext _db;

    public DbContextTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Can_create_and_retrieve_subscription()
    {
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"test-{userId}@example.com",
            Email = $"test-{userId}@example.com",
            NormalizedUserName = $"TEST-{userId}@EXAMPLE.COM",
            NormalizedEmail = $"TEST-{userId}@EXAMPLE.COM"
        };
        _db.Users.Add(user);

        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StripeCustomerId = "cus_test123",
            StripeSubscriptionId = "sub_test123",
            StripePriceId = "price_starter",
            Tier = SubscriptionTier.Starter,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow
        };
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        var loaded = await _db.Subscriptions.FindAsync(subscription.Id);
        Assert.NotNull(loaded);
        Assert.Equal("cus_test123", loaded.StripeCustomerId);
        Assert.Equal("sub_test123", loaded.StripeSubscriptionId);
        Assert.Equal(SubscriptionTier.Starter, loaded.Tier);
        Assert.Equal(SubscriptionStatus.Active, loaded.Status);
        Assert.NotNull(loaded.CurrentPeriodEnd);
        Assert.Null(loaded.CancelledAt);
    }

    [Fact]
    public async Task StripeSubscriptionId_must_be_unique()
    {
        var userId1 = Guid.NewGuid().ToString();
        var userId2 = Guid.NewGuid().ToString();

        _db.Users.Add(new ApplicationUser
        {
            Id = userId1, UserName = "u1@example.com", Email = "u1@example.com",
            NormalizedUserName = "U1@EXAMPLE.COM", NormalizedEmail = "U1@EXAMPLE.COM"
        });
        _db.Users.Add(new ApplicationUser
        {
            Id = userId2, UserName = "u2@example.com", Email = "u2@example.com",
            NormalizedUserName = "U2@EXAMPLE.COM", NormalizedEmail = "U2@EXAMPLE.COM"
        });

        _db.Subscriptions.Add(new SubscriptionEntity
        {
            Id = Guid.NewGuid(), UserId = userId1,
            StripeCustomerId = "cus_a", StripeSubscriptionId = "sub_duplicate",
            StripePriceId = "price_starter", Tier = SubscriptionTier.Starter
        });
        _db.Subscriptions.Add(new SubscriptionEntity
        {
            Id = Guid.NewGuid(), UserId = userId2,
            StripeCustomerId = "cus_b", StripeSubscriptionId = "sub_duplicate",
            StripePriceId = "price_starter", Tier = SubscriptionTier.Starter
        });

        await Assert.ThrowsAnyAsync<Exception>(() => _db.SaveChangesAsync());
    }

    public void Dispose() => _db.Dispose();
}
