using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SaasTemplate.Api.Data;
using Xunit;

namespace SaasTemplate.Api.Tests.Auth;

public class EmailVerificationTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public EmailVerificationTests(ApiTestFactory factory)
    {
        _factory = factory;
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    private async Task<ApplicationUser> GetUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user!;
    }

    private async Task<string> GenerateEmailConfirmationTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return await userManager.GenerateEmailConfirmationTokenAsync(user!);
    }

    [Fact]
    public async Task Register_creates_unconfirmed_email()
    {
        var email = $"verify-init-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        var user = await GetUserAsync(email);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task VerifyEmail_with_valid_token_confirms_email()
    {
        var email = $"verify-ok-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });
        var token = await GenerateEmailConfirmationTokenAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", new { email, token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await GetUserAsync(email);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task VerifyEmail_with_invalid_token_returns_badrequest()
    {
        var email = $"verify-bad-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        var response = await _client.PostAsJsonAsync("/api/auth/verify-email",
            new { email, token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var user = await GetUserAsync(email);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task SendVerification_returns_ok_for_unknown_email()
    {
        // Anti-enumeration: unknown email still returns 200.
        var response = await _client.PostAsJsonAsync("/api/auth/send-verification",
            new { email = $"nobody-{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendVerification_returns_ok_for_known_unverified_email()
    {
        var email = $"verify-resend-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        var response = await _client.PostAsJsonAsync("/api/auth/send-verification", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
