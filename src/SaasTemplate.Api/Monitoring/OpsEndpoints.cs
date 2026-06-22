using System.Security.Cryptography;
using System.Text;
using SaasTemplate.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace SaasTemplate.Api.Monitoring;

public static class OpsEndpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder app, string apiKey)
    {
        var ops = app.MapGroup("/api/ops");

        ops.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var key = httpContext.Request.Headers["X-Api-Key"].FirstOrDefault() ?? string.Empty;
            if (!ConstantTimeEquals(key, apiKey))
                return Results.Unauthorized();
            return await next(context);
        });

        ops.MapGet("/status", HandleStatus);

        ops.MapPost("/set-tier", async (AppDbContext db, SetTierRequest req, CancellationToken ct) =>
        {
            var validTiers = new[] { "starter", "professional", "business" };
            var tier = req.Tier?.ToLowerInvariant() ?? "";
            if (!validTiers.Contains(tier))
                return Results.BadRequest(new { error = $"Invalid tier. Must be one of: {string.Join(", ", validTiers)}" });

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email, ct);
            if (user is null)
                return Results.NotFound(new { error = $"No user found with email {req.Email}" });

            var sub = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == user.Id, ct);
            if (sub is null)
                return Results.NotFound(new { error = $"No subscription found for {req.Email}" });

            var previousTier = sub.Tier;
            sub.Tier = tier;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { email = req.Email, previousTier, newTier = tier });
        });

        ops.MapGet("/users", async (AppDbContext db, CancellationToken ct) =>
        {
            var users = await db.Users
                .Select(u => new
                {
                    u.Email,
                    u.FullName,
                    u.CreatedAt,
                    u.MarketingConsent,
                    u.TosAcceptedAt,
                    u.EmailConfirmed,
                    u.LockoutEnd,
                    Subscription = u.Subscription == null ? null : new
                    {
                        u.Subscription.Tier,
                        u.Subscription.Status,
                        u.Subscription.CurrentPeriodEnd,
                        u.Subscription.CreatedAt
                    }
                })
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync(ct);

            return Results.Ok(new { count = users.Count, users });
        });

        ops.MapGet("/audit", async (AppDbContext db, int? limit, string? userId, string? action, CancellationToken ct) =>
        {
            var take = Math.Clamp(limit ?? 100, 1, 500);

            var query = db.AuditEvents.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(a => a.UserId == userId);
            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            var events = await query
                .OrderByDescending(a => a.Timestamp)
                .Take(take)
                .Select(a => new
                {
                    a.Id,
                    a.Timestamp,
                    a.UserId,
                    a.Email,
                    a.Action,
                    a.TargetType,
                    a.TargetId,
                    a.IpAddress,
                    a.Metadata
                })
                .ToListAsync(ct);

            return Results.Ok(new { count = events.Count, events });
        });

        ops.MapDelete("/cleanup-test-users", async (AppDbContext db, string pattern, bool confirm, CancellationToken ct) =>
        {
            if (!confirm)
                return Results.BadRequest(new { error = "Pass confirm=true to execute deletion." });

            if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains('@'))
                return Results.BadRequest(new { error = "Pattern must contain '@' (e.g., @example.com)." });

            var users = await db.Users
                .Where(u => u.Email != null && u.Email.EndsWith(pattern))
                .ToListAsync(ct);

            if (users.Count == 0)
                return Results.Ok(new { deleted = 0, message = $"No users matching '{pattern}'." });

            var userIds = users.Select(u => u.Id).ToList();
            var subs = await db.Subscriptions.Where(s => userIds.Contains(s.UserId)).ToListAsync(ct);
            db.Subscriptions.RemoveRange(subs);
            db.Users.RemoveRange(users);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { deleted = users.Count, pattern, emails = users.Select(u => u.Email).ToList() });
        });

        return app;
    }

    private sealed record SetTierRequest(string Email, string Tier);

    private static async Task<IResult> HandleStatus(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dayAgo = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);

        var userCount = await db.Users.CountAsync(ct);
        var activeSubscriptions = await db.Subscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing, ct);
        var pastDueSubscriptions = await db.Subscriptions
            .CountAsync(s => s.Status == SubscriptionStatus.PastDue, ct);

        var registrationsLast24h = await db.Users.CountAsync(u => u.CreatedAt > dayAgo, ct);
        var registrationsLast7d = await db.Users.CountAsync(u => u.CreatedAt > weekAgo, ct);
        var newSubscriptionsLast24h = await db.Subscriptions.CountAsync(s => s.CreatedAt > dayAgo, ct);
        var newSubscriptionsLast7d = await db.Subscriptions.CountAsync(s => s.CreatedAt > weekAgo, ct);

        var tierCounts = await db.Subscriptions
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trialing)
            .GroupBy(s => s.Tier)
            .Select(g => new { Tier = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var mrr = tierCounts.Sum(t => t.Tier switch
        {
            SubscriptionTier.Starter      => t.Count * 49m,
            SubscriptionTier.Professional => t.Count * 99m,
            SubscriptionTier.Business     => t.Count * 149m,
            _                             => 0m
        });

        return Results.Ok(new
        {
            status = "ok",
            timestamp = now,
            users = new { total = userCount, registrationsLast24h, registrationsLast7d },
            subscriptions = new
            {
                active = activeSubscriptions,
                pastDue = pastDueSubscriptions,
                newLast24h = newSubscriptionsLast24h,
                newLast7d = newSubscriptionsLast7d,
                mrr
            }
        });
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
