using System.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SaasTemplate.Api.Monitoring;

/// <summary>
/// Wires structured logging, OpenTelemetry tracing/metrics, and request logging.
///
/// OTLP export is configured purely via the standard OpenTelemetry environment
/// variables (OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, etc.).
/// When no endpoint is configured the OTLP exporter is simply not added, so the
/// app collects spans/metrics in-process but performs no network I/O at startup.
/// </summary>
public static class Observability
{
    public const string ServiceName = "SaasTemplate.Api";

    /// <summary>ActivitySource for any custom spans the application wants to emit.</summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName);

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        var config = builder.Configuration;

        var serviceVersion = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Observability).Assembly)
            ?.InformationalVersion ?? "0.0.0";

        // Standard OTel env var; if unset, no OTLP exporter is wired (no network I/O).
        var otlpEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var hasOtlp = !string.IsNullOrWhiteSpace(otlpEndpoint) && !env.IsEnvironment("Testing");

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: ServiceName, serviceVersion: serviceVersion)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", env.EnvironmentName)
            });

        // --- Structured logging ---------------------------------------------
        // Always emit through Microsoft.Extensions.Logging. In production, prefer
        // a JSON console formatter so logs are machine-parseable; locally keep the
        // human-readable simple formatter. Scopes are included so correlation IDs
        // and user/org enrichment flow into every log line.
        builder.Logging.Configure(o =>
            o.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId);

        var useJsonLogs = config.GetValue("LOG_JSON", env.IsProduction());
        if (useJsonLogs)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddJsonConsole(o =>
            {
                o.IncludeScopes = true;
                o.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
            });
        }
        else
        {
            builder.Logging.AddSimpleConsole(o => o.IncludeScopes = true);
        }

        // OpenTelemetry logging — exports log records via OTLP when configured.
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
            o.ParseStateValues = true;
            o.SetResourceBuilder(resourceBuilder);
            if (hasOtlp)
                o.AddOtlpExporter();
        });

        // --- Tracing + metrics ----------------------------------------------
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName: ServiceName, serviceVersion: serviceVersion)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", env.EnvironmentName)
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ServiceName)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        // Don't trace health probes — they're noisy and uninteresting.
                        o.Filter = ctx => !IsHealthPath(ctx.Request.Path);
                        o.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(o => o.RecordException = true)
                    // Statement text is NOT captured (default) so query parameters /
                    // potential PII never reach the exporter.
                    .AddEntityFrameworkCoreInstrumentation();
                if (hasOtlp)
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (hasOtlp)
                    metrics.AddOtlpExporter();
            });

        return builder;
    }

    /// <summary>
    /// Request-logging middleware: records method/route/status/latency. Never logs
    /// query strings, headers, cookies, JWTs, or bodies — only the matched route
    /// template (or path) so no PII/secrets leak. Enriches the log scope with a
    /// correlation id and authenticated user/org ids when present.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        var logger = app.ApplicationServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SaasTemplate.Api.RequestLog");

        return app.Use(async (ctx, next) =>
        {
            // Correlation id: reuse an inbound trace/correlation header or mint one.
            var correlationId =
                FirstHeader(ctx, "X-Correlation-ID") ??
                FirstHeader(ctx, "X-Request-ID") ??
                Activity.Current?.TraceId.ToString() ??
                ctx.TraceIdentifier;

            ctx.Response.Headers["X-Correlation-ID"] = correlationId;

            var scope = new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId,
                ["RequestId"] = ctx.TraceIdentifier,
            };

            var userId = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User?.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId)) scope["UserId"] = userId;

            var orgId = ctx.User?.FindFirst("org")?.Value ?? ctx.User?.FindFirst("org_id")?.Value;
            if (!string.IsNullOrEmpty(orgId)) scope["OrgId"] = orgId;

            using var _ = logger.BeginScope(scope);

            var path = ctx.Request.Path;
            if (IsHealthPath(path))
            {
                await next();
                return;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await next();
            }
            finally
            {
                sw.Stop();
                // Prefer the matched route template over the raw path so we don't log
                // PII embedded in URLs (e.g. /unsubscribe?email=...). Falls back to the
                // path component only (never the query string).
                var route = ctx.GetEndpoint() is { } ep
                    && ep.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.RouteNameMetadata>() is null
                    ? (ctx.Features.Get<IEndpointFeature>()?.Endpoint?.DisplayName ?? path.Value)
                    : path.Value;

                var level = ctx.Response.StatusCode >= 500 ? LogLevel.Error
                    : ctx.Response.StatusCode >= 400 ? LogLevel.Warning
                    : LogLevel.Information;

                logger.Log(level,
                    "HTTP {Method} {Route} responded {StatusCode} in {ElapsedMs}ms",
                    ctx.Request.Method, route, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        });
    }

    private static bool IsHealthPath(PathString path) =>
        path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase);

    private static string? FirstHeader(HttpContext ctx, string name) =>
        ctx.Request.Headers.TryGetValue(name, out var v) && !string.IsNullOrEmpty(v)
            ? v.ToString()
            : null;
}
