using System.Diagnostics;

namespace SaasTemplate.Api.Monitoring;

/// <summary>
/// Error-tracking integration point for unhandled exceptions.
///
/// This is a lightweight, dependency-free implementation: unhandled exceptions are
/// recorded through the structured logger with environment and release tags and
/// associated with the active trace/correlation id, then re-thrown so the normal
/// ASP.NET Core error handling still runs.
///
/// The <c>SENTRY_DSN</c> environment variable is honoured as the conventional
/// toggle: when set, a "sentry.dsn.configured" tag is attached so an operator can
/// confirm the integration point is wired. Swapping in the Sentry SDK later only
/// requires adding the package and calling its middleware here — the env-var
/// contract stays the same.
/// </summary>
public static class ErrorTracking
{
    public static IApplicationBuilder UseErrorTracking(this IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var logger = app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SaasTemplate.Api.ErrorTracking");

        var release = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(ErrorTracking).Assembly)
            ?.InformationalVersion ?? "0.0.0";

        var dsnConfigured = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("SENTRY_DSN"));

        return app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                // Record the exception on the active span so traced backends correlate it.
                var activity = Activity.Current;
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                var correlationId =
                    (ctx.Response.Headers.TryGetValue("X-Correlation-ID", out var cid) ? cid.ToString() : null)
                    ?? activity?.TraceId.ToString()
                    ?? ctx.TraceIdentifier;

                // Log path only (never query string) to avoid leaking PII/secrets.
                using var scope = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["Environment"] = env.EnvironmentName,
                    ["Release"] = release,
                    ["CorrelationId"] = correlationId,
                    ["SentryDsnConfigured"] = dsnConfigured,
                });

                logger.LogError(ex,
                    "Unhandled exception processing {Method} {Path}",
                    ctx.Request.Method, ctx.Request.Path.Value);

                throw;
            }
        });
    }
}
