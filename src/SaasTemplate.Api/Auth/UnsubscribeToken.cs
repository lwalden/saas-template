using System.Security.Cryptography;
using System.Text;

namespace SaasTemplate.Api.Auth;

/// <summary>
/// Generates and validates HMAC-SHA256 signed unsubscribe tokens (CAN-SPAM, S10-004).
/// Token = base64url(HMAC-SHA256(email.ToLower(), secret)).
/// </summary>
public static class UnsubscribeToken
{
    /// <summary>Generates a URL-safe base64 unsubscribe token for the given email.</summary>
    public static string Generate(string email, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Validates a token in constant time to prevent timing attacks.
    /// Returns true if the token is valid for the given email and secret.
    /// </summary>
    public static bool Validate(string email, string token, string secret)
    {
        var expected = Generate(email, secret);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(token);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
