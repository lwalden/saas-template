using System.Net;
using System.Net.Sockets;

namespace SaasTemplate.Api.Security;

/// <summary>
/// Guards against Server-Side Request Forgery (SSRF) by blocking requests
/// to private, link-local, loopback, and reserved IP ranges (SA-011).
/// </summary>
public static class SsrfGuard
{
    public sealed record BlockResult(bool IsBlocked, string? Reason = null);

    /// <summary>
    /// Resolves the host via DNS and checks resolved addresses against blocked IP ranges.
    /// Blocks if ALL resolved IPs are private/reserved (allows CDN-fronted stores with mixed results).
    /// Exception: loopback (127.x) and metadata (169.254.x) block on ANY match.
    /// </summary>
    public static async Task<BlockResult> IsBlockedHostAsync(string host, ILogger? logger = null)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host);
        }
        catch (Exception ex)
        {
            logger?.LogWarning("SSRF: DNS resolution failed for {Host}: {Error}", host, ex.Message);
            return new BlockResult(true, $"Could not resolve hostname '{host}'. Check the URL and try again.");
        }

        if (addresses.Length == 0)
        {
            logger?.LogWarning("SSRF: DNS returned 0 addresses for {Host}", host);
            return new BlockResult(true, $"Could not resolve hostname '{host}'. Check the URL and try again.");
        }

        // Security-critical: loopback and metadata endpoints block on ANY match
        foreach (var address in addresses)
        {
            var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            if (normalized.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = normalized.GetAddressBytes();
                // Loopback: 127.0.0.0/8
                if (b[0] == 127)
                {
                    logger?.LogWarning("SSRF: blocked {Host} — loopback address {IP}", host, address);
                    return new BlockResult(true, "URL must point to a publicly accessible host.");
                }
                // Link-local / metadata: 169.254.0.0/16
                if (b[0] == 169 && b[1] == 254)
                {
                    logger?.LogWarning("SSRF: blocked {Host} — metadata/link-local address {IP}", host, address);
                    return new BlockResult(true, "URL must point to a publicly accessible host.");
                }
            }
            else if (normalized.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (normalized.Equals(IPAddress.IPv6Loopback))
                {
                    logger?.LogWarning("SSRF: blocked {Host} — IPv6 loopback", host);
                    return new BlockResult(true, "URL must point to a publicly accessible host.");
                }
            }
        }

        // For other private ranges: block only if ALL resolved IPs are private.
        // CDN-fronted stores may resolve to both public and private IPs.
        var hasPublicIp = false;
        foreach (var address in addresses)
        {
            if (!IsPrivateAddress(address))
            {
                hasPublicIp = true;
                break;
            }
        }

        if (!hasPublicIp)
        {
            var ips = string.Join(", ", addresses.Select(a => a.ToString()));
            logger?.LogWarning("SSRF: blocked {Host} — all resolved IPs are private: [{IPs}]", host, ips);
            return new BlockResult(true, "URL must point to a publicly accessible host.");
        }

        return new BlockResult(false);
    }

    /// <summary>
    /// Backward-compatible overload for callers that don't need the reason.
    /// </summary>
    public static async Task<bool> IsBlockedAsync(string host, ILogger? logger = null)
    {
        var result = await IsBlockedHostAsync(host, logger);
        return result.IsBlocked;
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateIPv4(address),
            AddressFamily.InterNetworkV6 => IsPrivateIPv6(address),
            _ => false
        };
    }

    private static bool IsPrivateIPv4(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return
            b[0] == 127 ||                              // Loopback: 127.0.0.0/8
            (b[0] == 169 && b[1] == 254) ||             // Link-local: 169.254.0.0/16
            b[0] == 10 ||                               // RFC1918 Class A: 10.0.0.0/8
            (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || // RFC1918 Class B: 172.16.0.0/12
            (b[0] == 192 && b[1] == 168) ||             // RFC1918 Class C: 192.168.0.0/16
            b[0] == 0 ||                                // This network: 0.0.0.0/8
            (b[0] >= 224 && b[0] <= 239) ||             // Multicast: 224.0.0.0/4
            b[0] >= 240;                                // Reserved: 240.0.0.0/4
    }

    private static bool IsPrivateIPv6(IPAddress address)
    {
        if (address.Equals(IPAddress.IPv6Loopback)) return true;
        if (address.Equals(IPAddress.IPv6Any)) return true;

        var b = address.GetAddressBytes();
        if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) return true; // Link-local: fe80::/10
        if ((b[0] & 0xfe) == 0xfc) return true;                  // Unique-local: fc00::/7

        return false;
    }
}
