using SaasTemplate.Api.Data;

namespace SaasTemplate.Api.Auditing;

/// <summary>
/// Records security- and account-relevant actions to the audit trail.
/// Calls are resilient: a failure to write an audit entry is logged but never
/// propagates, so auditing can't break the originating request.
/// </summary>
public interface IAuditLogger
{
    /// <param name="action">A canonical name from <see cref="AuditAction"/>.</param>
    /// <param name="userId">Acting user id, or null for anonymous/system actions.</param>
    /// <param name="email">Denormalised actor email for readability.</param>
    /// <param name="metadata">Optional context, serialised to JSON. Never pass secrets.</param>
    Task LogAsync(
        string action,
        string? userId = null,
        string? email = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
