namespace SaasTemplate.Api.Tests;

public class SecurityHeaderTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public SecurityHeaderTests(ApiTestFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchema();
    }

    [Fact]
    public async Task Response_includes_ContentSecurityPolicy_header()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.EnsureSuccessStatusCode();
        Assert.True(
            response.Headers.Contains("Content-Security-Policy"),
            "Response should include Content-Security-Policy header");

        var csp = response.Headers.GetValues("Content-Security-Policy").First();
        Assert.Contains("default-src", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
    }

    [Fact]
    public async Task Response_includes_PermissionsPolicy_header()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.EnsureSuccessStatusCode();
        Assert.True(
            response.Headers.Contains("Permissions-Policy"),
            "Response should include Permissions-Policy header");

        var pp = response.Headers.GetValues("Permissions-Policy").First();
        Assert.Contains("camera=()", pp);
        Assert.Contains("microphone=()", pp);
        Assert.Contains("geolocation=()", pp);
    }

    [Fact]
    public async Task CSP_allows_required_external_sources()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.True(response.Headers.Contains("Content-Security-Policy"),
            "CSP header must be present");
        var csp = response.Headers.GetValues("Content-Security-Policy").First();

        // GA4
        Assert.Contains("www.googletagmanager.com", csp);
        // Google Fonts
        Assert.Contains("fonts.googleapis.com", csp);
        Assert.Contains("fonts.gstatic.com", csp);
        // Stripe
        Assert.Contains("js.stripe.com", csp);
        // WebSocket for Blazor SignalR
        Assert.Contains("wss:", csp);
    }

    [Fact]
    public async Task Version_endpoint_still_returns_200_for_Blazor_reconnect()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/version");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("version", content);
    }
}
