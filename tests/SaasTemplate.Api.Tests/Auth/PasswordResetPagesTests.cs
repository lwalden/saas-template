using System.Net;

namespace SaasTemplate.Api.Tests.Auth;

/// <summary>
/// Smoke tests for the user-facing FEAT-03 Blazor pages (prerendered GET).
/// The verify-email page consumes its token in OnAfterRenderAsync (interactive
/// circuit only), so it is not GET-smoke-tested here — the backend verify flow
/// is covered by EmailVerificationTests.
/// </summary>
public class PasswordResetPagesTests : IClassFixture<ApiTestFactory>
{
    private readonly HttpClient _client;

    public PasswordResetPagesTests(ApiTestFactory factory)
    {
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ForgotPassword_page_returns_200()
    {
        var response = await _client.GetAsync("/auth/forgot-password");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_page_has_email_form_and_back_link()
    {
        var response = await _client.GetAsync("/auth/forgot-password");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Reset your password", html);
        Assert.Contains("Email address", html);
        Assert.Contains("Back to sign in", html);
        Assert.Contains("/login", html);
    }

    [Fact]
    public async Task ResetPassword_page_returns_200()
    {
        var response = await _client.GetAsync(
            "/auth/reset-password?email=user%40example.com&token=abc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_page_has_password_fields()
    {
        var response = await _client.GetAsync(
            "/auth/reset-password?email=user%40example.com&token=abc");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Choose a new password", html);
        Assert.Contains("New password", html);
        Assert.Contains("Confirm new password", html);
    }
}
