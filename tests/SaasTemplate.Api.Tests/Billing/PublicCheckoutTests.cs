using System.Net;
using System.Net.Http.Json;
using SaasTemplate.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

public class PublicCheckoutTests : IClassFixture<PublicCheckoutTests.TestFactory>
{
    private static readonly SqliteConnection _sharedConn;

    static PublicCheckoutTests()
    {
        _sharedConn = new SqliteConnection("Data Source=public-checkout-test;Mode=Memory;Cache=Shared");
        _sharedConn.Open();
    }

    public class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=public-checkout-test;Mode=Memory;Cache=Shared");
            builder.UseSetting("STRIPE_SECRET_KEY", "sk_test_fake");
            builder.UseSetting("STRIPE_WEBHOOK_SECRET", "whsec_test_fake");
            builder.UseSetting("STRIPE_PRICE_ID_STARTER", "price_starter_test");
            builder.UseSetting("STRIPE_PRICE_ID_STARTER_ANNUAL", "price_starter_annual_test");
            builder.UseSetting("STRIPE_PRICE_ID_PRO", "price_pro_test");
            builder.UseSetting("STRIPE_PRICE_ID_PRO_ANNUAL", "price_pro_annual_test");
            builder.UseSetting("STRIPE_PRICE_ID_BUSINESS", "price_business_test");
            builder.UseSetting("STRIPE_PRICE_ID_BUSINESS_ANNUAL", "price_business_annual_test");
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
                    options.UseSqlite("Data Source=public-checkout-test;Mode=Memory;Cache=Shared")
                           .ConfigureWarnings(w => w.Ignore(
                               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
            });
        }
    }

    private readonly TestFactory _factory;

    public PublicCheckoutTests(TestFactory factory)
    {
        _factory = factory;
        SqliteSchemaHelper.EnsureSchema("Data Source=public-checkout-test;Mode=Memory;Cache=Shared");
    }

    [Fact]
    public async Task Public_checkout_accessible_without_jwt()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/billing/public-checkout",
            new { tier = "starter", email = "test@example.com" });

        // Should NOT be 401 — endpoint is public
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_checkout_returns_400_for_missing_email()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/billing/public-checkout",
            new { tier = "starter", email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("email", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Public_checkout_returns_400_for_invalid_email()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/billing/public-checkout",
            new { tier = "starter", email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_checkout_returns_400_for_unknown_tier()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/billing/public-checkout",
            new { tier = "nonexistent", email = "test@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_checkout_returns_502_for_valid_request_with_fake_stripe_key()
    {
        // With a fake Stripe key, the Stripe API call will fail — but the endpoint
        // should reach the Stripe call (not fail on validation). 502 = Stripe error.
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/billing/public-checkout",
            new { tier = "starter", email = "valid@example.com" });

        // Either 502 (Stripe failure) or 200 (if Stripe somehow works) — but NOT 400 or 401
        Assert.True(
            response.StatusCode == HttpStatusCode.BadGateway ||
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 502 or 200 but got {response.StatusCode}");
    }
}
