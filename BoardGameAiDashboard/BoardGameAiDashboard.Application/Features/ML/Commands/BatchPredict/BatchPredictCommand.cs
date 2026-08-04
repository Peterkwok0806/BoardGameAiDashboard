using BoardGameAiDashboard.Application.Features.ML.Models;
using MediatR;

namespace BoardGameAiDashboard.Application.Features.ML.Commands.BatchPredict;

/// <summary>
/// Command for batch win rate predictions.
/// Processes multiple game states in a single request for efficiency.
/// </summary>
public sealed record BatchPredictCommand : IRequest<BatchPredictionResult>
{
    /// <summary>
    /// Gets the collection of game states to predict.
    /// </summary>
    public required IReadOnlyList<GameStatePredictionInput> Inputs { get; init; }

    /// <summary>
    /// Maximum number of predictions allowed in a single batch.
    /// </summary>
    public const int MaxBatchSize = 50;
}
