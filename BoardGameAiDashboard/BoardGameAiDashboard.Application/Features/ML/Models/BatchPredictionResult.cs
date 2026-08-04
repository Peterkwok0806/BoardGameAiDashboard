namespace BoardGameAiDashboard.Application.Features.ML.Models;

/// <summary>
/// Result model for batch win rate predictions.
/// </summary>
public sealed class BatchPredictionResult
{
    /// <summary>
    /// Gets the list of prediction results in the same order as inputs.
    /// </summary>
    public required IReadOnlyList<BatchPredictionItem> Predictions { get; init; }

    /// <summary>
    /// Gets the total number of predictions made.
    /// </summary>
    public int TotalCount => Predictions.Count;

    /// <summary>
    /// Gets the count of predictions with win probability above 0.5.
    /// </summary>
    public int FavorableCount => Predictions.Count(p => p.WinProbability > 0.5f);

    /// <summary>
    /// Gets the average win probability across all predictions.
    /// </summary>
    public float AverageWinProbability =>
        Predictions.Count > 0
            ? Predictions.Average(p => p.WinProbability)
            : 0f;
}

/// <summary>
/// Individual prediction result for batch processing.
/// </summary>
public sealed class BatchPredictionItem
{
    /// <summary>
    /// Gets the index of this prediction in the batch (0-based).
    /// </summary>
    public required int Index { get; init; }

    /// <summary>
    /// Gets the input that was used for this prediction.
    /// </summary>
    public required GameStatePredictionInput Input { get; init; }

    /// <summary>
    /// Gets the predicted win probability (0.0 to 1.0).
    /// </summary>
    public required float WinProbability { get; init; }

    /// <summary>
    /// Gets the confidence score (0.0 to 1.0).
    /// Higher confidence indicates the model is more certain about this prediction.
    /// </summary>
    public required float ConfidenceScore { get; init; }

    /// <summary>
    /// Gets the strategic recommendation based on this prediction.
    /// </summary>
    public required string Recommendation { get; init; }
}
