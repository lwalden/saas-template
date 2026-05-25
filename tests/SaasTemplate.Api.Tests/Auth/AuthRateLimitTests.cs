using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SaasTemplate.Api.Tests.Auth;

/// <summary>
/// Tests that auth endpoints are rate-limited.
/// Uses RateLimitApiTestFactory which enables rate limiting (non-Testing environment).
/// </summary>
public class AuthRateLimitTests : IClassFixture<RateLimitApiTestFactory>
{
    private readonly RateLimitApiTestFactory _factory;

    public AuthRateLimitTests(RateLimitApiTestFactory factory)
    {
        factory.EnsureSchema();
        _factory = factory;
    }

    [Fact]
    public async Task Login_returns_429_after_exceeding_rate_limit()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "ratelimit@example.com", password = "WrongP@ss12345!" };

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 15; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", payload);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task Register_returns_429_after_exceeding_rate_limit()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 15; i++)
        {
            var payload = new { email = $"reg-rl-{i}@example.com", password = "P@ssword12345!" };
            lastResponse = await client.PostAsJsonAsync("/api/auth/register", payload);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task MagicLink_returns_429_after_exceeding_rate_limit()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "magic-rl@example.com" };

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 10; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/magic-link", payload);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }

    [Fact]
    public async Task MagicLinkVerify_returns_429_after_exceeding_rate_limit()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "verify-rl@example.com", token = "fake-token" };

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 15; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/magic-link/verify", payload);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
