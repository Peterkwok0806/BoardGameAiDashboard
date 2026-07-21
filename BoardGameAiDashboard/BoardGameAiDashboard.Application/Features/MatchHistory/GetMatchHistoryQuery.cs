using MediatR;

namespace BoardGameAiDashboard.Application.Features.MatchHistory;

/// <summary>
/// CQRS query to retrieve match history for a specific game.
/// Planned for Phase 2 (Game CRUD + Match History).
/// </summary>
public sealed record GetMatchHistoryQuery : IRequest<List<MatchHistoryDto>>
{
    /// <summary>The game identifier.</summary>
    public Guid GameId { get; init; }

    /// <summary>Maximum number of matches to return.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>DTO for a single match history entry.</summary>
public sealed record MatchHistoryDto
{
    /// <summary>Match identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Game identifier.</summary>
    public Guid GameId { get; init; }

    /// <summary>Player IDs who participated.</summary>
    public List<Guid> PlayerIds { get; init; } = new();

    /// <summary>Winner player ID (nullable for draws).</summary>
    public Guid? WinnerId { get; init; }

    /// <summary>Match start time.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Match end time.</summary>
    public DateTime EndedAt { get; init; }

    /// <summary>Match duration in minutes.</summary>
    public double DurationMinutes { get; init; }

    /// <summary>Optional match notes.</summary>
    public string? Notes { get; init; }
}
