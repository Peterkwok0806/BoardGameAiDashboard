using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// Placeholder handler for user registration.
/// Planned for Phase 6 (JWT + ASP.NET Core Identity).
/// </summary>
internal sealed class RegisterUserCommandHandler
    : IRequestHandler<RegisterUserCommand, AuthResultDto>
{
    public Task<AuthResultDto> Handle(
        RegisterUserCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException(
            "User registration is planned for Phase 6 (JWT + Identity).");
    }
}
