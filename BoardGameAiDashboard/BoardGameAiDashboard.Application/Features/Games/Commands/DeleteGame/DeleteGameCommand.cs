using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.DeleteGame;

/// <summary>
/// CQRS command to soft-delete a board game by its identifier.
/// </summary>
public sealed record DeleteGameCommand : IRequest<Unit>
{
    /// <summary>The unique identifier of the game to delete.</summary>
    public Guid Id { get; init; }
}
