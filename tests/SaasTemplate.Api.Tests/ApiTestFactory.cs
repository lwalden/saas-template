using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;

namespace SaasTemplate.Api.Tests;

/// <summary>
/// Shared WebApplicationFactory for tests that need the full app but don't have
/// special per-class configuration requirements. Uses an in-memory SQLite database.
/// </summary>
public class ApiTestFactory : WebApplicationFactory<Program>
{
    private const string ConnStr = "Data Source=api-test-shared;Mode=Memory;Cache=Shared";

    private static readonly SqliteConnection _sharedConn;

    static ApiTestFactory()
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

            // Replace Resend email service with no-op stub for tests
            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor is not null) services.Remove(emailDescriptor);
            services.AddSingleton<IEmailService, NoOpEmailService>();
        });
    }

    public void EnsureSchema() => SqliteSchemaHelper.EnsureSchema(ConnStr);
}

/// <summary>No-op email service for integration tests — avoids Resend API calls.</summary>
internal sealed class NoOpEmailService : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendMagicLinkAsync(string toEmail, string magicLinkUrl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendWelcomeEmailAsync(string toEmail, string tier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendOnboardingEmailAsync(string toEmail, int stage, string tier, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendPaymentFailedAsync(string toEmail, string billingPortalUrl, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
