using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS command to authenticate a user and issue JWT tokens.
/// </summary>
public sealed record LoginUserCommand : IRequest<TokenPairResponse>
{
    /// <summary>User's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>User's password (plain text, verified server-side).</summary>
    public string Password { get; init; } = string.Empty;
}
