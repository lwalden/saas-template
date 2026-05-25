using System.Net;
using System.Net.Http.Json;

namespace SaasTemplate.Api.Tests.Infrastructure;

/// <summary>
/// Tests that the API correctly reads the real client IP from Cloudflare's
/// CF-Connecting-IP header, and that rate limiting applies to the real IP
/// rather than the Cloudflare edge IP.
/// </summary>
public class CloudflareHeaderTests : IClassFixture<RateLimitApiTestFactory>
{
    private readonly RateLimitApiTestFactory _factory;

    public CloudflareHeaderTests(RateLimitApiTestFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchema();
    }

    [Fact]
    public async Task ForwardedHeaders_CfConnectingIp_UsedAsClientIp()
    {
        // Arrange: send a request with CF-Connecting-IP set to a known IP.
        // The app should read this header and use it as the remote client IP.
        // We verify by hitting an endpoint that works (healthz) — if forwarded
        // headers are configured for CF-Connecting-IP, the middleware processes it.
        // The real proof is in the rate-limiting test below, but this test confirms
        // the header is accepted without errors.
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add("CF-Connecting-IP", "203.0.113.42");

        // Act
        var response = await client.SendAsync(request);

        // Assert: the request should succeed — the forwarded headers middleware
        // should accept CF-Connecting-IP and set RemoteIpAddress to 203.0.113.42.
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RateLimiting_UsesRealIpBehindCloudflare()
    {
        // Arrange: Two "different users" behind Cloudflare, identified by different
        // CF-Connecting-IP values. If rate limiting correctly uses the forwarded IP,
        // each user gets their own rate limit bucket. If it ignores the header,
        // both share the loopback IP bucket and User B gets rate-limited.
        //
        // The auth endpoint has a 10 req/min per IP limit.
        var client = _factory.CreateClient();

        // User A: exhaust the rate limit bucket — send 11 requests
        // (10 = limit, 11th ensures the bucket is fully depleted)
        for (var i = 0; i < 11; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            req.Headers.Add("CF-Connecting-IP", "198.51.100.10");
            req.Content = JsonContent.Create(new { email = $"cf-a-{i}@example.com", password = "WrongP@ss12345!" });
            await client.SendAsync(req);
        }

        // User B: send 1 request from a DIFFERENT CF-Connecting-IP.
        // If rate limiting uses real IP (from CF-Connecting-IP), User B has a fresh
        // bucket and should NOT get 429.
        // If the header is ignored, both users share the loopback IP bucket and
        // User B gets 429 because User A already exhausted it.
        var userBRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
        userBRequest.Headers.Add("CF-Connecting-IP", "198.51.100.20");
        userBRequest.Content = JsonContent.Create(new { email = "cf-b@example.com", password = "WrongP@ss12345!" });

        // Act
        var userBResponse = await client.SendAsync(userBRequest);

        // Assert: User B should NOT be rate-limited (401 = bad credentials, which is expected).
        Assert.NotEqual(HttpStatusCode.TooManyRequests, userBResponse.StatusCode);
    }
}
