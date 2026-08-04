using BoardGameAiDashboard.Application.Features.ML.Models;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.PredictWinRate;

/// <summary>
/// Command to predict win rate based on game state.
/// </summary>
public sealed record PredictWinRateCommand : IRequest<GameStatePredictionResult>
{
    /// <summary>Game state features for prediction.</summary>
    public GameStatePredictionInput Input { get; init; } = new();
}
