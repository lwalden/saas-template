using System.Text;
using System.Threading.RateLimiting;
using SaasTemplate.Api;
using Microsoft.AspNetCore.HttpOverrides;
using SaasTemplate.Api.Auth;
using Microsoft.AspNetCore.DataProtection;
using SaasTemplate.Api.Billing;
using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;
using SaasTemplate.Api.Monitoring;
using SaasTemplate.Api.OpenApi;
using SaasTemplate.Api.Security;
using SaasTemplate.Api.Webhooks;
using Stripe;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Resend;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Load .env file in Development — ASP.NET Core doesn't do this automatically.
if (builder.Environment.IsDevelopment())
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var envFile = Path.Combine(dir.FullName, ".env");
        if (System.IO.File.Exists(envFile))
        {
            foreach (var line in System.IO.File.ReadAllLines(envFile))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                if (!string.IsNullOrEmpty(key))
                    Environment.SetEnvironmentVariable(key, value);
            }
            break;
        }
        dir = dir.Parent;
    }
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection connection string is required.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
               sql.EnableRetryOnFailure(
                   maxRetryCount: 5,
                   maxRetryDelay: TimeSpan.FromSeconds(10),
                   errorNumbersToAdd: null))
           .ConfigureWarnings(w => w.Ignore(
               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromMinutes(15));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// JWT
var jwtSecret = builder.Configuration["JWT_SECRET"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
        jwtSecret = "dev-secret-key-min-32-characters-long!";
    else
        throw new InvalidOperationException("JWT_SECRET is required in production environments.");
}

var jwtSettings = new JwtSettings
{
    Secret = jwtSecret,
    Issuer = "SaasTemplate",
    Audience = "SaasTemplate",
    ExpiryMinutes = int.TryParse(builder.Configuration["JWT_EXPIRY_MINUTES"], out var exp) ? exp : 60
};
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<ITokenService, SaasTemplate.Api.Auth.TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub
        };
    })
    .AddCookie("ExternalOAuth", options =>
    {
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"] ?? "";
        options.ClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"] ?? "";
        options.SignInScheme = "ExternalOAuth";
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<SaasTemplate.Api.Auth.DashboardSession>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<SaasTemplate.Api.Auditing.IAuditLogger, SaasTemplate.Api.Auditing.AuditLogger>();

builder.Services.AddHttpClient("internal", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["INTERNAL_BASE_URL"] ?? "http://localhost:5131");
});

// Observability: structured logging, OpenTelemetry tracing + metrics.
// OTLP export only activates when OTEL_EXPORTER_OTLP_ENDPOINT is set (no network I/O otherwise).
builder.AddObservability();

// OpenAPI (FEAT-16): generate the public API document ("v1") from code.
// The document is served unconditionally below; only the interactive UI is non-prod gated.
builder.Services.AddPublicOpenApi();

// Health checks: a "live" tag for liveness (trivial) and a DB check tagged "ready"
// for readiness. The DB check has a short timeout so a slow DB can't hang the probe.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<OnboardingEmailService>();

// Validate required external API keys at startup (skip in Testing)
if (!builder.Environment.IsEnvironment("Testing"))
{
    var requiredKeys = new[] { "STRIPE_SECRET_KEY", "RESEND_API_KEY" };
    var missingKeys = requiredKeys.Where(k => string.IsNullOrWhiteSpace(builder.Configuration[k])).ToList();
    if (missingKeys.Count > 0)
        throw new InvalidOperationException($"Required environment variable(s) not set: {string.Join(", ", missingKeys)}");
}

// Stripe
StripeConfiguration.ApiKey = builder.Configuration["STRIPE_SECRET_KEY"] ?? "";

builder.Services.AddSingleton<AppSettings>();

// Resend email
builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
    o.ApiToken = builder.Configuration["RESEND_API_KEY"] ?? "");
builder.Services.AddTransient<IResend, ResendClient>();
builder.Services.AddTransient<IEmailService, ResendEmailService>();

// Outbound webhook dispatcher (n8n integration)
builder.Services.AddHttpClient("webhook", client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<IWebhookDispatcher, WebhookDispatcher>();

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["https://saastemplate.example.com", "https://www.saastemplate.example.com"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .WithHeaders("Content-Type", "Authorization")
              .WithMethods("POST", "GET", "PUT", "PATCH", "DELETE"));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardedForHeaderName = "CF-Connecting-IP";
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var enableRateLimiting = !builder.Environment.IsEnvironment("Testing")
    || builder.Configuration.GetValue<bool>("ENABLE_RATE_LIMITING_IN_TESTS");

if (enableRateLimiting)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("auth", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            });
        });

        options.AddPolicy("auth-magic-link", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6
            });
        });

        options.AddPolicy("public-checkout", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2
            });
        });
    });
}

var app = builder.Build();

app.Services.GetRequiredService<AppSettings>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

app.UseForwardedHeaders();

// Error tracking (outermost app-level handler) + request logging. Placed right after
// forwarded headers so the real client IP / correlation id are available, and around
// the rest of the pipeline so latency and unhandled exceptions are captured.
app.UseErrorTracking();
app.UseRequestLogging();

