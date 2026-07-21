namespace BoardGameAiDashboard.Application.Features.Games.Queries.GetGames;

/// <summary>
/// DTO for game list items returned by <see cref="GetGamesQuery"/>.
/// </summary>
public sealed record GameDto
{
    /// <summary>Unique identifier.</summary>
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

    /// <summary>UTC timestamp when the game was last updated.</summary>
    public DateTime? UpdatedAt { get; init; }
}
