using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS command to register a new user account.
/// Planned for Phase 6 (JWT + Identity).
/// </summary>
public sealed record RegisterUserCommand : IRequest<AuthResultDto>
{
    /// <summary>User's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>User's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>User's password (plain text, will be hashed server-side).</summary>
    public string Password { get; init; } = string.Empty;
}

/// <summary>DTO for authentication results.</summary>
public sealed record AuthResultDto
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>JWT access token.</summary>
    public string? AccessToken { get; init; }

    /// <summary>Refresh token.</summary>
    public string? RefreshToken { get; init; }

    /// <summary>Token expiration in seconds.</summary>
    public int ExpiresIn { get; init; }

    /// <summary>Error message if the operation failed.</summary>
    public string? Error { get; init; }
}
