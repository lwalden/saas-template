using System.Security.Claims;
using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Billing;

/// <summary>
/// Reusable minimal-API endpoint filter that blocks a metered request when the
/// authenticated user has exhausted their per-tier <see cref="TierConfig.MonthlyQuota"/>.
/// Apply with <c>.RequireQuota()</c> on any metered endpoint. Unlimited tier always passes.
/// </summary>
/// <remarks>
/// On block it returns HTTP 402 (Payment Required) with a problem body that includes
/// an "upgrade" CTA, the limit, used, and remaining — so the UI can prompt an upgrade.
/// The filter only CHECKS quota; record the actual usage in the handler via
/// <see cref="IUsageService.RecordUsageAsync"/> once the action succeeds.
/// </remarks>
public sealed class QuotaEnforcementFilter : IEndpointFilter
{
    private readonly string _meter;

    public QuotaEnforcementFilter(string meter = UsageMeter.Default) => _meter = meter;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var usage = http.RequestServices.GetRequiredService<IUsageService>();
        var check = await usage.CheckQuotaAsync(userId, _meter, http.RequestAborted);

        if (!check.Allowed)
        {
            return Results.Json(new
            {
                error = "quota_exceeded",
                message = check.Message,
                action = "upgrade",
                used = check.Used,
                limit = check.Limit,
                remaining = check.Remaining
            }, statusCode: StatusCodes.Status402PaymentRequired);
        }

        return await next(context);
    }
}

/// <summary>Extension to apply <see cref="QuotaEnforcementFilter"/> to an endpoint.</summary>
public static class QuotaEnforcementExtensions
{
    /// <summary>
    /// Blocks the request with 402 + an upgrade CTA when the user is over quota for
    /// <paramref name="meter"/>. Place after <c>.RequireAuthorization()</c>.
    /// </summary>
    public static TBuilder RequireQuota<TBuilder>(this TBuilder builder, string meter = UsageMeter.Default)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new QuotaEnforcementFilter(meter));
        return builder;
    }
}
