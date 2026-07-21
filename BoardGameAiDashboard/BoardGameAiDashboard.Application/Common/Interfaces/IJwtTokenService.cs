using BoardGameAiDashboard.Application.Features.Auth;

namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Service for generating, validating, and refreshing JWT tokens.
/// Implementation lives in the Infrastructure layer.
/// Throws <see cref="BoardGameAiDashboard.Application.Common.Exceptions.UnauthorizedException"/>
/// when token operations fail (e.g., invalid credentials, expired refresh token).
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generate a new access token + refresh token pair for the given user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="email">The user's email (used as the JWT subject claim).</param>
    /// <param name="ipAddress">IP address of the requesting client (for audit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TokenPairResponse"/> with the newly created tokens.</returns>
    Task<TokenPairResponse> GenerateTokenPairAsync(
        Guid userId, string email, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate and rotate a refresh token — revoke the old one and issue a new pair.
    /// </summary>
    /// <param name="refreshToken">The refresh token to exchange.</param>
    /// <param name="ipAddress">IP address of the requesting client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TokenPairResponse"/> with the new token pair.</returns>
    Task<TokenPairResponse> RefreshTokenAsync(
        string refreshToken, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke all active refresh tokens for a user (e.g., on password change).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
}
