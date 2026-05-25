namespace SaasTemplate.Api.Auth;

/// <summary>
/// Scoped service (one per Blazor Server circuit) that holds the authenticated user's
/// JWT and UserId for the lifetime of the SignalR session.
/// </summary>
public sealed class DashboardSession
{
    public string? Token { get; private set; }
    public string? UserId { get; private set; }
    public string? Email { get; private set; }

    public bool IsAuthenticated => Token is not null && UserId is not null;

    public void SetUser(string token, string userId, string email)
    {
        Token = token;
        UserId = userId;
        Email = email;
    }

    public void Clear()
    {
        Token = null;
        UserId = null;
        Email = null;
    }
}
