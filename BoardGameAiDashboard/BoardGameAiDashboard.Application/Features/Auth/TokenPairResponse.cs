namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// Response DTO for JWT token pair (access + refresh).
/// Returned after successful login, registration, and token refresh.
/// </summary>
public sealed record TokenPairResponse
{
    /// <summary>JWT access token.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Refresh token for obtaining a new access token.</summary>
    public string RefreshToken { get; init; } = string.Empty;

    /// <summary>Number of seconds until the access token expires.</summary>
    public int ExpiresIn { get; init; }
}
