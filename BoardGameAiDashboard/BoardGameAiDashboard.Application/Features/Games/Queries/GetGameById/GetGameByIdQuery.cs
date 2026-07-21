using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Queries.GetGameById;

/// <summary>
/// CQRS query to retrieve a single game by its unique identifier, including navigation counts.
/// </summary>
public sealed record GetGameByIdQuery : IRequest<GameDetailDto>
{
    /// <summary>The unique identifier of the game to retrieve.</summary>
    public Guid Id { get; init; }
}
