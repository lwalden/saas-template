namespace SaasTemplate.Api.Auth;

public class JwtSettings
{
    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "SaasTemplate";
    public string Audience { get; set; } = "SaasTemplate";
    public int ExpiryMinutes { get; set; } = 60;
}
