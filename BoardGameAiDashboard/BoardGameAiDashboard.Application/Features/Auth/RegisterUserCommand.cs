using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS command to register a new user account.
/// </summary>
public sealed record RegisterUserCommand : IRequest<TokenPairResponse>
{
    /// <summary>User's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>User's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>User's password (plain text, will be hashed server-side).</summary>
    public string Password { get; init; } = string.Empty;
}
