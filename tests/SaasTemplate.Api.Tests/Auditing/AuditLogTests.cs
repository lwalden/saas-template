using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaasTemplate.Api.Data;
using Xunit;

namespace SaasTemplate.Api.Tests.Auditing;

public class AuditLogTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public AuditLogTests(ApiTestFactory factory)
    {
        _factory = factory;
        factory.EnsureSchema();
        _client = factory.CreateClient();
    }

    private async Task<List<AuditEvent>> EventsForAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.AuditEvents
            .AsNoTracking()
            .Where(a => a.Email == email)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();
    }

    [Fact]
    public async Task Register_writes_user_registered_event()
    {
        var email = $"audit-reg-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        var events = await EventsForAsync(email);
        Assert.Contains(events, e => e.Action == AuditAction.UserRegistered);
        // The registered event carries the acting user's id.
        Assert.All(events.Where(e => e.Action == AuditAction.UserRegistered), e => Assert.False(string.IsNullOrEmpty(e.UserId)));
    }

    [Fact]
    public async Task Successful_login_writes_login_succeeded_event()
    {
        var email = $"audit-login-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });
        await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "P@ssword12345!" });

        var events = await EventsForAsync(email);
        Assert.Contains(events, e => e.Action == AuditAction.LoginSucceeded);
    }

    [Fact]
    public async Task Failed_login_writes_login_failed_event_with_reason()
    {
        var email = $"audit-fail-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });
        await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongP@ss12345!" });

        var events = await EventsForAsync(email);
        var failed = Assert.Single(events, e => e.Action == AuditAction.LoginFailed);
        Assert.Contains("invalid_credentials", failed.Metadata);
    }

    [Fact]
    public async Task Failed_login_for_unknown_user_is_still_audited()
    {
        var email = $"audit-ghost-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongP@ss12345!" });

        var events = await EventsForAsync(email);
        var failed = Assert.Single(events, e => e.Action == AuditAction.LoginFailed);
        // No account exists, so there is no acting user id — the email is still recorded.
        Assert.Null(failed.UserId);
    }

    [Fact]
    public async Task Ops_audit_endpoint_requires_api_key()
    {
        var response = await _client.GetAsync("/api/ops/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ops_audit_endpoint_returns_events_for_user()
    {
        var email = $"audit-ops-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password = "P@ssword12345!" });

        string userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = (await db.AuditEvents.AsNoTracking().FirstAsync(a => a.Email == email && a.Action == AuditAction.UserRegistered)).UserId!;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/ops/audit?userId={userId}");
        request.Headers.Add("X-Api-Key", ApiTestFactory.TestOpsApiKey);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuditListResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Events);
        Assert.Contains(body.Events, e => e.Action == AuditAction.UserRegistered);
    }

    private sealed record AuditListResponse(int Count, List<AuditItem> Events);
    private sealed record AuditItem(string Action, string? UserId, string? Email);
}