app.UseHttpsRedirection();

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://www.googletagmanager.com https://www.google-analytics.com https://js.stripe.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https://www.google-analytics.com https://www.googletagmanager.com; " +
        "connect-src 'self' https://www.google-analytics.com https://www.googletagmanager.com https://analytics.google.com wss:; " +
        "frame-src https://js.stripe.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    ctx.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=(), usb=(), payment=()";
    await next();
});

// Billing webhook mapped before CORS — Stripe calls are server-to-server
app.MapBillingEndpoints();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsEnvironment("Testing") || app.Configuration.GetValue<bool>("ENABLE_RATE_LIMITING_IN_TESTS"))
    app.UseRateLimiter();
app.UseStaticFiles();
app.UseAntiforgery();
// Health endpoints with a readiness/liveness split.
// - /healthz       : overall (all checks) — preserved for existing callers/tests.
// - /healthz/live  : liveness — trivial "self" check only, never touches the DB.
// - /healthz/ready : readiness — includes the DB check.
// Infra endpoints are excluded from the public OpenAPI document.
app.MapHealthChecks("/healthz").ExcludeFromDescription();
app.MapHealthChecks("/healthz/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).ExcludeFromDescription();
app.MapHealthChecks("/healthz/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).ExcludeFromDescription();

var buildVersion = System.Reflection.CustomAttributeExtensions
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Program).Assembly)
    ?.InformationalVersion ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
app.MapGet("/api/version", () => Results.Ok(new { version = buildVersion }))
    .WithName("GetApiVersion")
    .WithSummary("Get API build version")
    .WithDescription("Returns the deployed build/informational version string.")
    .WithTags("Meta")
    .Produces(StatusCodes.Status200OK);

var opsApiKey = app.Configuration["OPS_API_KEY"];
if (!string.IsNullOrWhiteSpace(opsApiKey))
{
    if (opsApiKey.Length < 32)
        throw new InvalidOperationException("OPS_API_KEY must be at least 32 characters.");
    app.MapOpsEndpoints(opsApiKey);
}

app.MapGet("/api", () => Results.Ok(new { service = "SaasTemplate API", status = "running" }))
    .WithName("GetApiRoot")
    .WithSummary("API root / liveness ping")
    .WithDescription("Lightweight unauthenticated check that the API is running.")
    .WithTags("Meta")
    .Produces(StatusCodes.Status200OK);

// CAN-SPAM one-click unsubscribe
app.MapGet("/unsubscribe", async (string? email, string? token, UserManager<ApplicationUser> userManager, JwtSettings jwtSettings) =>
{
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        return Results.Content(UnsubscribeHtml("Invalid unsubscribe link. Please contact support."), "text/html");

    if (!SaasTemplate.Api.Auth.UnsubscribeToken.Validate(email, token, jwtSettings.Secret))
        return Results.Content(UnsubscribeHtml("This unsubscribe link is invalid or has expired."), "text/html");

    var user = await userManager.FindByEmailAsync(email);
    if (user is not null && user.MarketingConsent)
    {
        user.MarketingConsent = false;
        await userManager.UpdateAsync(user);
    }

    return Results.Content(UnsubscribeHtml("You have been unsubscribed. You will no longer receive marketing emails."), "text/html");

    static string UnsubscribeHtml(string message) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"><title>Unsubscribe</title></head>
        <body style="font-family:sans-serif;max-width:480px;margin:4rem auto;padding:1.5rem;text-align:center;color:#1e293b;">
          <h1 style="font-size:1.25rem;font-weight:700;margin-bottom:1rem;">SaasTemplate</h1>
          <p style="font-size:1rem;color:#475569;">{message}</p>
        </body>
        </html>
        """;
}).ExcludeFromDescription();

app.MapPost("/api/account/marketing-consent", async (MarketingConsentRequest req, UserManager<ApplicationUser> userManager, System.Security.Claims.ClaimsPrincipal principal) =>
{
    var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var user = await userManager.FindByIdAsync(userId);
    if (user is null)
        return Results.NotFound();

    user.MarketingConsent = req.Consent;
    await userManager.UpdateAsync(user);
    return Results.Ok(new { consent = user.MarketingConsent });
}).RequireAuthorization()
  .WithName("SetMarketingConsent")
  .WithSummary("Update marketing email consent")
  .WithDescription("Sets the authenticated user's marketing-email consent flag. Requires a JWT bearer token.")
  .WithTags("Account")
  .Accepts<MarketingConsentRequest>("application/json")
  .Produces(StatusCodes.Status200OK)
  .Produces(StatusCodes.Status401Unauthorized)
  .Produces(StatusCodes.Status404NotFound);

app.MapAuthEndpoints();

// OpenAPI (FEAT-16): serve the generated public API document at /openapi/v1.json.
// Mapped unconditionally so it's reachable in Development AND Testing (tests GET it).
app.MapOpenApi("/openapi/{documentName}.json");

// Interactive API reference (Scalar) — NON-production only. Never exposed in Production.
if (!app.Environment.IsProduction())
{
    app.MapScalarApiReference("/scalar", options =>
    {
        options.OpenApiRoutePattern = "/openapi/v1.json";
        options.Title = "SaasTemplate API v1";
    });
}

app.MapRazorComponents<SaasTemplate.Api.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
