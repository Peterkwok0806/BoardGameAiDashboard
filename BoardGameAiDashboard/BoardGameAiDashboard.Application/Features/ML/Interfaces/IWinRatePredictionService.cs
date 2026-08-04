using BoardGameAiDashboard.Application.Features.ML.Models;

namespace BoardGameAiDashboard.Application.Features.ML.Interfaces;

/// <summary>
/// Service for win rate prediction using ONNX Runtime.
/// </summary>
public interface IWinRatePredictionService
{
    /// <summary>
    /// Predicts win probability based on game state features.
    /// </summary>
    /// <param name="input">Game state features.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Prediction result with probability and insights.</returns>
    Task<GameStatePredictionResult> PredictWinRateAsync(
        GameStatePredictionInput input,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the ONNX model is loaded and ready.
    /// </summary>
    bool IsModelLoaded { get; }

    /// <summary>
    /// Reloads the ONNX model from disk.
    /// </summary>
    Task ReloadModelAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the path to the currently loaded model.
    /// </summary>
    string? ModelPath { get; }

    /// <summary>
    /// Predicts win probability for multiple game states in a batch.
    /// </summary>
    /// <param name="inputs">Collection of game state features.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch prediction results.</returns>
    Task<BatchPredictionResult> BatchPredictAsync(
        IReadOnlyList<GameStatePredictionInput> inputs,
        CancellationToken ct = default);
}
