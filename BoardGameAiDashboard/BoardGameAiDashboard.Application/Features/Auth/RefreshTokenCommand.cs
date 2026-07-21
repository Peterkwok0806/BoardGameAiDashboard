using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS command to refresh an expired JWT access token.
/// Planned for Phase 6 (JWT + Identity).
/// </summary>
public sealed record RefreshTokenCommand : IRequest<AuthResultDto>
{
    /// <summary>The refresh token to exchange for a new access token.</summary>
    public string RefreshToken { get; init; } = string.Empty;
}
