using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.UpdateGame;

/// <summary>
/// CQRS command to update an existing board game.
/// </summary>
public sealed record UpdateGameCommand : IRequest<UpdateGameCommandResponse>
{
    /// <summary>The unique identifier of the game to update.</summary>
    public Guid Id { get; init; }

    /// <summary>Game display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Full description of the board game.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Minimum number of players.</summary>
    public int MinPlayers { get; init; }

    /// <summary>Maximum number of players.</summary>
    public int MaxPlayers { get; init; }
}
