using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SaasTemplate.Api.Auth;
using SaasTemplate.Api.Data;
using Xunit;

namespace SaasTemplate.Api.Tests.Auth;

public class PasswordResetTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public PasswordResetTests(ApiTestFactory factory)
    {
        _factory = factory;
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    private async Task<string> RegisterAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return email;
    }

    private async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return await userManager.GeneratePasswordResetTokenAsync(user!);
    }

    [Fact]
    public async Task ForgotPassword_returns_ok_for_unknown_email()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = $"nobody-{Guid.NewGuid():N}@example.com" });

        // Anti-enumeration: unknown email still returns 200.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_returns_ok_for_known_email()
    {
        var email = await RegisterAsync($"forgot-{Guid.NewGuid():N}@example.com", "P@ssword12345!");

        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_with_valid_token_changes_password()
    {
        var email = await RegisterAsync($"reset-{Guid.NewGuid():N}@example.com", "P@ssword12345!");
        var token = await GeneratePasswordResetTokenAsync(email);
        const string newPassword = "N3wP@ssword6789!";

        var reset = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // New password works...
        var loginNew = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, loginNew.StatusCode);

        // ...and the old one no longer does.
        var loginOld = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "P@ssword12345!" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_with_invalid_token_returns_badrequest()
    {
        var email = await RegisterAsync($"resetbad-{Guid.NewGuid():N}@example.com", "P@ssword12345!");

        var response = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token = "not-a-real-token", newPassword = "N3wP@ssword6789!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_with_unknown_email_returns_badrequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = $"ghost-{Guid.NewGuid():N}@example.com", token = "x", newPassword = "N3wP@ssword6789!" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_rejecting_weak_password_returns_validation_error()
    {
        var email = await RegisterAsync($"resetweak-{Guid.NewGuid():N}@example.com", "P@ssword12345!");
        var token = await GeneratePasswordResetTokenAsync(email);

        // "weak" fails the password policy (length/complexity) — surfaced as a validation problem, not the generic message.
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { email, token, newPassword = "weak" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
