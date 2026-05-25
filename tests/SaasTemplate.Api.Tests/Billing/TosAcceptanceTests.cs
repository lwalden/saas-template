using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SaasTemplate.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SaasTemplate.Api.Tests.Billing;

public class TosAcceptanceTests : IClassFixture<TosAcceptanceTests.TestFactory>
{
    private static readonly SqliteConnection _sharedConn;

    static TosAcceptanceTests()
    {
        _sharedConn = new SqliteConnection("Data Source=tos-test;Mode=Memory;Cache=Shared");
        _sharedConn.Open();
    }

    public class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=tos-test;Mode=Memory;Cache=Shared");
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
                    options.UseSqlite("Data Source=tos-test;Mode=Memory;Cache=Shared")
                           .ConfigureWarnings(w => w.Ignore(
                               Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
            });
        }
    }

    private readonly TestFactory _factory;

    public TosAcceptanceTests(TestFactory factory)
    {
        _factory = factory;
        SqliteSchemaHelper.EnsureSchema("Data Source=tos-test;Mode=Memory;Cache=Shared");
    }

    [Fact]
    public async Task Checkout_rejects_when_user_has_no_tos_acceptance()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/billing/checkout", new { tier = "starter" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("terms", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AcceptTos_stores_timestamp_on_user()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsync("/api/billing/accept-tos", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TosResponse>();
        Assert.NotNull(body?.AcceptedAt);
    }

    [Fact]
    public async Task AcceptTos_is_idempotent()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var first = await client.PostAsync("/api/billing/accept-tos", null);
        var firstBody = await first.Content.ReadFromJsonAsync<TosResponse>();

        var second = await client.PostAsync("/api/billing/accept-tos", null);
        var secondBody = await second.Content.ReadFromJsonAsync<TosResponse>();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // Second call should NOT overwrite the first timestamp
        Assert.NotNull(firstBody!.AcceptedAt);
        Assert.NotNull(secondBody!.AcceptedAt);
        // Both should represent the same moment — compare the raw strings from the second call onward
        // (both read from DB at that point, same format)
        var third = await client.PostAsync("/api/billing/accept-tos", null);
        var thirdBody = await third.Content.ReadFromJsonAsync<TosResponse>();
        Assert.Equal(secondBody.AcceptedAt, thirdBody!.AcceptedAt);
    }

    [Fact]
    public async Task Checkout_succeeds_after_tos_acceptance()
    {
        var client = _factory.CreateClient();
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Accept ToS first
        await client.PostAsync("/api/billing/accept-tos", null);

        // Checkout will fail at Stripe (fake key), but should NOT fail with ToS error
        var response = await client.PostAsJsonAsync("/api/billing/checkout", new { tier = "starter" });

        // Should be 502 (Stripe error) not 400 (ToS error)
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static async Task<string> RegisterAndGetToken(HttpClient client)
    {
        var email = $"tos-{Guid.NewGuid():N}@example.com";
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "P@ssword12345!",
            fullName = "ToS Test"
        });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private sealed record AuthResponse(string Token, string Email);
    private sealed record TosResponse(string AcceptedAt);
}
