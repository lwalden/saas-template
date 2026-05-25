using System.Net;
using System.Text;
using System.Net.Http.Json;
using SaasTemplate.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SaasTemplate.Api.Tests.Billing;

public class BillingEndpointTests : IClassFixture<BillingEndpointTests.TestFactory>
{
    // Shared in-memory SQLite — must stay open for entire test class lifetime
    private static readonly SqliteConnection _sharedConn;

    static BillingEndpointTests()
    {
        _sharedConn = new SqliteConnection("Data Source=billing-integ;Mode=Memory;Cache=Shared");
        _sharedConn.Open();
    }

    public class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=billing-integ;Mode=Memory;Cache=Shared");
            builder.UseSetting("STRIPE_SECRET_KEY", "sk_test_fake");
            builder.UseSetting("STRIPE_WEBHOOK_SECRET", "whsec_test_fake");
            builder.UseSetting("STRIPE_PRICE_ID_STARTER", "price_starter_test");
            builder.UseSetting("STRIPE_PRICE_ID_PRO", "price_pro_test");
            builder.UseSetting("STRIPE_PRICE_ID_BUSINESS", "price_business_test");
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
                    options.UseSqlite("Data Source=billing-integ;Mode=Memory;Cache=Shared")
                           .ConfigureWarnings(w => w.Ignore(
                               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
            });
        }
    }

    private readonly TestFactory _factory;

    public BillingEndpointTests(TestFactory factory)
    {
        _factory = factory;
        SqliteSchemaHelper.EnsureSchema("Data Source=billing-integ;Mode=Memory;Cache=Shared");
    }

    // ── Checkout ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Checkout_returns_401_without_jwt()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/billing/checkout", new { tier = "starter" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_returns_400_for_unknown_tier()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/billing/checkout", new { tier = "invalid_tier" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Webhook ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_returns_400_for_missing_stripe_signature()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent("""{"type":"test.event"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_returns_400_for_invalid_stripe_signature()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/billing/webhook")
        {
            Content = new StringContent("""{"type":"test.event"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Stripe-Signature", "t=12345,v1=invalidsig");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Subscription query ────────────────────────────────────────────────

    [Fact]
    public async Task GetSubscription_returns_401_without_jwt()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/billing/subscription");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_returns_404_for_user_with_no_subscription()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/billing/subscription");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task<string> RegisterAndGetToken(HttpClient client)
    {
        var email = $"billing-{Guid.NewGuid():N}@example.com";
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "P@ssword12345!",
            fullName = "Billing Test"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private sealed record AuthResponse(string Token, string Email);
}
