using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SaasTemplate.Api.Auth;
using SaasTemplate.Api.Data;
using SaasTemplate.Api.Email;
using SaasTemplate.Api.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SaasTemplate.Api.Tests.Auth;

public class MarketingConsentTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public MarketingConsentTests(ApiTestFactory factory)
    {
        _factory = factory;
        factory.EnsureSchema();
    }

    /// <summary>Register a user and return (token, email).</summary>
    private async Task<(string Token, string Email)> RegisterUser(HttpClient client)
    {
        var email = $"consent-{Guid.NewGuid():N}@example.com";
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "P@ssword12345!" });
        resp.EnsureSuccessStatusCode();
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.Token, email);
    }

    // -------------------------------------------------------------------------
    // 1. Default value
    // -------------------------------------------------------------------------
    [Fact]
    public async Task MarketingConsent_DefaultsFalse()
    {
        var client = _factory.CreateClient();
        var (token, email) = await RegisterUser(client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);

        Assert.False(user.MarketingConsent);
    }

    // -------------------------------------------------------------------------
    // 2. POST consent=true persists
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConsentEndpoint_SetTrue_Persists()
    {
        var client = _factory.CreateClient();
        var (token, email) = await RegisterUser(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.PostAsJsonAsync("/api/account/marketing-consent",
            new { consent = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        Assert.True(user.MarketingConsent);
    }

    // -------------------------------------------------------------------------
    // 3. POST consent=false persists
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConsentEndpoint_SetFalse_Persists()
    {
        var client = _factory.CreateClient();
        var (token, email) = await RegisterUser(client);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // First set to true
        await client.PostAsJsonAsync("/api/account/marketing-consent",
            new { consent = true });

        // Then set to false
        var resp = await client.PostAsJsonAsync("/api/account/marketing-consent",
            new { consent = false });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        Assert.False(user.MarketingConsent);
    }

    // -------------------------------------------------------------------------
    // 4. Unauthenticated returns 401
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ConsentEndpoint_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        // No Authorization header
        var resp = await client.PostAsJsonAsync("/api/account/marketing-consent",
            new { consent = true });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // -------------------------------------------------------------------------
    // 5. Unsubscribe sets MarketingConsent = false
    // -------------------------------------------------------------------------
    [Fact]
    public async Task UnsubscribeEndpoint_SetsMarketingConsentFalse()
    {
        var client = _factory.CreateClient();
        var (token, email) = await RegisterUser(client);

        // First opt in
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        await client.PostAsJsonAsync("/api/account/marketing-consent",
            new { consent = true });
        client.DefaultRequestHeaders.Authorization = null;

        // Generate valid unsubscribe token
        using var scope = _factory.Services.CreateScope();
        var jwtSettings = scope.ServiceProvider.GetRequiredService<JwtSettings>();
        var unsToken = UnsubscribeToken.Generate(email, jwtSettings.Secret);

        var encodedEmail = Uri.EscapeDataString(email);
        var resp = await client.GetAsync(
            $"/unsubscribe?email={encodedEmail}&token={unsToken}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        Assert.False(user.MarketingConsent);
    }

    // Tests 6, 7, 8 removed — they tested RescanEmailDecider which is domain-specific
    // and was removed from the SaaS template. Marketing consent behaviour for
    // transactional vs marketing emails is covered by the OnboardingEmailServiceTests.
}
