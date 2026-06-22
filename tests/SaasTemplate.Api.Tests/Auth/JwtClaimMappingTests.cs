using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using SaasTemplate.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaasTemplate.Api.Email;
using Xunit;

namespace SaasTemplate.Api.Tests.Auth;

/// <summary>
/// Verifies that JWT claim mapping works correctly in the ASP.NET pipeline —
/// specifically that the "sub" claim from the JWT is accessible as
/// ClaimTypes.NameIdentifier on the server side. This is the root cause
/// of the production defect where scans were created with null UserId.
/// </summary>
public class JwtClaimMappingTests : IClassFixture<JwtClaimMappingTests.TestFactory>
{
    private const string ConnStr = "Data Source=jwt-claim-test;Mode=Memory;Cache=Shared";

    public class TestFactory : WebApplicationFactory<Program>
    {
        private static readonly SqliteConnection _conn;
        static TestFactory() { _conn = new SqliteConnection(ConnStr); _conn.Open(); }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnStr);
            builder.UseSetting("GOOGLE_CLIENT_ID", "test-client-id");
            builder.UseSetting("GOOGLE_CLIENT_SECRET", "test-client-secret");
            builder.ConfigureServices(services =>
            {
                var toRemove = services.Where(d => d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true).ToList();
                foreach (var d in toRemove) services.Remove(d);
                services.AddDbContext<AppDbContext>(o => o.UseSqlite(ConnStr)
                    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
                var emailDesc = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
                if (emailDesc is not null) services.Remove(emailDesc);
                services.AddSingleton<IEmailService, NoOpEmailService>();
            });
        }
    }

    private sealed class NoOpEmailService : IEmailService
    {
        public Task SendAsync(string a, string b, string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendMagicLinkAsync(string a, string b, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPasswordResetAsync(string a, string b, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendEmailVerificationAsync(string a, string b, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendWelcomeEmailAsync(string a, string b, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendOnboardingEmailAsync(string a, int b, string c, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendPaymentFailedAsync(string a, string b, CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly TestFactory _factory;

    public JwtClaimMappingTests(TestFactory factory)
    {
        _factory = factory;
        SqliteSchemaHelper.EnsureSchema(ConnStr);
    }

    /* Removed: scan endpoint was deleted from the template.
    /// <summary>
    /// S19-000: Authenticated scan must store the user's ID — not null.
    /// This test reproduces the production defect where ClaimTypes.NameIdentifier
    /// returned null because the JWT "sub" claim was not mapped.
    /// </summary>
    [Fact]
    public async Task Authenticated_scan_stores_userId_not_null()
    {
        var client = _factory.CreateClient();
        var email = $"claim-test-{Guid.NewGuid():N}@example.com";

        // Register and get JWT
        var regResp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "P@ssword12345!",
            fullName = "Claim Test"
        });
        regResp.EnsureSuccessStatusCode();
        var auth = await regResp.Content.ReadFromJsonAsync<AuthResp>();
        Assert.NotNull(auth?.Token);

        // Verify the JWT contains a "sub" claim
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(auth!.Token);
        var subClaim = jwt.Subject;
        Assert.False(string.IsNullOrEmpty(subClaim), "JWT must contain a 'sub' claim");

        // Get the actual user ID from the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        Assert.Equal(user.Id, subClaim);

        // Create an active subscription
        db.Subscriptions.Add(new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            StripeCustomerId = "cus_claim_test",
            StripeSubscriptionId = $"sub_claim_{Guid.NewGuid():N}",
            StripePriceId = "price_starter",
            Tier = SubscriptionTier.Starter,
            Status = SubscriptionStatus.Active
        });
        await db.SaveChangesAsync();

        // Submit a scan with the JWT
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Token);
        var scanResp = await client.PostAsJsonAsync("/api/scans", new
        {
            url = "https://claim-mapping-test.myshopify.com",
            email
        });
        Assert.Equal(System.Net.HttpStatusCode.Accepted, scanResp.StatusCode);

        // THE CRITICAL ASSERTION: the scan must have the user's ID, not null
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scan = await verifyDb.Scans
            .FirstOrDefaultAsync(s => s.Url == "https://claim-mapping-test.myshopify.com");
        Assert.NotNull(scan);
        Assert.NotNull(scan!.UserId);
        Assert.Equal(user.Id, scan.UserId);
    }
    */

    /// <summary>
    /// Verify that all endpoints using ClaimTypes.NameIdentifier can resolve it.
    /// The billing subscription endpoint is the most critical after scans.
    /// </summary>
    [Fact]
    public async Task Billing_subscription_endpoint_resolves_userId()
    {
        var client = _factory.CreateClient();
        var email = $"billing-claim-{Guid.NewGuid():N}@example.com";

        var regResp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "P@ssword12345!",
            fullName = "Billing Claim Test"
        });
        regResp.EnsureSuccessStatusCode();
        var auth = await regResp.Content.ReadFromJsonAsync<AuthResp>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);

        // GET /api/billing/subscription — should return 404 (no sub), not 401/500
        var resp = await client.GetAsync("/api/billing/subscription");
        // 404 = userId resolved correctly, just no subscription found
        // 401 or 500 = claim mapping broken
        Assert.Equal(System.Net.HttpStatusCode.NotFound, resp.StatusCode);
    }

    private sealed record AuthResp(string Token, string Email, bool HasActiveSubscription = false);
}
