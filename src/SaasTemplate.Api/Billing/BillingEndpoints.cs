using System.Security.Claims;
using System.Text;
using SaasTemplate.Api.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace SaasTemplate.Api.Billing;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        // Unauthenticated webhook — mapped outside CORS, before UseCors()
        app.MapPost("/api/billing/webhook", HandleStripeWebhook);

        // Public (unauthenticated) checkout — for new customers who haven't signed in yet
        app.MapPost("/api/billing/public-checkout", HandlePublicCheckout)
            .RequireRateLimiting("public-checkout");

        var billing = app.MapGroup("/api/billing").RequireAuthorization();
        billing.MapPost("/checkout", HandleCheckout);
        billing.MapPost("/portal", HandlePortalSession);
        billing.MapGet("/subscription", HandleGetSubscription);
        billing.MapPost("/accept-tos", HandleAcceptTos);
        billing.MapPost("/change-plan", HandleChangePlan);


        return app;
    }

    private static async Task<IResult> HandleAcceptTos(
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var dbUser = await db.Users.FindAsync(userId);
        if (dbUser is null)
            return Results.NotFound(new { error = "User not found." });

        // Idempotent: don't overwrite the original acceptance timestamp
        if (dbUser.TosAcceptedAt is null)
        {
            dbUser.TosAcceptedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { acceptedAt = dbUser.TosAcceptedAt.Value.ToString("o") });
    }

    private static async Task<IResult> HandlePublicCheckout(
        PublicCheckoutRequest request,
        IConfiguration config,
        AppSettings appSettings,
        ILogger<Program> logger)
    {
        // Validate email
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254)
            return Results.BadRequest(new { error = "A valid email address is required." });

        try
        {
            var addr = new System.Net.Mail.MailAddress(request.Email);
            if (addr.Address != request.Email.Trim())
                return Results.BadRequest(new { error = "A valid email address is required." });
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { error = "A valid email address is required." });
        }

        var email = request.Email.Trim().ToLowerInvariant();

        // Resolve price ID
        var isAnnual = string.Equals(request.Interval, "annual", StringComparison.OrdinalIgnoreCase);
        var priceId = (request.Tier, isAnnual) switch
        {
            ("starter", false)      => config["STRIPE_PRICE_ID_STARTER"],
            ("starter", true)       => config["STRIPE_PRICE_ID_STARTER_ANNUAL"],
            ("professional", false) => config["STRIPE_PRICE_ID_PRO"],
            ("professional", true)  => config["STRIPE_PRICE_ID_PRO_ANNUAL"],
            ("business", false)     => config["STRIPE_PRICE_ID_BUSINESS"],
            ("business", true)      => config["STRIPE_PRICE_ID_BUSINESS_ANNUAL"],
            _ => null
        };

        if (priceId is null)
            return Results.BadRequest(new { error = "Invalid tier. Accepted values: starter, professional, business." });

        try
        {
            // Check if this email already has an active Stripe subscription
            string? existingCustomerId = null;
            var customers = await new CustomerService().ListAsync(
                new CustomerListOptions { Email = email, Limit = 1 });
            var customer = customers.Data.FirstOrDefault();

            if (customer is not null)
            {
                existingCustomerId = customer.Id;

                // Check for active subscriptions
                var subs = await new SubscriptionService().ListAsync(
                    new SubscriptionListOptions { Customer = customer.Id, Status = "active", Limit = 1 });

                if (subs.Data.Count > 0)
                {
                    return Results.Ok(new
                    {
                        action = "login",
                        message = "You already have an active subscription. Sign in to manage your plan."
                    });
                }
            }

            var sessionOptions = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                ],
                SuccessUrl = $"{appSettings.FrontendUrl}/checkout/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{appSettings.BaseUrl}/pricing",
                ConsentCollection = new SessionConsentCollectionOptions
                {
                    TermsOfService = "required"
                },
                Metadata = new Dictionary<string, string>
                {
                    ["tier"] = request.Tier,
                    ["source"] = "public-checkout"
                }
            };

            // Reuse existing Stripe customer or set email for new customer
            if (existingCustomerId is not null)
                sessionOptions.Customer = existingCustomerId;
            else
                sessionOptions.CustomerEmail = email;

            var service = new SessionService();
            var session = await service.CreateAsync(sessionOptions);

            logger.LogInformation("Public checkout session created for {Email}, tier {Tier}", email, request.Tier);
            return Results.Ok(new { url = session.Url });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Public checkout session creation failed for {Email}", email);
            return Results.Problem("Failed to create checkout session.", statusCode: 502);
        }
    }

    private static async Task<IResult> HandleCheckout(
        CheckoutRequest request,
        ClaimsPrincipal user,
        IConfiguration config,
        AppSettings appSettings,
        AppDbContext db,
        ILogger<Program> logger)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);

        // Require ToS acceptance before allowing checkout
        var dbUser = await db.Users.FindAsync(userId);
        if (dbUser?.TosAcceptedAt is null)
            return Results.BadRequest(new { error = "You must accept the Terms of Service before subscribing." });

        var isAnnual = string.Equals(request.Interval, "annual", StringComparison.OrdinalIgnoreCase);
        var priceId = (request.Tier, isAnnual) switch
        {
            ("starter", false)      => config["STRIPE_PRICE_ID_STARTER"],
            ("starter", true)       => config["STRIPE_PRICE_ID_STARTER_ANNUAL"],
            ("professional", false) => config["STRIPE_PRICE_ID_PRO"],
            ("professional", true)  => config["STRIPE_PRICE_ID_PRO_ANNUAL"],
            ("business", false)     => config["STRIPE_PRICE_ID_BUSINESS"],
            ("business", true)      => config["STRIPE_PRICE_ID_BUSINESS_ANNUAL"],
            _ => null
        };

        if (priceId is null)
            return Results.BadRequest(new { error = $"Unknown tier: {request.Tier}" });

        try
        {
            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                ],
                CustomerEmail = email,
                ClientReferenceId = userId,
                SuccessUrl = $"{appSettings.BaseUrl}/billing?checkout=success",
                CancelUrl = $"{appSettings.BaseUrl}/billing?checkout=cancelled",
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId ?? "",
                    ["tier"] = request.Tier
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            logger.LogInformation("Stripe checkout session created for user {UserId}, tier {Tier}", userId, request.Tier);
            return Results.Ok(new { url = session.Url });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe checkout session creation failed for user {UserId}", userId);
            return Results.Problem("Failed to create checkout session.", statusCode: 502);
        }
    }

    private static async Task<IResult> HandlePortalSession(
        ClaimsPrincipal user,
        AppDbContext db,
        AppSettings appSettings,
        ILogger<Program> logger)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);

        // Try local DB first
        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        var customerId = subscription?.StripeCustomerId;

        // Fallback: look up customer in Stripe by email if DB record is missing
        // (handles the case where webhook hasn't delivered yet)
        if (customerId is null && !string.IsNullOrEmpty(email))
        {
            try
            {
                var customers = await new CustomerService().ListAsync(
                    new CustomerListOptions { Email = email, Limit = 1 });
                customerId = customers.Data.FirstOrDefault()?.Id;
            }
            catch (StripeException ex)
            {
                logger.LogWarning(ex, "Stripe customer lookup by email failed for {UserId}", userId);
            }
        }

        if (customerId is null)
            return Results.NotFound(new { error = "No subscription found. Subscribe to a plan first." });

        try
        {
            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = customerId,
                ReturnUrl = $"{appSettings.BaseUrl}/billing"
            };

            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options);

            return Results.Ok(new { url = session.Url });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe billing portal session failed for user {UserId}", userId);
            return Results.Problem("Failed to create billing portal session.", statusCode: 502);
        }
    }

    private static async Task<IResult> HandleGetSubscription(
        ClaimsPrincipal user,
        AppDbContext db,
        IConfiguration config,
        ILogger<Program> logger)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (subscription is not null)
        {
            var usage = await GetUsageAsync(db, userId!, subscription);
            return Results.Ok(new
            {
                tier = subscription.Tier,
                status = subscription.Status,
                currentPeriodEnd = subscription.CurrentPeriodEnd,
                stripeCustomerId = subscription.StripeCustomerId,
                stripeSubscriptionId = subscription.StripeSubscriptionId,
                quotaUsed = usage.Used,
                quotaLimit = usage.Limit,
                periodStart = usage.PeriodStart
            });
        }

        // Fallback: check Stripe directly for an active subscription by email.
        // This covers the window between checkout completion and webhook delivery.
        if (!string.IsNullOrEmpty(email))
        {
            try
            {
                var customers = await new CustomerService().ListAsync(
                    new CustomerListOptions { Email = email, Limit = 1 });
                var customer = customers.Data.FirstOrDefault();
                if (customer is not null)
                {
                    var subs = await new SubscriptionService().ListAsync(
                        new SubscriptionListOptions { Customer = customer.Id, Limit = 1 });
                    var stripeSub = subs.Data.FirstOrDefault();
                    if (stripeSub is not null && stripeSub.Status is "active" or "trialing")
                    {
                        // Determine tier from price ID
                        var priceId = stripeSub.Items.Data.FirstOrDefault()?.Price?.Id;
                        var tier = priceId switch
                        {
                            var p when p == config["STRIPE_PRICE_ID_STARTER"] => "starter",
                            var p when p == config["STRIPE_PRICE_ID_PRO"] => "professional",
                            var p when p == config["STRIPE_PRICE_ID_BUSINESS"] => "business",
                            _ => "unknown"
                        };

                        // Backfill the DB record so future requests don't need Stripe API
                        db.Subscriptions.Add(new SubscriptionEntity
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId!,
                            StripeCustomerId = customer.Id,
                            StripeSubscriptionId = stripeSub.Id,
                            StripePriceId = priceId ?? "",
                            Tier = tier,
                            Status = stripeSub.Status,
                            CurrentPeriodEnd = stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd ?? throw new InvalidOperationException($"Subscription {stripeSub.Id} has no items"),
                            CreatedAt = DateTime.UtcNow
                        });
                        await db.SaveChangesAsync();
                        logger.LogInformation("Backfilled subscription for user {UserId} from Stripe (webhook may have been delayed)", userId);

                        // Reload the subscription we just inserted for usage calculation
                        var backfilledSub = await db.Subscriptions
                            .FirstOrDefaultAsync(s => s.UserId == userId);
                        var usage = await GetUsageAsync(db, userId!, backfilledSub!);

                        return Results.Ok(new
                        {
                            tier,
                            status = stripeSub.Status,
                            currentPeriodEnd = (DateTime?)(stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd ?? throw new InvalidOperationException($"Subscription {stripeSub.Id} has no items")),
                            stripeCustomerId = customer.Id,
                            stripeSubscriptionId = stripeSub.Id,
                            scansUsed = usage.Used,
                            scansLimit = usage.Limit,
                            periodStart = usage.PeriodStart
                        });
                    }
                }
            }
            catch (StripeException ex)
            {
                logger.LogWarning(ex, "Stripe subscription lookup fallback failed for {UserId}", userId);
            }
        }

        return Results.NotFound(new { error = "No active subscription found." });
    }

    private static Task<(int Used, int Limit, DateTime PeriodStart)> GetUsageAsync(
        AppDbContext db, string userId, SubscriptionEntity subscription)
    {
        var tierConfig = TierLimits.ForTier(subscription.Tier);
        var periodStart = subscription.CurrentPeriodEnd.HasValue
            ? subscription.CurrentPeriodEnd.Value.AddMonths(-1)
            : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // TODO: Replace 0 with your actual usage query for this subscription period.
        // Example: count actions/events/seats for the billing period.
        var used = 0;
        return Task.FromResult((used, tierConfig.MonthlyQuota, periodStart));
    }

    private static async Task<IResult> HandleStripeWebhook(
        HttpContext context,
        AppDbContext db,
        IConfiguration config,
        ILogger<Program> logger,
        Microsoft.AspNetCore.Identity.UserManager<Data.ApplicationUser> userManager,
        Email.IEmailService emailService,
        AppSettings appSettings)
    {
        if (!context.Request.Headers.TryGetValue("Stripe-Signature", out var sigHeader))
        {
            logger.LogWarning("Stripe webhook: missing Stripe-Signature header");
            return Results.BadRequest(new { error = "Missing Stripe-Signature header" });
        }

        // Read raw body for signature validation
        string json;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true))
            json = await reader.ReadToEndAsync();

        var webhookSecret = config["STRIPE_WEBHOOK_SECRET"] ?? "";

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, sigHeader.ToString(), webhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Stripe webhook: invalid signature — {Message}", ex.Message);
            return Results.BadRequest(new { error = "Invalid Stripe signature" });
        }

        logger.LogInformation("Stripe webhook received: {EventType} (id: {EventId})",
            stripeEvent.Type, stripeEvent.Id);

        try
        {
            await StripeWebhookHandler.HandleAsync(stripeEvent, db, config, logger, userManager, emailService, appSettings);
        }
        catch (Exception ex)
        {
            // Return 500 so Stripe retries — handler only caches successful events, so retries reprocess
            logger.LogError(ex, "Stripe webhook handler failed for {EventType} (id: {EventId})",
                stripeEvent.Type, stripeEvent.Id);
            return Results.StatusCode(500);
        }

        return Results.Ok();
    }

    private static async Task<IResult> HandleChangePlan(
        ChangePlanRequest request,
        ClaimsPrincipal user,
        IConfiguration config,
        AppDbContext db,
        ILogger<Program> logger)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (subscription is null)
            return Results.NotFound(new { error = "No subscription found." });

        var validation = ChangePlanValidator.Validate(
            subscription.Tier, request.Tier, subscription.Status);

        if (!validation.IsValid)
            return Results.BadRequest(new { error = validation.Error });

        var targetPriceId = TierPriceResolver.ResolvePriceId(request.Tier, config);
        if (targetPriceId is null)
            return Results.BadRequest(new { error = $"Unknown tier: {request.Tier}" });

        try
        {
            var subscriptionService = new SubscriptionService();
            var stripeSubscription = await subscriptionService.GetAsync(subscription.StripeSubscriptionId);
            var currentItemId = stripeSubscription.Items.Data.FirstOrDefault()?.Id;

            if (currentItemId is null)
            {
                logger.LogError("Subscription {SubId} has no items — cannot change plan", subscription.StripeSubscriptionId);
                return Results.Problem("Subscription has no active items.", statusCode: 502);
            }

            await subscriptionService.UpdateAsync(subscription.StripeSubscriptionId,
                new SubscriptionUpdateOptions
                {
                    Items =
                    [
                        new SubscriptionItemOptions
                        {
                            Id = currentItemId,
                            Price = targetPriceId
                        }
                    ],
                    ProrationBehavior = "create_prorations"
                });

            logger.LogInformation(
                "Plan changed for user {UserId}: {OldTier} → {NewTier}",
                userId, subscription.Tier, request.Tier);

            return Results.Ok(new { message = "Plan changed successfully.", tier = request.Tier });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe plan change failed for user {UserId}", userId);
            return Results.Problem("Failed to change plan.", statusCode: 502);
        }
    }

    private sealed record CheckoutRequest(string Tier, string Interval = "monthly");
    private sealed record PublicCheckoutRequest(string Tier, string Email, string Interval = "monthly");
    private sealed record ChangePlanRequest(string Tier);
}
