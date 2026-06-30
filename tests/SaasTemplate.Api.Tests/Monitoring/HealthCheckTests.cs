using System.Net;

namespace SaasTemplate.Api.Tests.Monitoring;

/// <summary>
/// Verifies the readiness/liveness health endpoint split. The existing /healthz
/// mapping must keep returning 200, and the new /healthz/live and /healthz/ready
/// endpoints must respond 200 under the Testing environment (SQLite DB created).
/// </summary>
public class HealthCheckTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public HealthCheckTests(ApiTestFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchema();
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/healthz/live")]
    [InlineData("/healthz/ready")]
    public async Task HealthEndpoint_Returns200(string path)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Response_IncludesCorrelationIdHeader()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api");

        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }
}
