namespace SaasTemplate.Api.Tests;

public class HealthCheckTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public HealthCheckTests(ApiTestFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchema();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", content);
    }

    [Fact]
    public async Task ApiEndpoint_ReturnsServiceInfo()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("SaasTemplate API", content);
    }
}
