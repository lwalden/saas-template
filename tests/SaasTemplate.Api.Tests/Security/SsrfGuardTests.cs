using SaasTemplate.Api.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace SaasTemplate.Api.Tests.Security;

public class SsrfGuardTests
{
    private readonly Mock<ILogger> _logger = new();

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    public async Task Loopback_and_private_IPs_are_always_blocked(string host)
    {
        var result = await SsrfGuard.IsBlockedHostAsync(host, _logger.Object);
        Assert.True(result.IsBlocked);
    }

    [Theory]
    [InlineData("google.com")]
    [InlineData("shopify.com")]
    public async Task Public_hosts_are_not_blocked(string host)
    {
        var result = await SsrfGuard.IsBlockedHostAsync(host, _logger.Object);
        Assert.False(result.IsBlocked);
    }

    [Fact]
    public async Task DNS_failure_returns_distinct_error()
    {
        var result = await SsrfGuard.IsBlockedHostAsync("this-domain-definitely-does-not-exist-12345.com", _logger.Object);
        Assert.True(result.IsBlocked);
        Assert.Contains("resolve", result.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
