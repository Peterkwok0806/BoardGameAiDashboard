using BoardGameAiDashboard.Application.Common.Exceptions;
using BoardGameAiDashboard.Application.Common.Interfaces;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS handler for JWT token refresh.
/// Validates the refresh token, revokes it, and issues a new token pair.
/// Throws <see cref="UnauthorizedException"/> if the refresh token is invalid or expired.
/// </summary>
internal sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, TokenPairResponse>
{
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    public async Task<TokenPairResponse> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        return await _jwtTokenService.RefreshTokenAsync(
            request.RefreshToken,
            ipAddress: string.Empty,
            cancellationToken);
    }
}
