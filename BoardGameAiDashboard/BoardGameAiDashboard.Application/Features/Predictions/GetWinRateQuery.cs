using MediatR;

namespace BoardGameAiDashboard.Application.Features.Predictions;

/// <summary>
/// CQRS query to get win rate predictions for a specific game.
/// Planned for Phase 8 (ML Predictions).
/// </summary>
public sealed record GetWinRateQuery : IRequest<WinRateDto>
{
    /// <summary>The game identifier.</summary>
    public Guid GameId { get; init; }
}

/// <summary>DTO for win rate prediction results.</summary>
public sealed record WinRateDto
{
    /// <summary>Overall win rate percentage (0-100).</summary>
    public double WinRate { get; init; }

    /// <summary>Number of matches analyzed.</summary>
    public int MatchesAnalyzed { get; init; }

    /// <summary>Confidence level of the prediction.</summary>
    public double Confidence { get; init; }
}
