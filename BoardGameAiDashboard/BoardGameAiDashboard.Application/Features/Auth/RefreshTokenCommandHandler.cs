using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// Placeholder handler for token refresh.
/// Planned for Phase 6 (JWT + ASP.NET Core Identity).
/// </summary>
internal sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, AuthResultDto>
{
    public Task<AuthResultDto> Handle(
        RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "Token refresh is planned for Phase 6 (JWT + Identity).");
    }
}
