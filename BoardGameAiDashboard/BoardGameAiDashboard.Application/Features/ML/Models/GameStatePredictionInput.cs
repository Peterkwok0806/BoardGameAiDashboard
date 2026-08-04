namespace BoardGameAiDashboard.Application.Features.ML.Models;

/// <summary>
/// Input model for win rate prediction.
/// Matches the CSV columns exported from MatchHistory.
/// </summary>
public sealed class GameStatePredictionInput
{
    /// <summary>Game identifier (optional, for tracking).</summary>
    public Guid? GameId { get; init; }

    /// <summary>Number of players in the match.</summary>
    public float PlayerCount { get; init; }

    /// <summary>Hour of day when the game was played (0-23).</summary>
    public float HourOfDay { get; init; }

    /// <summary>Day of week (0=Sunday, 6=Saturday).</summary>
    public float DayOfWeek { get; init; }

    /// <summary>Hero level at the time of prediction.</summary>
    public float HeroLevel { get; init; }

    /// <summary>Number of hero kills.</summary>
    public float HeroKills { get; init; }

    /// <summary>Number of deaths.</summary>
    public float Deaths { get; init; }

    /// <summary>Number of unit/minion kills.</summary>
    public float UnitKills { get; init; }

    /// <summary>Total gold accumulated.</summary>
    public float TotalGold { get; init; }

    /// <summary>Highest attack stat.</summary>
    public float HighestAtk { get; init; }

    /// <summary>Highest defense stat.</summary>
    public float HighestDef { get; init; }

    /// <summary>Highest speed stat.</summary>
    public float HighestSpeed { get; init; }

    /// <summary>Attack range.</summary>
    public float AtkRange { get; init; }
}
