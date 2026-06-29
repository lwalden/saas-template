using System.Text.Json;

namespace SaasTemplate.Api.Tests;

/// <summary>
/// FEAT-16: asserts the OpenAPI document builds and describes the public API surface
/// (representative Auth + Billing routes) while excluding internal/ops and infra routes.
/// </summary>
public class OpenApiTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public OpenApiTests(ApiTestFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchema();
    }

    [Fact]
    public async Task OpenApiDocument_IsReachable_AndDescribesPublicSurface()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Document metadata reflects the v1 surface.
        Assert.Equal("SaasTemplate API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());

        var paths = root.GetProperty("paths");

        // Representative PUBLIC endpoints are present.
        Assert.True(paths.TryGetProperty("/api/auth/register", out _), "register route missing");
        Assert.True(paths.TryGetProperty("/api/auth/login", out _), "login route missing");
        Assert.True(paths.TryGetProperty("/api/billing/subscription", out _), "subscription route missing");

        // Internal/ops and infra endpoints are EXCLUDED.
        Assert.False(paths.TryGetProperty("/api/ops/status", out _), "ops route must be excluded");
        Assert.False(paths.TryGetProperty("/api/ops/users", out _), "ops route must be excluded");
        Assert.False(paths.TryGetProperty("/healthz", out _), "healthz must be excluded");
        Assert.False(paths.TryGetProperty("/api/billing/webhook", out _), "webhook must be excluded");

        // The JWT bearer security scheme is documented (FEAT-06 will add API keys).
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(schemes.TryGetProperty("Bearer", out var bearer), "Bearer scheme missing");
        Assert.Equal("http", bearer.GetProperty("type").GetString());

        // Authorized billing operation references the Bearer requirement.
        var getSub = paths.GetProperty("/api/billing/subscription").GetProperty("get");
        Assert.True(getSub.TryGetProperty("security", out var security) && security.GetArrayLength() > 0,
            "authorized endpoint should carry a security requirement");
    }
}
