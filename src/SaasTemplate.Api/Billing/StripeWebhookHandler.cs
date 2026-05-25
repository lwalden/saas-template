using System.Collections.Concurrent;
using SaasTemplate.Api.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace SaasTemplate.Api.Billing;

public static class StripeWebhookHandler
{
    // In-memory idempotency: track successfully processed event IDs to prevent duplicate handling.
    // This is an optimization, not a correctness requirement — all handlers are naturally idempotent
    // at the DB level (upsert by UserId/StripeSubscriptionId). The cache prevents unnecessary DB queries
    // on rapid Stripe retries. On process restart the cache is empty, which is safe because the
    // handlers' DB operations are idempotent.
    // Process-scoped — assumes single-instance deployment. If the app scales to multiple instances,
    // replace with a distributed cache (Redis/IDistributedCache) or DB-backed processed_events table.
    private static readonly ConcurrentDictionary<string, DateTime> _processedEvents = new();
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    private static long _lastCleanupTicks = DateTime.UtcNow.Ticks;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);

    public static async Task HandleAsync(
        Event stripeEvent,
        AppDbContext db,
        IConfiguration config,
        ILogger logger,
        Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>? userManager = null,
        Email.IEmailService? emailService = null,
        AppSettings? appSettings = null)
    {
        CleanupExpiredEvents();

        // Atomic idempotency gate: TryAdd returns false if the key already exists.
        // If we've already successfully processed this event, skip it.
        if (!_processedEvents.TryAdd(stripeEvent.Id, DateTime.UtcNow))
        {
            logger.LogInformation("Stripe webhook: skipping duplicate event {EventId} ({EventType})",
                stripeEvent.Id, stripeEvent.Type);
            return;
        }

        try
        {
            switch (stripeEvent.Type)
            {
                case EventTypes.CheckoutSessionCompleted:
                    await HandleCheckoutCompleted(stripeEvent, db, config, logger, userManager, emailService, appSettings);
                    break;

                case EventTypes.CustomerSubscriptionUpdated:
                    await HandleSubscriptionUpdated(stripeEvent, db, config, logger);
                    break;

                case EventTypes.CustomerSubscriptionDeleted:
                    await HandleSubscriptionDeleted(stripeEvent, db, logger);
                    break;

                case EventTypes.InvoicePaymentFailed:
                    await HandlePaymentFailed(stripeEvent, db, logger, emailService, appSettings);
                    break;

                default:
                    logger.LogDebug("Stripe webhook: unhandled event type {EventType}", stripeEvent.Type);
                    break;
            }
        }
        catch
        {
            // Remove from cache on failure so Stripe retries can reprocess
            _processedEvents.TryRemove(stripeEvent.Id, out _);
            throw;
        }
    }

    private static async Task HandleCheckoutCompleted(
        Event stripeEvent,
        AppDbContext db,
        IConfiguration config,
        ILogger logger,
        Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>? userManager,
        Email.IEmailService? emailService,
        AppSettings? appSettings)
    {
        if (stripeEvent.Data.Object is not Stripe.Checkout.Session session)
        {
            logger.LogWarning("Stripe webhook: checkout.session.completed missing session object");
            return;
        }

        var userId = session.Metadata?.GetValueOrDefault("userId")
                     ?? session.ClientReferenceId;

        // Anonymous checkout (public-checkout flow): no userId in metadata.
        // Auto-create account from Stripe customer email.
        if (string.IsNullOrWhiteSpace(userId))
        {
            var email = session.CustomerEmail
                ?? session.CustomerDetails?.Email;

            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("Stripe webhook: checkout.session.completed has no userId and no customer email — cannot create account");
                return;
            }

            if (userManager is null)
            {
                logger.LogWarning("Stripe webhook: anonymous checkout requires UserManager — missing dependency");
                return;
            }

            // Find or create user by email
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                userId = existingUser.Id;
                // Record ToS acceptance if not already set (Stripe collected consent)
                if (existingUser.TosAcceptedAt is null)
                {
                    existingUser.TosAcceptedAt = DateTime.UtcNow;
                    await userManager.UpdateAsync(existingUser);
                }
                logger.LogInformation("Stripe webhook: anonymous checkout — linked to existing user {UserId} ({Email})", userId, email);
            }
            else
            {
                var newUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    TosAcceptedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(newUser);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Stripe webhook: failed to create account for {Email}: {Errors}",
                        email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                userId = newUser.Id;
                logger.LogInformation("Stripe webhook: anonymous checkout — created account {UserId} for {Email}", userId, email);

                // Send welcome magic link email
                if (emailService is not null && appSettings is not null)
                {
                    try
                    {
                        var token = await userManager.GenerateUserTokenAsync(newUser, "Default", "magic-link");
                        var magicLinkUrl = $"{appSettings.BaseUrl}/auth/verify?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
                        await emailService.SendMagicLinkAsync(email, magicLinkUrl);
                        logger.LogInformation("Stripe webhook: welcome magic link sent to {Email}", email);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Stripe webhook: failed to send welcome email to {Email}", email);
                        // Don't fail the webhook — account and subscription are created
                    }
                }
            }
        }

        // Fetch full subscription details from Stripe
        var subscriptionService = new SubscriptionService();
        var stripeSubscription = await subscriptionService.GetAsync(session.SubscriptionId);

        var priceId = stripeSubscription.Items.Data.FirstOrDefault()?.Price.Id;
        var tier = ResolveTier(priceId, config, logger);

        var existing = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);

        if (existing is not null)
        {
            existing.StripeCustomerId = session.CustomerId;
            existing.StripeSubscriptionId = stripeSubscription.Id;
            existing.StripePriceId = priceId ?? "";
            existing.Tier = tier;
            existing.Status = SubscriptionStatus.Active;
            existing.CurrentPeriodEnd = GetCurrentPeriodEnd(stripeSubscription);
            existing.CancelledAt = null;
        }
        else
        {
            db.Subscriptions.Add(new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                StripeCustomerId = session.CustomerId,
                StripeSubscriptionId = stripeSubscription.Id,
                StripePriceId = priceId ?? "",
                Tier = tier,
                Status = SubscriptionStatus.Active,
                CurrentPeriodEnd = GetCurrentPeriodEnd(stripeSubscription)
            });
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Subscription created/updated for user {UserId}, tier {Tier}", userId, tier);
    }

    private static async Task HandleSubscriptionUpdated(
        Event stripeEvent,
        AppDbContext db,
        IConfiguration config,
        ILogger logger)
    {
        if (stripeEvent.Data.Object is not Subscription stripeSubscription) return;

        var entity = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (entity is null)
        {
            logger.LogWarning("Stripe webhook: subscription.updated — no local record for {SubId}", stripeSubscription.Id);
            return;
        }

        entity.Status = MapStripeStatus(stripeSubscription.Status);
        entity.CurrentPeriodEnd = GetCurrentPeriodEnd(stripeSubscription);

        var priceId = stripeSubscription.Items.Data.FirstOrDefault()?.Price.Id;
        if (priceId is not null)
        {
            entity.StripePriceId = priceId;

            // S31-003 fix: resolve tier from the new price ID so downgrades/upgrades
            // done via Stripe portal or the change-plan endpoint update the local tier.
            var resolvedTier = TierPriceResolver.ResolveTierFromPriceId(priceId, config);
            if (resolvedTier is not null)
            {
                if (entity.Tier != resolvedTier)
                {
                    logger.LogInformation(
                        "Subscription {SubId} tier changed: {OldTier} → {NewTier} (price {PriceId})",
                        stripeSubscription.Id, entity.Tier, resolvedTier, priceId);
                }
                entity.Tier = resolvedTier;
            }
            else
            {
                logger.LogWarning(
                    "Subscription {SubId}: unrecognised price {PriceId} — tier not updated",
                    stripeSubscription.Id, priceId);
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Subscription {SubId} updated: status={Status}, tier={Tier}",
            stripeSubscription.Id, entity.Status, entity.Tier);
    }

    private static async Task HandleSubscriptionDeleted(
        Event stripeEvent,
        AppDbContext db,
        ILogger logger)
    {
        if (stripeEvent.Data.Object is not Subscription stripeSubscription) return;

        var entity = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscription.Id);

        if (entity is null) return;

        entity.Status = SubscriptionStatus.Cancelled;
        entity.CancelledAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        logger.LogInformation("Subscription {SubId} cancelled", stripeSubscription.Id);
    }

    private static async Task HandlePaymentFailed(
        Event stripeEvent,
        AppDbContext db,
        ILogger logger,
        Email.IEmailService? emailService,
        AppSettings? appSettings)
    {
        if (stripeEvent.Data.Object is not Invoice invoice) return;

        var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId))
        {
            logger.LogWarning("Stripe webhook: payment_failed event {EventId} has no subscription ID — skipping", stripeEvent.Id);
            return;
        }

        var entity = await db.Subscriptions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId);

        if (entity is null) return;

        entity.Status = SubscriptionStatus.PastDue;
        await db.SaveChangesAsync();
        logger.LogWarning("Subscription {SubId} payment failed — marked past_due", subscriptionId);

        // Send dunning email
        if (emailService is null || appSettings is null || entity.User?.Email is null) return;

        try
        {
            var billingPortalUrl = $"{appSettings.BaseUrl}/billing";
            await emailService.SendPaymentFailedAsync(entity.User.Email, billingPortalUrl);
            logger.LogInformation("Dunning email sent to {Email} for subscription {SubId}",
                entity.User.Email, subscriptionId);
        }
        catch (Exception ex)
        {
            // Don't fail the webhook handler — subscription is already marked past_due
            logger.LogError(ex, "Failed to send dunning email for subscription {SubId}", subscriptionId);
        }
    }

    private static string ResolveTier(string? priceId, IConfiguration config, ILogger logger)
    {
        if (priceId == config["STRIPE_PRICE_ID_STARTER"] || priceId == config["STRIPE_PRICE_ID_STARTER_ANNUAL"])
            return SubscriptionTier.Starter;
        if (priceId == config["STRIPE_PRICE_ID_PRO"] || priceId == config["STRIPE_PRICE_ID_PRO_ANNUAL"])
            return SubscriptionTier.Professional;
        if (priceId == config["STRIPE_PRICE_ID_BUSINESS"] || priceId == config["STRIPE_PRICE_ID_BUSINESS_ANNUAL"])
            return SubscriptionTier.Business;

        logger.LogWarning("Stripe webhook: unrecognised price ID {PriceId} — defaulting to starter", priceId);
        return SubscriptionTier.Starter;
    }

    // Stripe SDK 50+: CurrentPeriodEnd moved from Subscription to SubscriptionItem
    private static DateTime GetCurrentPeriodEnd(Subscription sub) =>
        sub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd
        ?? throw new InvalidOperationException($"Subscription {sub.Id} has no items — cannot determine CurrentPeriodEnd");

    private static string MapStripeStatus(string stripeStatus) => stripeStatus switch
    {
        "active" => SubscriptionStatus.Active,
        "trialing" => SubscriptionStatus.Trialing,
        "past_due" => SubscriptionStatus.PastDue,
        "canceled" or "cancelled" => SubscriptionStatus.Cancelled,
        _ => SubscriptionStatus.Active
    };

    private static void CleanupExpiredEvents()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        var lastTicks = Interlocked.Read(ref _lastCleanupTicks);
        if (nowTicks - lastTicks < CleanupInterval.Ticks) return;

        // Atomic compare-and-swap: only one thread wins the race to run cleanup
        if (Interlocked.CompareExchange(ref _lastCleanupTicks, nowTicks, lastTicks) != lastTicks) return;

        var cutoff = DateTime.UtcNow - IdempotencyTtl;
        foreach (var kvp in _processedEvents)
        {
            if (kvp.Value < cutoff)
                _processedEvents.TryRemove(kvp.Key, out _);
        }
    }

    /// <summary>Clears the idempotency cache and resets cleanup timer. For testing only.</summary>
    public static void ResetIdempotencyCache()
    {
        _processedEvents.Clear();
        Interlocked.Exchange(ref _lastCleanupTicks, DateTime.UtcNow.Ticks);
    }
}
