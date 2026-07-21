using MediatR;

namespace BoardGameAiDashboard.Application.Features.MatchHistory;

/// <summary>
/// CQRS command to record a completed match.
/// Planned for Phase 2 (Game CRUD + Match History).
/// </summary>
public sealed record RecordMatchCommand : IRequest<Guid>
{
    /// <summary>The game played.</summary>
    public Guid GameId { get; init; }

    /// <summary>Player IDs who participated.</summary>
    public List<Guid> PlayerIds { get; init; } = new();

    /// <summary>Player ID of the winner (nullable for draws).</summary>
    public Guid? WinnerId { get; init; }

    /// <summary>Match start time.</summary>
    public DateTime StartedAt { get; init; }

    /// <summary>Match end time.</summary>
    public DateTime EndedAt { get; init; }

    /// <summary>Optional match notes.</summary>
    public string? Notes { get; init; }
}
