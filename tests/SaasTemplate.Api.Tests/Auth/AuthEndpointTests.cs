using System.Net;
using System.Net.Http.Json;
using SaasTemplate.Api.Auth;
using Xunit;

namespace SaasTemplate.Api.Tests.Auth;

public class AuthEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(ApiTestFactory factory)
    {
        _factory = factory;
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_returns_token_for_valid_request()
    {
        var request = new { email = $"test-{Guid.NewGuid():N}@example.com", password = "P@ssword12345!", fullName = "Test User" };
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.Equal(request.email, body.Email);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var request = new { email, password = "P@ssword12345!" };

        await _client.PostAsJsonAsync("/api/auth/register", request);
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_short_password()
    {
        var request = new { email = $"short-{Guid.NewGuid():N}@example.com", password = "abc" };
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_token_for_valid_credentials()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "P@ssword12345!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body.Token));
    }

    [Fact]
    public async Task Login_returns_unauthorized_for_wrong_password()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongP@ss12345!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_unauthorized_for_nonexistent_user()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "noone@example.com", password = "P@ssword12345!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MagicLink_returns_ok_even_for_unknown_email()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/magic-link",
            new { email = "unknown@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Google_auth_endpoint_returns_redirect()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/auth/google");

        // Google OAuth challenge returns 302 redirect to accounts.google.com
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("accounts.google.com", response.Headers.Location?.ToString() ?? "");
    }

    [Fact]
    public async Task Google_callback_without_external_cookie_redirects_to_login_with_error()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/auth/google/callback");

        // Without a valid ExternalOAuth cookie, callback fails and redirects to /login?error=oauth-failed
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.ToString() ?? "";
        Assert.Contains("/login", location);
        Assert.Contains("error=oauth-failed", location);
    }
}
