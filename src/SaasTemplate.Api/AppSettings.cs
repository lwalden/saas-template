namespace SaasTemplate.Api;

/// <summary>
/// Validated application URL settings. Registered as singleton at startup.
/// Centralises the APP_BASE_URL and APP_FRONTEND_URL defaults and validation
/// so individual services don't repeat the fallback string.
/// </summary>
public sealed class AppSettings
{
    /// <summary>API base URL (Blazor dashboard, billing redirects, magic links).</summary>
    public string BaseUrl { get; }

    /// <summary>Frontend/landing page URL (retry links for non-subscribers, pricing links).</summary>
    public string FrontendUrl { get; }

    public AppSettings(IConfiguration config)
    {
        BaseUrl = ValidateUrl(
            config["APP_BASE_URL"] ?? "https://api.example.com",
            "APP_BASE_URL");

        FrontendUrl = ValidateUrl(
            config["APP_FRONTEND_URL"] ?? "https://www.example.com",
            "APP_FRONTEND_URL");
    }

    private static string ValidateUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            throw new InvalidOperationException(
                $"{name} must be a valid http:// or https:// URL. Got: \"{value}\"");
        }

        // Reject URLs with path, query, or fragment — must be a clean origin
        if (uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"{name} must be an origin URL without a path, query, or fragment. Got: \"{value}\"");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
