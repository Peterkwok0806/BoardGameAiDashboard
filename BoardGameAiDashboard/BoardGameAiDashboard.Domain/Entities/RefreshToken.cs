using BoardGameAiDashboard.Domain.Common;

namespace BoardGameAiDashboard.Domain.Entities;

/// <summary>
/// Refresh token for JWT token rotation.
/// Stores the refresh token hash, expiration, and revocation status.
/// </summary>
public class RefreshToken : BaseEntity
{
    /// <summary>The refresh token value (cryptographically generated).</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>Foreign key to the owning user.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Navigation: the owning user.</summary>
    public virtual User User { get; private set; } = null!;

    /// <summary>When this refresh token expires.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Whether this token has been revoked (e.g., on logout or rotation).</summary>
    public bool IsRevoked { get; private set; }

    /// <summary>IP address that created this token (for audit).</summary>
    public string? CreatedByIp { get; private set; }

    private RefreshToken() { } // EF Core constructor

    public RefreshToken(string token, Guid userId, DateTime expiresAt, string? createdByIp = null)
    {
        Token = token;
        UserId = userId;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
    }

    /// <summary>
    /// Check whether this token is still valid (not revoked and not expired).
    /// </summary>
    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Revoke this token (e.g., on logout or when rotating).
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
        MarkUpdated();
    }
}
