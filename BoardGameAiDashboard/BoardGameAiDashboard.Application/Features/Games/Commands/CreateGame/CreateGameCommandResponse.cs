namespace BoardGameAiDashboard.Application.Features.Games.Commands.CreateGame;

/// <summary>
/// Response DTO returned after successfully creating a game.
/// </summary>
public sealed record CreateGameCommandResponse
{
    /// <summary>The newly created game's unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Game display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Full description of the board game.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Minimum number of players.</summary>
    public int MinPlayers { get; init; }

    /// <summary>Maximum number of players.</summary>
    public int MaxPlayers { get; init; }

    /// <summary>UTC timestamp when the game was created.</summary>
    public DateTime CreatedAt { get; init; }
}
