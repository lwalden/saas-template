using SaasTemplate.Api.Billing;
using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Stripe;
using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

public class DunningEmailTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;
    private readonly FakeEmailService _emailService;

    public DunningEmailTests()
    {
        var connStr = $"Data Source=dunning-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connStr);
        _connection.Open();
        SqliteSchemaHelper.EnsureSchema(connStr);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connStr)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        _db = new AppDbContext(options);

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["STRIPE_PRICE_ID_STARTER"] = "price_starter_test",
                ["APP_BASE_URL"] = "https://test.app",
            })
            .Build();

        _logger = Mock.Of<ILogger>();
        _emailService = new FakeEmailService();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<string> SeedUserWithSubscription(string subscriptionId = "sub_dunning_test")
    {
        var userId = Guid.NewGuid().ToString();
        _db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"dunning-{userId}@test.com",
            NormalizedUserName = $"DUNNING-{userId}@TEST.COM",
            Email = $"dunning-{userId}@test.com",
            NormalizedEmail = $"DUNNING-{userId}@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.Subscriptions.Add(new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StripeCustomerId = "cus_dunning",
            StripeSubscriptionId = subscriptionId,
            StripePriceId = "price_starter_test",
            Tier = SubscriptionTier.Starter,
            Status = SubscriptionStatus.Active,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30)
        });
        await _db.SaveChangesAsync();
        return userId;
    }

    private Event CreatePaymentFailedEvent(string eventId, string subscriptionId)
    {
        var invoice = new Invoice
        {
            Parent = new InvoiceParent
            {
                SubscriptionDetails = new InvoiceParentSubscriptionDetails
                {
                    SubscriptionId = subscriptionId
                }
            }
        };
        return new Event
        {
            Id = eventId,
            Type = EventTypes.InvoicePaymentFailed,
            Data = new EventData { Object = invoice }
        };
    }

    [Fact]
    public async Task PaymentFailed_sends_dunning_email_when_subscription_found()
    {
        await SeedUserWithSubscription();
        var evt = CreatePaymentFailedEvent($"evt_dunning_{Guid.NewGuid():N}", "sub_dunning_test");

        await StripeWebhookHandler.HandleAsync(evt, _db, _config, _logger,
            emailService: _emailService, appSettings: new AppSettings(_config));

        Assert.Single(_emailService.PaymentFailedEmails);
        Assert.Contains("dunning-", _emailService.PaymentFailedEmails[0].Email);
    }

    [Fact]
    public async Task PaymentFailed_does_not_send_email_when_subscription_not_found()
    {
        var evt = CreatePaymentFailedEvent($"evt_dunning_{Guid.NewGuid():N}", "sub_nonexistent");

        await StripeWebhookHandler.HandleAsync(evt, _db, _config, _logger,
            emailService: _emailService, appSettings: new AppSettings(_config));

        Assert.Empty(_emailService.PaymentFailedEmails);
    }

    [Fact]
    public async Task PaymentFailed_marks_subscription_past_due()
    {
        await SeedUserWithSubscription();
        var evt = CreatePaymentFailedEvent($"evt_dunning_{Guid.NewGuid():N}", "sub_dunning_test");

        await StripeWebhookHandler.HandleAsync(evt, _db, _config, _logger,
            emailService: _emailService, appSettings: new AppSettings(_config));

        var sub = await _db.Subscriptions.FirstAsync(s => s.StripeSubscriptionId == "sub_dunning_test");
        Assert.Equal(SubscriptionStatus.PastDue, sub.Status);
    }

    private class FakeEmailService : IEmailService
    {
        public List<(string Email, string BillingPortalUrl)> PaymentFailedEmails { get; } = [];

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendMagicLinkAsync(string toEmail, string magicLinkUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendWelcomeEmailAsync(string toEmail, string tier, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendOnboardingEmailAsync(string toEmail, int stage, string tier, CancellationToken ct = default) => Task.CompletedTask;

        public Task SendPaymentFailedAsync(string toEmail, string billingPortalUrl, CancellationToken ct = default)
        {
            PaymentFailedEmails.Add((toEmail, billingPortalUrl));
            return Task.CompletedTask;
        }
    }
}
