using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SaasTemplate.Api.Webhooks;

/// <summary>
/// Fires outbound HMAC-signed webhook events to a configured URL (e.g., n8n).
/// Failures are logged but never affect the calling pipeline.
/// Registered as singleton; uses IHttpClientFactory to avoid captive dependency.
/// </summary>
public interface IWebhookDispatcher
{
    Task DispatchAsync(string eventType, object payload, CancellationToken ct = default);
}

public sealed class WebhookDispatcher : IWebhookDispatcher
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<WebhookDispatcher> _logger;
    private readonly string? _webhookUrl;
    private readonly string? _webhookSecret;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public WebhookDispatcher(IHttpClientFactory httpFactory, IConfiguration config, ILogger<WebhookDispatcher> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _webhookUrl = config["WEBHOOK_URL"];
        _webhookSecret = config["WEBHOOK_SECRET"];

        if (!string.IsNullOrEmpty(_webhookSecret) && _webhookSecret.Length < 32)
            throw new InvalidOperationException("WEBHOOK_SECRET must be at least 32 characters.");
    }

    public async Task DispatchAsync(string eventType, object payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
            return; // Webhook not configured — silently skip

        try
        {
            var json = JsonSerializer.Serialize(payload, JsonOpts);

            // Include timestamp in HMAC to prevent replay attacks.
            // Receiver should reject requests where timestamp is >5 min old.
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            if (!string.IsNullOrWhiteSpace(_webhookSecret))
            {
                var signature = ComputeSignature(timestamp, json, _webhookSecret);
                content.Headers.Add("X-SaasTemplate-Signature", signature);
                content.Headers.Add("X-SaasTemplate-Timestamp", timestamp);
            }

            content.Headers.Add("X-SaasTemplate-Event", eventType);

            // 5-second timeout prevents a slow/unresponsive n8n from blocking the scan worker
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var client = _httpFactory.CreateClient("webhook");
            var response = await client.PostAsync(_webhookUrl, content, timeoutCts.Token);

            _logger.LogInformation(
                "Webhook dispatched: {Event} -> {Url} (status={Status})",
                eventType, _webhookUrl, (int)response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            // Expected on timeout or app shutdown — log at debug, not warning
            _logger.LogDebug("Webhook dispatch cancelled for {Event} -> {Url} (timeout or shutdown)", eventType, _webhookUrl);
        }
        catch (Exception ex)
        {
            // Never fail the calling pipeline
            _logger.LogWarning(ex, "Webhook dispatch failed for {Event} -> {Url}", eventType, _webhookUrl);
        }
    }

    /// <summary>
    /// Signs "{timestamp}.{payload}" to bind the signature to both the content and the time,
    /// preventing replay attacks. Format: sha256={hex}.
    /// </summary>
    private static string ComputeSignature(string timestamp, string payload, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
        var hash = HMACSHA256.HashData(key, data);
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
