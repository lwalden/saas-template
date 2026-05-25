using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;
using Microsoft.EntityFrameworkCore;

namespace SaasTemplate.Api.Monitoring;

/// <summary>
/// Background service that sends onboarding drip emails to new subscribers.
/// Runs hourly. Sends 3 emails: welcome (day 0), education (day 3), retention (day 7).
/// Tracks progress via SubscriptionEntity.OnboardingStage to avoid duplicates.
/// </summary>
public sealed class OnboardingEmailService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OnboardingEmailService> _logger;

    public OnboardingEmailService(IServiceScopeFactory scopeFactory, ILogger<OnboardingEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnboardingEmails(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "OnboardingEmailService: unhandled error during processing");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessOnboardingEmails(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;

        // Find active subscriptions with pending onboarding emails
        var subscriptions = await db.Subscriptions
            .Include(s => s.User)
            .Where(s => s.Status == SubscriptionStatus.Active && s.OnboardingStage < 3)
            .ToListAsync(ct);

        foreach (var sub in subscriptions)
        {
            var age = now - sub.CreatedAt;
            var nextStage = sub.OnboardingStage + 1;

            var shouldSend = nextStage switch
            {
                1 => age >= TimeSpan.Zero,          // welcome: immediately (first run after creation)
                2 => age >= TimeSpan.FromDays(3),    // education: day 3
                3 => age >= TimeSpan.FromDays(7),    // retention: day 7
                _ => false
            };

            if (!shouldSend) continue;

            var email = sub.User?.Email;
            if (string.IsNullOrWhiteSpace(email)) continue;

            // S38-003: onboarding drip emails are marketing — skip if user hasn't consented
            if (!(sub.User?.MarketingConsent ?? false))
            {
                _logger.LogInformation(
                    "Onboarding stage {Stage} skipped for {Email} — no marketing consent",
                    nextStage, email);
                continue;
            }

            try
            {
                await emailService.SendOnboardingEmailAsync(email, nextStage, sub.Tier, ct);
                sub.OnboardingStage = nextStage;
                await db.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Onboarding stage {Stage} sent to {Email} (subscription {SubId}, age {Age}d)",
                    nextStage, email, sub.Id, age.TotalDays);
            }
            catch (Exception ex)
            {
                // Log but don't crash — will retry next hour
                _logger.LogWarning(ex,
                    "Failed to send onboarding stage {Stage} to {Email} — will retry",
                    nextStage, email);
            }
        }
    }
}
