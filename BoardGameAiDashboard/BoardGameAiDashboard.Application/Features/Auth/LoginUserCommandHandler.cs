using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// Placeholder handler for user login.
/// Planned for Phase 6 (JWT + ASP.NET Core Identity).
/// </summary>
internal sealed class LoginUserCommandHandler
    : IRequestHandler<LoginUserCommand, AuthResultDto>
{
    public Task<AuthResultDto> Handle(
        LoginUserCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "User login is planned for Phase 6 (JWT + Identity).");
    }
}
