using System.Net;

namespace SaasTemplate.Api.Tests.Billing;

public class PricingPageTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;

    public PricingPageTests(ApiTestFactory factory)
    {
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Pricing_page_returns_200_without_authentication()
    {
        var response = await _client.GetAsync("/pricing");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Pricing_page_contains_plan_cards()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Starter", html);
        Assert.Contains("Professional", html);
        Assert.Contains("Business", html);
    }

    [Fact]
    public async Task Pricing_page_has_sign_in_and_get_started_ctas()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        // Email input is inside a dialog that opens on "Get Started" click.
        // On initial render, verify the CTA buttons and sign-in link exist.
        Assert.Contains("Get Started", html);
        Assert.Contains("Sign in", html);
        Assert.Contains("/login", html);
    }

    [Fact]
    public async Task Pricing_page_contains_annual_toggle()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Monthly", html);
        Assert.Contains("Annual", html);
        Assert.Contains("Save 10%", html);
    }

    [Fact]
    public async Task Pricing_page_has_page_title()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Pricing", html);
    }

    [Fact]
    public async Task Pricing_page_has_accessible_feature_comparison_table()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<table", html);
        Assert.Contains("<caption", html);
        Assert.Contains("scope=\"col\"", html);
        Assert.Contains("scope=\"row\"", html);
    }

    [Fact]
    public async Task Pricing_page_has_skip_link()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("skip-link", html);
        Assert.Contains("Skip to main content", html);
    }

    [Fact]
    public async Task Pricing_page_has_included_in_every_plan_section()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("pricing-included", html);
        Assert.Contains("Free scan included", html);
        Assert.Contains("Cancel anytime", html);
        Assert.Contains("Code fixes you review first", html);
        Assert.Contains("Compliance PDF reports", html);
    }

    [Fact]
    public async Task Pricing_page_has_social_proof_text()
    {
        var response = await _client.GetAsync("/pricing");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Shopify store owners who fixed their accessibility issues", html);
        Assert.Contains("not with an overlay widget", html);
    }
}
