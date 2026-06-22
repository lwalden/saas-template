using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Auditing;

/// <summary>
/// Default <see cref="IAuditLogger"/> that persists audit events to the application
/// database. IP/user-agent are captured from the current HTTP context when present.
/// </summary>
/// <remarks>
/// The write is awaited inline but wrapped so it never throws into the caller. For
/// high-throughput hot paths this should move onto the background-job queue
/// (backlog FEAT-13) so the request thread isn't blocked on the insert.
/// </remarks>
public sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(AppDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<AuditLogger> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string? userId = null,
        string? email = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var http = _httpContextAccessor.HttpContext;
            var userAgent = http?.Request.Headers.UserAgent.ToString();

            var entry = new AuditEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Action = action,
                UserId = userId,
                Email = email,
                TargetType = targetType,
                TargetId = targetId,
                IpAddress = http?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Truncate(string.IsNullOrEmpty(userAgent) ? null : userAgent, 512),
                Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata)
            };

            _db.AuditEvents.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Auditing must never break the action being audited.
            _logger.LogError(ex, "Failed to write audit event {Action} for user {UserId}", action, userId);
        }
    }

    private static string? Truncate(string? value, int max) =>
        value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}
