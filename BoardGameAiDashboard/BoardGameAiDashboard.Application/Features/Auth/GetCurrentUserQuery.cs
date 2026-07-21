using MediatR;

namespace BoardGameAiDashboard.Application.Features.Auth;

/// <summary>
/// CQRS query to retrieve the currently authenticated user's profile.
/// </summary>
/// <param name="UserId">The authenticated user's ID (extracted from JWT claims by the controller).</param>
public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<UserProfileDto>;

/// <summary>
/// DTO for the authenticated user's profile information.
/// </summary>
public sealed record UserProfileDto
{
    /// <summary>User's unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>User's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>User's display name.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>When the account was created (UTC).</summary>
    public DateTime CreatedAt { get; init; }
}
