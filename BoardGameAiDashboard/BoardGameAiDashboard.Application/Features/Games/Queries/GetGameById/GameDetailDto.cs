namespace BoardGameAiDashboard.Application.Features.Games.Queries.GetGameById;

/// <summary>
/// Detailed DTO for a single game, including navigation collection counts.
/// </summary>
public sealed record GameDetailDto
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

    /// <summary>Number of rule chunks (RAG document sources).</summary>
    public int RuleChunkCount { get; init; }

    /// <summary>Number of characters.</summary>
    public int CharacterCount { get; init; }

    /// <summary>Number of cards.</summary>
    public int CardCount { get; init; }

    /// <summary>Number of match history records.</summary>
    public int MatchHistoryCount { get; init; }
}
