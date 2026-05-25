using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;

namespace SaasTemplate.Api.Tests;

/// <summary>
/// WebApplicationFactory variant that enables rate limiting within the Testing environment.
/// Sets ENABLE_RATE_LIMITING_IN_TESTS=true so Program.cs registers rate limiter services
/// and middleware while keeping all other Testing-environment accommodations (JWT fallback,
/// skipped API key validation, no background services).
/// Uses a separate SQLite connection to avoid cross-test state pollution with ApiTestFactory.
/// </summary>
public class RateLimitApiTestFactory : WebApplicationFactory<Program>
{
    private const string ConnStr = "Data Source=rate-limit-test;Mode=Memory;Cache=Shared";

    private static readonly SqliteConnection _sharedConn;

    static RateLimitApiTestFactory()
    {
        _sharedConn = new SqliteConnection(ConnStr);
        _sharedConn.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnStr);
        builder.UseSetting("GOOGLE_CLIENT_ID", "test-client-id");
        builder.UseSetting("GOOGLE_CLIENT_SECRET", "test-client-secret");
        builder.UseSetting("ENABLE_RATE_LIMITING_IN_TESTS", "true");

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType.FullName != null &&
                            d.ServiceType.FullName.Contains("EntityFrameworkCore"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(ConnStr)
                       .ConfigureWarnings(w => w.Ignore(
                           Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            // Replace Resend email service with no-op stub
            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null) services.Remove(emailDescriptor);
            services.AddSingleton<IEmailService, NoOpEmailService>();
        });
    }

    public void EnsureSchema() => SqliteSchemaHelper.EnsureSchema(ConnStr);
}
