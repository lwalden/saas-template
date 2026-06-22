using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;
using SaasTemplate.Api.Monitoring;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace SaasTemplate.Api.Tests.Monitoring;

public class OnboardingEmailServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ServiceProvider _provider;
    private readonly FakeEmailService _emailService;
    private readonly OnboardingEmailService _service;

    public OnboardingEmailServiceTests()
    {
        var connStr = $"Data Source=onboard-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(connStr);
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connStr)
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _emailService = new FakeEmailService();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlite(connStr)
               .ConfigureWarnings(w => w.Ignore(
                   Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        services.AddSingleton<IEmailService>(_emailService);

        _provider = services.BuildServiceProvider();
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();

        _service = new OnboardingEmailService(scopeFactory, Mock.Of<ILogger<OnboardingEmailService>>());
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
        _connection.Dispose();
    }

    private string SeedUser(string id, bool marketingConsent = true)
    {
        _db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = $"user-{id}",
            NormalizedUserName = $"USER-{id}",
            Email = $"{id}@test.com",
            NormalizedEmail = $"{id}@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            MarketingConsent = marketingConsent
        });
        _db.SaveChanges();
        return id;
    }

    private SubscriptionEntity SeedSubscription(string userId, DateTime createdAt, int onboardingStage = 0, string status = "active")
    {
        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StripeCustomerId = $"cus_{Guid.NewGuid():N}",
            StripeSubscriptionId = $"sub_{Guid.NewGuid():N}",
            StripePriceId = "price_starter",
            Tier = "Starter",
            Status = status,
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(30),
            CreatedAt = createdAt,
            OnboardingStage = onboardingStage
        };
        _db.Subscriptions.Add(sub);
        _db.SaveChanges();
        return sub;
    }

    private async Task RunOneCycle()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await _service.StartAsync(cts.Token); await Task.Delay(1500, cts.Token); }
        catch (OperationCanceledException) { }
        await _service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task New_subscription_receives_welcome_email_immediately()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddMinutes(-5));

        await RunOneCycle();

        Assert.Single(_emailService.Sent);
        Assert.Equal(1, _emailService.Sent[0].Stage);
        Assert.Contains("@test.com", _emailService.Sent[0].Email);
    }

    [Fact]
    public async Task Stage_2_sends_after_3_days()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddDays(-4), onboardingStage: 1);

        await RunOneCycle();

        Assert.Single(_emailService.Sent);
        Assert.Equal(2, _emailService.Sent[0].Stage);
    }

    [Fact]
    public async Task Stage_2_does_not_send_before_3_days()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddDays(-2), onboardingStage: 1);

        await RunOneCycle();

        Assert.Empty(_emailService.Sent);
    }

    [Fact]
    public async Task Stage_3_sends_after_7_days()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddDays(-8), onboardingStage: 2);

        await RunOneCycle();

        Assert.Single(_emailService.Sent);
        Assert.Equal(3, _emailService.Sent[0].Stage);
    }

    [Fact]
    public async Task Stage_3_does_not_send_before_7_days()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddDays(-5), onboardingStage: 2);

        await RunOneCycle();

        Assert.Empty(_emailService.Sent);
    }

    [Fact]
    public async Task Completed_onboarding_receives_no_more_emails()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddDays(-30), onboardingStage: 3);

        await RunOneCycle();

        Assert.Empty(_emailService.Sent);
    }

    [Fact]
    public async Task Cancelled_subscription_receives_no_emails()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        SeedSubscription(userId, DateTime.UtcNow.AddMinutes(-5), status: "cancelled");

        await RunOneCycle();

        Assert.Empty(_emailService.Sent);
    }

    [Fact]
    public async Task Email_failure_does_not_advance_stage()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        var sub = SeedSubscription(userId, DateTime.UtcNow.AddMinutes(-5));
        _emailService.ShouldThrow = true;

        await RunOneCycle();

        // Reload from DB to check stage was not advanced
        await _db.Entry(sub).ReloadAsync();
        Assert.Equal(0, sub.OnboardingStage);
    }

    [Fact]
    public async Task OnboardingStage_persists_to_database_after_send()
    {
        var userId = SeedUser(Guid.NewGuid().ToString());
        var sub = SeedSubscription(userId, DateTime.UtcNow.AddMinutes(-5));

        await RunOneCycle();

        await _db.Entry(sub).ReloadAsync();
        Assert.Equal(1, sub.OnboardingStage);
    }

    /// <summary>Fake email service that records sent onboarding emails.</summary>
    private sealed class FakeEmailService : IEmailService
    {
        public List<(string Email, int Stage, string Tier)> Sent { get; } = [];
        public bool ShouldThrow { get; set; }

        public Task SendOnboardingEmailAsync(string toEmail, int stage, string tier, CancellationToken ct = default)
        {
            if (ShouldThrow) throw new InvalidOperationException("Simulated email failure");
            Sent.Add((toEmail, stage, tier));
            return Task.CompletedTask;
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendMagicLinkAsync(string toEmail, string magicLinkUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string toEmail, string verifyUrl, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendWelcomeEmailAsync(string toEmail, string tier, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPaymentFailedAsync(string toEmail, string billingPortalUrl, CancellationToken ct = default) => Task.CompletedTask;
    }
}
