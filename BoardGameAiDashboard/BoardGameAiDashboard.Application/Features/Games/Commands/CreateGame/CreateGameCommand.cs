using MediatR;

namespace BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;

/// <summary>
/// CQRS command to create a new board game.
/// </summary>
public sealed record CreateGameCommand : IRequest<CreateGameCommandResponse>
{
    /// <summary>Game display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Full description of the board game.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Minimum number of players.</summary>
    public int MinPlayers { get; init; }

    /// <summary>Maximum number of players.</summary>
    public int MaxPlayers { get; init; }
}
